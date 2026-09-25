using System.Collections;
using UnityEngine;
using UnityEngine.Splines;

// Put this on both trucks. Truck 1 (the leader): leave "Leader" empty.
// Truck 2 (the follower): assign Truck 1's MissionTruckFollower into
// its "Leader" field - it then tracks Truck 1's progress at a fixed
// gap instead of moving independently, so it automatically stays the
// right distance behind even through the pause and the final stop.
public class MissionTruckFollower : MonoBehaviour
{
    private enum State { DrivingToStop, PausedAtStop, DrivingToEnd, StoppedAtEnd }

    public SplineContainer spline;
    public float speed = 5f;
    [Tooltip("How quickly the truck speeds up / slows down, in units per second squared")]
    public float acceleration = 4f;
    [Tooltip("Starts easing toward a stop this many units before it, instead of halting instantly")]
    public float brakingDistance = 15f;

    [Tooltip("Leave empty for the lead truck. Assign the lead truck here to follow it at a fixed gap instead.")]
    public MissionTruckFollower leader;
    public float followGapDistance = 10f;

    [Header("Stop point (temporary pause)")]
    public int stopKnotIndex;
    public float stopDurationSeconds = 120f;

    [Header("End point (permanent halt)")]
    [Tooltip("Knot index where the truck comes to a complete, permanent stop")]
    public int endKnotIndex;

    [Header("Turning")]
    public float turnSpeedDegrees = 180f;

    [Header("Ground snapping")]
    [Tooltip("Fixes trucks floating where the road sits higher than the surrounding terrain. Increase if trucks still float - it means the gap between the spline's authored height and actual ground is bigger than this reaches.")]
    public LayerMask groundMask = ~0;
    public float groundSearchHeight = 50f;
    [Tooltip("Manual nudge applied after ground snapping - use this if the truck model's own pivot isn't at its wheelbase")]
    public float groundOffset = 0f;

    [Tooltip("Starts moving automatically on Play - turn this off once Mission3Manager calls BeginMoving() itself")]
    public bool autoStart = true;

    public float DistanceTravelled { get; private set; }
    public float CurrentSpeed => currentSpeed;

    private State state = State.DrivingToStop;
    private float currentSpeed;
    private float stopKnotDistance;
    private float endKnotDistance;
    private float splineLength;
    private bool loggedGroundMiss;
    private bool moving;

    private void Start()
    {
        splineLength = spline.CalculateLength();
        stopKnotDistance = ComputeDistanceAtKnot(stopKnotIndex);
        endKnotDistance = ComputeDistanceAtKnot(endKnotIndex);

        if (autoStart) BeginMoving();
    }

    public void BeginMoving()
    {
        moving = true;
    }

    // Called externally (e.g. an EMP system) to force an immediate,
    // permanent stop wherever the truck currently is. Call this on the
    // leader only - any follower mirrors this automatically, since its
    // own position is derived from the leader's DistanceTravelled.
    public void ForceStop()
    {
        currentSpeed = 0f;
        state = State.StoppedAtEnd;
        Debug.Log($"{name} disabled and stopped.");
    }

    private void Update()
    {
        if (spline == null) return;

        if (leader != null)
        {
            // Mirrors the leader's progress minus a fixed gap - stays
            // correct even while the leader is paused or stopped.
            DistanceTravelled = Mathf.Max(0f, leader.DistanceTravelled - followGapDistance);
        }
        else if (moving)
        {
            UpdateOwnMovement();
        }

        float t = Mathf.Clamp01(DistanceTravelled / splineLength);
        Vector3 pos = SnapToGround(spline.EvaluatePosition(t));

        // Face the direction actually travelled this frame, not the
        // spline's theoretical tangent - this can't drift or lag
        // behind a curve, since it's derived from the real motion.
        Vector3 moveDelta = pos - transform.position;
        transform.position = pos;

        if (moveDelta.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDelta.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeedDegrees * Time.deltaTime);
        }
    }

    private void UpdateOwnMovement()
    {
        float targetSpeed = speed;

        if (state == State.DrivingToStop)
        {
            float distToStop = stopKnotDistance - DistanceTravelled;
            if (distToStop <= brakingDistance)
            {
                // Scales down WITH the actual remaining distance, so it
                // can never hit zero speed before actually arriving.
                targetSpeed = speed * Mathf.Clamp01(distToStop / brakingDistance);
            }
        }
        else if (state == State.DrivingToEnd)
        {
            float distToEnd = endKnotDistance - DistanceTravelled;
            if (distToEnd <= brakingDistance)
            {
                targetSpeed = speed * Mathf.Clamp01(distToEnd / brakingDistance);
            }
        }
        else
        {
            targetSpeed = 0f; // paused or already stopped
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        DistanceTravelled += currentSpeed * Time.deltaTime;

        // Snap firmly once very close, instead of crawling asymptotically forever
        if (state == State.DrivingToStop && DistanceTravelled >= stopKnotDistance - 0.05f)
        {
            DistanceTravelled = stopKnotDistance;
            currentSpeed = 0f;
            state = State.PausedAtStop;
            Debug.Log($"Truck paused at farm stop point for {stopDurationSeconds:F0}s.");
            StartCoroutine(ResumeAfterStop());
        }
        else if (state == State.DrivingToEnd && DistanceTravelled >= endKnotDistance - 0.05f)
        {
            DistanceTravelled = endKnotDistance;
            currentSpeed = 0f;
            state = State.StoppedAtEnd;
            Debug.Log("Truck reached the destination and stopped for good.");
        }
    }

    private IEnumerator ResumeAfterStop()
    {
        yield return new WaitForSeconds(stopDurationSeconds);
        state = State.DrivingToEnd;
        Debug.Log("Truck resuming.");
        Mission3Manager.Instance?.OnTrucksLeavingFarm();
    }

    private Vector3 SnapToGround(Vector3 pos)
    {
        Vector3 rayOrigin = pos + Vector3.up * groundSearchHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundSearchHeight * 2f, groundMask))
        {
            pos.y = hit.point.y + groundOffset;
        }
        else if (!loggedGroundMiss)
        {
            loggedGroundMiss = true;
            Debug.LogWarning($"{name}: ground raycast found nothing near {pos} - the gap between the spline's height here and actual ground may exceed Ground Search Height, or the Ground layer doesn't cover this spot.");
        }
        return pos;
    }

    private float ComputeDistanceAtKnot(int knotIndex)
    {
        SplineUtility.GetNearestPoint(spline.Spline, spline.Spline[knotIndex].Position, out _, out float t);
        return t * splineLength;
    }
}