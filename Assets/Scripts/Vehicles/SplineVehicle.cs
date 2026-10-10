using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("Max turn rate when Snap To Ground is off. Ground vehicles steer with Driving Feel instead.")]
    public float turnSpeedDegrees = 180f;

    [Header("Height")]
    [Tooltip("On: raycasts down to the ground (road vehicles). Off: sits exactly on the spline (trains - the spline IS the rails).")]
    public bool snapToGround = true;
    public LayerMask groundMask = ~0;
    [Tooltip("Optional: parent of the road meshes (e.g. 'Roads'). Their colliders count as ground too, whatever their layer, so vehicles drive ON the road instead of the terrain hidden under it.")]
    public Transform roadSurfaces;
    public float groundSearchHeight = 50f;
    [Tooltip("Vertical nudge after positioning - use if the model's pivot isn't at its wheels")]
    public float groundOffset = 0f;
    [Tooltip("Snap To Ground only: how far AHEAD of the pivot the front wheels are, along the spline. Ground is sampled there.")]
    public float groundSampleAhead = 2.5f;
    [Tooltip("Snap To Ground only: how far BEHIND the pivot the rear wheels are. A trailer whose pivot is at its hitch uses ~0 ahead and its length behind.")]
    public float groundSampleBehind = 2.5f;
    [Tooltip("Seconds to ease height and tilt toward the ground - filters out terrain bumps so the vehicle doesn't bob. Keep small for fast vehicles or they'll lag into slopes.")]
    public float groundSmoothTime = 0.1f;

    [Header("Driving Feel (Snap To Ground only)")]
    [Tooltip("Rounds sharp spline corners into smooth arcs, roughly this many metres either side of each corner. 0 = follow the spline exactly.")]
    public float cornerRounding = 6f;
    [Tooltip("Seconds to ease the steering - softens the start and end of each turn. Small values (~0.1) avoid the vehicle visibly sliding behind the road.")]
    public float steeringSmoothTime = 0.1f;
    [Tooltip("Degrees the body leans outward per m/s² of cornering force. 0 = no lean.")]
    public float bodyRoll = 0.8f;
    public float maxBodyRoll = 4f;
    [Tooltip("How many seconds BEFORE a bend the front wheels start turning in (needs a WheelSpinner with Steer Wheels on this vehicle)")]
    public float steerLeadTime = 1.5f;
    [Tooltip("Trailers: hangs off the leader's hitch (Follow Gap Distance behind its pivot) and is dragged like a real trailer, pivoting through turns. Leave off for separate vehicles driving in a convoy.")]
    public bool hitchedToLeader;

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

    // Followers don't track their own speed - the convoy moves at the leader's
    private float ConvoySpeed => leader != null ? leader.ConvoySpeed : currentSpeed;
    public bool IsWaitingAtStop => leader != null ? leader.IsWaitingAtStop : state == State.PausedAtStop;

    private State state = State.DrivingToStop;
    private float ownDistance;
    private float currentSpeed;
    private float stopKnotDistance;
    private float endKnotDistance;
    private float worldToLocal = 1f;
    private float worldLength;
    private bool initialized;
    private bool loggedGroundMiss;

    // Ground-follow state (Snap To Ground)
    private bool groundPlaced;
    private float smoothY, smoothYVelocity;
    private float smoothPitch, smoothPitchVelocity;
    private float yaw, yawVelocity;
    private float roll, rollVelocity;
    private Vector3 trailerRear;
    private Vector3 prevPosition;
    private float prevYaw;
    private float lastFrontGroundY, lastRearGroundY;
    private bool hasFrontGround, hasRearGround;
    private readonly List<SplineVehicle> trailers = new List<SplineVehicle>();
    private readonly RaycastHit[] groundHits = new RaycastHit[16];

    private bool IsHitchedTrailer => hitchedToLeader && leader != null && snapToGround;
    private WheelSpinner wheelSpinner;
    private bool moving;
    private Coroutine resumeRoutine;

    private void Start()
    {
        Init();
        if (IsHitchedTrailer) leader.trailers.Add(this);
        if (autoStart) BeginMoving();
    }

    private void Init()
    {
        if (initialized || spline == null) return;
        initialized = true;

        // Distances here are world units; the spline's own math runs in
        // its local units. The ratio keeps both consistent even if the
        // SplineContainer is scaled.
        worldLength = spline.CalculateLength();
        worldToLocal = worldLength > 0f ? spline.Spline.GetLength() / worldLength : 1f;

        stopKnotDistance = KnotDistance(stopKnotIndex);
        endKnotDistance = KnotDistance(endKnotIndex);

        wheelSpinner = GetComponent<WheelSpinner>();
        if (wheelSpinner != null)
        {
            wheelSpinner.Setup();
            if (!wheelSpinner.HasSteering) wheelSpinner = null; // spin-only (e.g. trailer) - nothing to steer
        }
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

        if (snapToGround)
        {
            // A hitched trailer is placed by its leader, straight after
            // the leader moves, so it never lags a frame off the hitch.
            if (IsHitchedTrailer) return;
            PlaceOnGround(DistanceTravelled);
            PlaceTrailers();
            return;
        }

        float t = DistanceToT(DistanceTravelled);
        Vector3 pos = (Vector3)spline.EvaluatePosition(t) + Vector3.up * groundOffset;

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

    // Road vehicles: sits on the ground at its front AND rear wheels and
    // tilts to match, instead of balancing on one point under the pivot
    // (which made long vehicles sink into slopes, float over dips and
    // bob on every terrain bump). The spline only steers - its height
    // is ignored, so knots dipping under the terrain don't matter.
    private void PlaceOnGround(float distance)
    {
        Vector3 center = PathPoint(distance);
        Vector3 front = PathPoint(distance + groundSampleAhead);
        Vector3 rear = PathPoint(distance - groundSampleBehind);

        float frontY = GroundHeight(front, ref lastFrontGroundY, ref hasFrontGround);
        float rearY = GroundHeight(rear, ref lastRearGroundY, ref hasRearGround);

        // Heading and tilt come from the line between the wheels - like
        // real steering, the nose swings gradually into a bend instead of
        // snapping to the spline's direction at each point.
        Vector3 flat = front - rear;
        flat.y = 0f;
        float run = flat.magnitude;

        float targetY = rearY + groundOffset;
        float targetPitch = smoothPitch;
        float targetYaw = yaw;
        if (run > 0.01f)
        {
            // Height under the pivot, along the slope between the axles
            float pivotFraction = Mathf.Clamp01(Vector3.Distance(new Vector3(rear.x, 0f, rear.z), new Vector3(center.x, 0f, center.z)) / run);
            targetY = Mathf.Lerp(rearY, frontY, pivotFraction) + groundOffset;
            targetPitch = -Mathf.Atan2(frontY - rearY, run) * Mathf.Rad2Deg;
            targetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        }

        if (!groundPlaced)
        {
            // First frame: drop straight into place, no easing in from (0,0,0)
            smoothY = targetY;
            smoothPitch = targetPitch;
            yaw = targetYaw;
        }
        else
        {
            smoothY = Mathf.SmoothDamp(smoothY, targetY, ref smoothYVelocity, groundSmoothTime);
            smoothPitch = Mathf.SmoothDampAngle(smoothPitch, targetPitch, ref smoothPitchVelocity, groundSmoothTime);
            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, steeringSmoothTime);
        }

        ApplyPose(new Vector3(center.x, smoothY, center.z));
        SteerWheels(distance);
    }

    // Front wheels: turn in steerLeadTime seconds before a bend, and stay
    // turned until the front axle is through it (whichever bend is sharper)
    private void SteerWheels(float distance)
    {
        if (wheelSpinner == null) return;

        float frontAt = distance + wheelSpinner.FrontAxleZ;
        float now = PathCurvature(frontAt);
        float ahead = PathCurvature(frontAt + ConvoySpeed * steerLeadTime);
        wheelSpinner.SetSteerCurvature(Mathf.Abs(ahead) > Mathf.Abs(now) ? ahead : now);
    }

    // How sharply the driven path bends here: 1 / turning radius, positive = right
    private float PathCurvature(float distance)
    {
        const float window = 2f;
        Vector3 a = PathPoint(distance - window);
        Vector3 b = PathPoint(distance);
        Vector3 c = PathPoint(distance + window);
        Vector3 inDir = b - a, outDir = c - b;
        inDir.y = 0f;
        outDir.y = 0f;
        if (inDir.sqrMagnitude < 0.0001f || outDir.sqrMagnitude < 0.0001f) return 0f;
        return Vector3.SignedAngle(inDir, outDir, Vector3.up) * Mathf.Deg2Rad / window;
    }

    // Trailer: the front sits on the leader's hitch; the rear axle is
    // dragged behind it (it only ever moves toward the hitch), which is
    // exactly how a real trailer swings and cuts in through a turn.
    private void PlaceAsTrailer()
    {
        Transform cab = leader.transform;
        Vector3 hitch = cab.position - cab.forward * followGapDistance;

        if (!groundPlaced)
        {
            // Start straight in line behind the cab. (Sampling the path
            // here fails at the spline's start: there's no path behind it,
            // so the rear lands on the cab and the trailer spins sideways.)
            Vector3 back = -cab.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.0001f) back = -Vector3.forward;
            trailerRear = hitch + back.normalized * groundSampleBehind;
        }

        Vector3 toHitch = hitch - trailerRear;
        toHitch.y = 0f;
        if (toHitch.sqrMagnitude > 0.0001f)
        {
            Vector3 dir = toHitch.normalized;
            trailerRear = hitch - dir * groundSampleBehind;
            yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        float rearY = GroundHeight(trailerRear, ref lastRearGroundY, ref hasRearGround) + groundOffset;
        float targetPitch = -Mathf.Atan2(hitch.y - rearY, groundSampleBehind) * Mathf.Rad2Deg;
        smoothPitch = groundPlaced
            ? Mathf.SmoothDampAngle(smoothPitch, targetPitch, ref smoothPitchVelocity, groundSmoothTime)
            : targetPitch;

        ApplyPose(hitch);
    }

    private void PlaceTrailers()
    {
        foreach (SplineVehicle trailer in trailers)
        {
            trailer.PlaceAsTrailer();
            trailer.PlaceTrailers();
        }
    }

    // Sets the final pose, leaning the body outward in proportion to
    // how hard it's cornering (speed x turn rate), eased so it settles.
    private void ApplyPose(Vector3 position)
    {
        float dt = Time.deltaTime;
        if (groundPlaced && dt > 0f)
        {
            Vector3 moved = position - prevPosition;
            moved.y = 0f;
            float speedNow = moved.magnitude / dt;
            float yawRate = Mathf.DeltaAngle(prevYaw, yaw) * Mathf.Deg2Rad / dt;
            float targetRoll = Mathf.Clamp(speedNow * yawRate * bodyRoll, -maxBodyRoll, maxBodyRoll);
            roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVelocity, 0.3f);
        }

        groundPlaced = true;
        prevPosition = position;
        prevYaw = yaw;
        transform.SetPositionAndRotation(position, Quaternion.Euler(smoothPitch, yaw, roll));
    }

    // A point on the path the vehicle actually drives: the spline,
    // averaged over a short window. Straights are unchanged; sharp or
    // linear corners become smooth arcs a real vehicle could drive.
    private Vector3 PathPoint(float distance)
    {
        distance = Mathf.Clamp(distance, 0f, worldLength);
        if (cornerRounding <= 0.01f) return spline.EvaluatePosition(DistanceToT(distance));

        const int samples = 8;
        Vector3 sum = Vector3.zero;
        for (int i = 0; i <= samples; i++)
        {
            float d = distance + Mathf.Lerp(-cornerRounding, cornerRounding, i / (float)samples);
            sum += (Vector3)spline.EvaluatePosition(DistanceToT(Mathf.Clamp(d, 0f, worldLength)));
        }
        return sum / (samples + 1);
    }

    // Ground height below a point. If the ray misses (a hole in the
    // Ground layer), keeps the last height instead of diving to the spline.
    private float GroundHeight(Vector3 point, ref float lastY, ref bool hasLast)
    {
        Vector3 rayOrigin = point + Vector3.up * groundSearchHeight;
        if (roadSurfaces == null)
        {
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundSearchHeight * 2f, groundMask))
            {
                lastY = hit.point.y;
                hasLast = true;
                return lastY;
            }
        }
        else
        {
            // Roads may sit on a different layer than the terrain, so cast
            // against everything and keep the highest hit that's either
            // ground or road - never cars, people or the vehicle itself.
            int count = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, groundHits, groundSearchHeight * 2f, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            float bestY = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                Collider col = groundHits[i].collider;
                bool isGround = (groundMask.value & (1 << col.gameObject.layer)) != 0;
                if (!isGround && !col.transform.IsChildOf(roadSurfaces)) continue;
                if (groundHits[i].point.y > bestY) { bestY = groundHits[i].point.y; found = true; }
            }
            if (found)
            {
                lastY = bestY;
                hasLast = true;
                return lastY;
            }
        }

        if (!loggedGroundMiss)
        {
            loggedGroundMiss = true;
            Debug.LogWarning($"{name}: ground raycast found nothing near {point} - the gap between the spline's height and the ground may exceed Ground Search Height, or the Ground layer doesn't cover this spot.");
        }
        return hasLast ? lastY : point.y;
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