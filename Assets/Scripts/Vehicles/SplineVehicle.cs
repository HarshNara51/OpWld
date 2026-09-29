using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;

// Moves a vehicle along a SplineContainer by real distance travelled.
// Used by the Farm trucks (Mission 3) and the train (Mission 1).
//
// Lead vehicle: leave "Leader" empty. Followers (truck 2, train cars):
// assign the vehicle directly ahead. A follower derives its position
// from its leader's progress minus a fixed gap, so the whole convoy
// pauses, departs and stops together with no extra logic.
public class SplineVehicle : MonoBehaviour
{
    public enum StopRelease { Timed, External }
    private enum State { DrivingToStop, PausedAtStop, DrivingToEnd, StoppedAtEnd }

    public SplineContainer spline;
    public float speed = 5f;
    [Tooltip("How quickly the vehicle speeds up / slows down, in units per second squared")]
    public float acceleration = 4f;
    [Tooltip("Starts easing toward a stop this many units before it, instead of halting instantly")]
    public float brakingDistance = 15f;

    [Tooltip("Leave empty for the lead vehicle. Assign the vehicle directly ahead to follow it at a fixed gap.")]
    public SplineVehicle leader;
    [Tooltip("Pivot-to-pivot distance to the leader")]
    public float followGapDistance = 10f;

    [Header("Stop point (temporary pause)")]
    public int stopKnotIndex;
    [Tooltip("Timed: resumes after Stop Duration Seconds. External: waits at the stop until Depart() is called (e.g. by a mission manager).")]
    public StopRelease stopRelease = StopRelease.Timed;
    public float stopDurationSeconds = 120f;

    [Header("End point (permanent halt)")]
    [Tooltip("Knot index where the vehicle comes to a complete, permanent stop")]
    public int endKnotIndex;

    [Header("Orientation")]
    [Tooltip("On: faces the direction actually moved, smoothed (road vehicles). Off: locks exactly to the spline's direction (trains on rails).")]
    public bool faceMoveDirection = true;
    public float turnSpeedDegrees = 180f;

    [Header("Height")]
    [Tooltip("On: raycasts down to the ground (road vehicles). Off: sits exactly on the spline (trains - the spline IS the rails).")]
    public bool snapToGround = true;
    public LayerMask groundMask = ~0;
    public float groundSearchHeight = 50f;
    [Tooltip("Vertical nudge after positioning - use if the model's pivot isn't at its wheels")]
    public float groundOffset = 0f;

    [Tooltip("Starts moving automatically on Play. Turn off when a mission manager calls BeginMoving() itself.")]
    public bool autoStart = true;

    [Header("Events (lead vehicle only)")]
    public UnityEvent onArrivedAtStop;
    public UnityEvent onDepartedStop;
    public UnityEvent onReachedEnd;

    // Followers compute this live from their leader, so every car in a
    // chain reads consistent values in the same frame (no gap jitter).
    public float DistanceTravelled =>
        leader != null ? Mathf.Max(0f, leader.DistanceTravelled - followGapDistance) : ownDistance;

    public float CurrentSpeed => currentSpeed;
    public bool IsWaitingAtStop => leader != null ? leader.IsWaitingAtStop : state == State.PausedAtStop;

    private State state = State.DrivingToStop;
    private float ownDistance;
    private float currentSpeed;
    private float stopKnotDistance;
    private float endKnotDistance;
    private float worldToLocal = 1f;
    private bool initialized;
    private bool loggedGroundMiss;
    private bool moving;
    private Coroutine resumeRoutine;

    private void Start()
    {
        Init();
        if (autoStart) BeginMoving();
    }

    private void Init()
    {
        if (initialized || spline == null) return;
        initialized = true;

        // Distances here are world units; the spline's own math runs in
        // its local units. The ratio keeps both consistent even if the
        // SplineContainer is scaled.
        float worldLength = spline.CalculateLength();
        worldToLocal = worldLength > 0f ? spline.Spline.GetLength() / worldLength : 1f;

        stopKnotDistance = KnotDistance(stopKnotIndex);
        endKnotDistance = KnotDistance(endKnotIndex);
    }

    public void BeginMoving()
    {
        moving = true;
    }

    // Leaves the stop point (or skips it, if still approaching).
    // Safe to call on any car - followers pass it to their leader.
    public void Depart()
    {
        if (leader != null) { leader.Depart(); return; }
        if (state != State.DrivingToStop && state != State.PausedAtStop) return;

        if (resumeRoutine != null) { StopCoroutine(resumeRoutine); resumeRoutine = null; }
        moving = true;
        state = State.DrivingToEnd;
        Debug.Log($"{name} departing.");
        onDepartedStop.Invoke();
    }

    // Immediate, permanent stop wherever the vehicle is (e.g. EMP).
    // Call on the leader - followers mirror it automatically.
    public void ForceStop()
    {
        if (resumeRoutine != null) { StopCoroutine(resumeRoutine); resumeRoutine = null; }
        currentSpeed = 0f;
        state = State.StoppedAtEnd;
        Debug.Log($"{name} disabled and stopped.");
    }

    // Distance along the spline of the point nearest to a world position.
    public float DistanceAtWorldPoint(Vector3 worldPoint)
    {
        Init();
        float3 local = spline.transform.InverseTransformPoint(worldPoint);
        SplineUtility.GetNearestPoint(spline.Spline, local, out _, out float t);
        return spline.Spline.ConvertIndexUnit(t, PathIndexUnit.Normalized, PathIndexUnit.Distance) / worldToLocal;
    }

    private void Update()
    {
        if (spline == null) return;
        if (leader == null && moving) UpdateOwnMovement();
    }

    // Positioning happens in LateUpdate, after every leader has moved
    // this frame, so coupled cars never lag one frame behind.
    private void LateUpdate()
    {
        if (spline == null) return;

        float t = DistanceToT(DistanceTravelled);
        Vector3 pos = spline.EvaluatePosition(t);
        pos = snapToGround ? SnapToGround(pos) : pos + Vector3.up * groundOffset;

        if (faceMoveDirection)
        {
            // Faces the direction actually travelled this frame - can't
            // drift behind a curve on bumpy ground.
            Vector3 moveDelta = pos - transform.position;
            transform.position = pos;

            if (moveDelta.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(moveDelta.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeedDegrees * Time.deltaTime);
            }
        }
        else
        {
            transform.position = pos;
            Vector3 tangent = spline.EvaluateTangent(t);
            if (tangent.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            }
        }
    }

    private void UpdateOwnMovement()
    {
        float targetSpeed = speed;

        if (state == State.DrivingToStop)
        {
            float distToStop = stopKnotDistance - ownDistance;
            if (distToStop <= brakingDistance)
            {
                // Scales down WITH the remaining distance, so it can never
                // hit zero speed before actually arriving.
                targetSpeed = speed * Mathf.Clamp01(distToStop / brakingDistance);
            }
        }
        else if (state == State.DrivingToEnd)
        {
            float distToEnd = endKnotDistance - ownDistance;
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
        ownDistance += currentSpeed * Time.deltaTime;

        // Snap firmly once very close, instead of crawling forever
        if (state == State.DrivingToStop && ownDistance >= stopKnotDistance - 0.05f)
        {
            ownDistance = stopKnotDistance;
            currentSpeed = 0f;
            state = State.PausedAtStop;
            Debug.Log($"{name} arrived at stop point.");
            onArrivedAtStop.Invoke();

            if (stopRelease == StopRelease.Timed)
            {
                resumeRoutine = StartCoroutine(ResumeAfterStop());
            }
        }
        else if (state == State.DrivingToEnd && ownDistance >= endKnotDistance - 0.05f)
        {
            ownDistance = endKnotDistance;
            currentSpeed = 0f;
            state = State.StoppedAtEnd;
            Debug.Log($"{name} reached the end and stopped for good.");
            onReachedEnd.Invoke();
        }
    }

    private IEnumerator ResumeAfterStop()
    {
        yield return new WaitForSeconds(stopDurationSeconds);
        resumeRoutine = null;
        Depart();
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
            Debug.LogWarning($"{name}: ground raycast found nothing near {pos} - the gap between the spline's height and the ground may exceed Ground Search Height, or the Ground layer doesn't cover this spot.");
        }
        return pos;
    }

    private float KnotDistance(int knotIndex)
    {
        return spline.Spline.ConvertIndexUnit(knotIndex, PathIndexUnit.Knot, PathIndexUnit.Distance) / worldToLocal;
    }

    // Converts real distance to spline t, so speed and car gaps stay
    // uniform even where knots are unevenly spaced.
    private float DistanceToT(float distance)
    {
        return spline.Spline.ConvertIndexUnit(distance * worldToLocal, PathIndexUnit.Distance, PathIndexUnit.Normalized);
    }
}