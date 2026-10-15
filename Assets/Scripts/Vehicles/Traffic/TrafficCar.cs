using System;
using System.Collections.Generic;
using UnityEngine;

// One ambient traffic car. Added at runtime by TrafficSpawner to a plain
// car model - no setup needed on the model itself.
//
// Drives like the tow truck: the front axle follows the lane and the rear
// axle is dragged behind it, so it turns like a real car (no sliding) and
// the front wheels steer into bends a moment early.
// Slows for bends, keeps its distance to the car ahead, stops for the
// player (on foot or driving) and waits its turn at crossings.
public class TrafficCar : MonoBehaviour
{
    public static readonly List<TrafficCar> All = new List<TrafficCar>();

    // Tuning (the spawner copies its values in)
    [NonSerialized] public float cruiseSpeed = 11f;       // m/s (11 = ~40 km/h)
    [NonSerialized] public float acceleration = 3f;
    [NonSerialized] public float braking = 6f;
    [NonSerialized] public float cornerGrip = 2.5f;       // sideways m/s² allowed in bends - lower = slower corners
    [NonSerialized] public float stopGap = 3f;            // metres left free in front when queueing
    [NonSerialized] public float steerLeadTime = 1.2f;
    [NonSerialized] public float patience = 6f;           // seconds stuck behind another traffic car before squeezing past

    public event Action<TrafficCar> Finished;

    public TrafficPath Path { get; private set; }
    public float Distance => distance;
    public float Speed => currentSpeed;
    public float HalfWidth { get; private set; } = 1f;
    public float FrontZ { get; private set; } = 2f;   // front bumper, metres ahead of the pivot
    public float RearZ { get; private set; } = -2f;   // rear bumper (negative)
    public UnityEngine.Object BlockedBy { get; private set; }

    private float distance;
    private float currentSpeed;
    private float groundOffset;
    private float stuckTime;
    private float ignoreTrafficUntil;

    private WheelSpinner wheelSpinner;
    private CarLights lights;
    private bool axleSteering;
    private float rearAxleZ, wheelbase;
    private Vector3 rearPoint;
    private bool rearPlaced;

    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    // ================= setup =================

    // Turns a plain car model into a traffic car. Call once, right after Instantiate.
    public void Setup(TrafficPath path, float startDistance)
    {
        StripPhysics();
        Measure();
        SetupWheels();
        lights = GetComponentInChildren<CarLights>();

        Path = path;
        distance = startDistance;
        currentSpeed = startDistance > 0f ? cruiseSpeed * 0.7f : cruiseSpeed * 0.5f;
        rearPlaced = false;
        Place();
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    // Imported cars can come with a driveable physics setup - make it a
    // kinematic body that still blocks (and nudges) the player
    private void StripPhysics()
    {
        foreach (WheelCollider wc in GetComponentsInChildren<WheelCollider>(true)) DestroyImmediate(wc);
        foreach (Rigidbody extra in GetComponentsInChildren<Rigidbody>(true))
            if (extra.gameObject != gameObject) DestroyImmediate(extra);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
    }

    // Size and ground offset, read from the model while it's still upright at the origin
    private void Measure()
    {
        Quaternion rot = transform.rotation;
        Vector3 pos = transform.position;
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (any)
        {
            groundOffset = -b.min.y;          // pivot height above the lowest point (tyres)
            HalfWidth = b.extents.x;
            FrontZ = b.max.z;
            RearZ = b.min.z;
        }

        transform.SetPositionAndRotation(pos, rot);
    }

    // Finds the wheel meshes by name and hands them to a WheelSpinner
    private void SetupWheels()
    {
        var all = new List<Transform>();
        var front = new List<Transform>();
        foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>(true))
        {
            if (!IsWheelName(mf.name)) continue;
            all.Add(mf.transform);
            if (transform.InverseTransformPoint(mf.transform.position).z > 0f) front.Add(mf.transform);
        }

        if (all.Count >= 3 && front.Count > 0 && front.Count < all.Count)
        {
            wheelSpinner = GetComponent<WheelSpinner>();
            if (wheelSpinner == null) wheelSpinner = gameObject.AddComponent<WheelSpinner>();
            wheelSpinner.Configure(all.ToArray(), front.ToArray());
            axleSteering = wheelSpinner.HasSteering && wheelSpinner.FrontAxleZ - wheelSpinner.RearAxleZ > 0.5f;
            rearAxleZ = wheelSpinner.RearAxleZ;
            wheelbase = wheelSpinner.FrontAxleZ - wheelSpinner.RearAxleZ;
        }

        if (!axleSteering)
        {
            // No usable wheels: steer from an imaginary axle pair
            rearAxleZ = RearZ * 0.6f;
            wheelbase = (FrontZ - RearZ) * 0.6f;
            axleSteering = wheelbase > 0.5f;
        }
    }

    private static bool IsWheelName(string n)
    {
        n = n.ToLowerInvariant();
        if (n.Contains("steer")) return false; // steering wheel inside the car
        if (n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre")) return true;
        return n == "fl" || n == "fr" || n == "bl" || n == "br" || n == "rl" || n == "rr";
    }

    // ================= driving =================

    private void Update()
    {
        if (Path == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float target = Mathf.Min(cruiseSpeed, BendSpeed());

        ScanForObstacles();
        float free = FreeDistanceAhead(out UnityEngine.Object blocker);
        BlockedBy = blocker;
        if (blocker != null)
        {
            float room = Mathf.Max(0f, free - stopGap);
            target = Mathf.Min(target, Mathf.Sqrt(2f * braking * room));
            if (room < 0.5f) target = 0f;
        }

        // Stuck behind another traffic car or a parked car for too long (e.g.
        // two cars blocking each other at a crossing) - squeeze through slowly.
        // Never pushes through the player or the car they're driving.
        bool player = blocker != null && (blocker == VehicleInteraction.Current || blocker is Transform);
        if (blocker != null && !player && currentSpeed < 0.5f) stuckTime += dt;
        else stuckTime = 0f;
        if (stuckTime > patience)
        {
            ignoreTrafficUntil = Time.time + 3f;
            stuckTime = 0f;
        }

        float rate = target < currentSpeed ? braking * 1.5f : acceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * dt);
        distance += currentSpeed * dt;

        if (distance - RearZ >= Path.Length) // fully past the end of the route
        {
            Finished?.Invoke(this);
            Destroy(gameObject);
            return;
        }

        Place();
        UpdateSignal();
    }

    // Indicator on while the road just ahead turns sharply (a junction or corner),
    // off again once the car is through it. Gentle bends don't count.
    private void UpdateSignal()
    {
        if (lights == null) return;
        float front = distance + FrontZ;
        float yaw = Vector3.SignedAngle(Path.DirectionAt(front - 3f), Path.DirectionAt(Mathf.Min(front + 25f, Path.Length)), Vector3.up);
        lights.SetSignal(Mathf.Abs(yaw) > 30f ? (yaw > 0f ? 1 : -1) : 0); // positive = turning right
    }

    // Slow enough now to make every bend in the next stretch of road
    private float BendSpeed()
    {
        float best = float.MaxValue;
        float front = distance + FrontZ;
        float look = 8f + currentSpeed * currentSpeed / (2f * braking);
        for (float a = 0f; a <= look; a += 3f)
        {
            float k = Mathf.Abs(Path.CurvatureAt(front + a));
            if (k < 0.002f) continue;
            float v = Mathf.Sqrt(cornerGrip / k);
            best = Mathf.Min(best, Mathf.Sqrt(v * v + 2f * braking * a));
        }
        return Mathf.Max(3f, best);
    }

    // Metres of free lane in front of the bumper, and what's blocking it (null = clear)
    private float FreeDistanceAhead(out UnityEngine.Object blocker)
    {
        blocker = null;
        float look = 6f + stopGap + currentSpeed * currentSpeed / (2f * braking);
        float front = distance + FrontZ;

        Transform player = PlayerTransform();
        VehicleInteraction driven = VehicleInteraction.Current;
        bool checkTraffic = Time.time >= ignoreTrafficUntil;

        for (float a = 0f; a <= look; a += 1.5f)
        {
            Vector3 p = Path.PointAt(front + a);

            if (checkTraffic)
            {
                foreach (TrafficCar other in All)
                {
                    if (other == this || other.Path == null) continue;
                    if (other.Overlaps(p, HalfWidth * 0.8f) && !GivesWayTo(other))
                    {
                        blocker = other;
                        return a;
                    }
                }
            }

            if (driven != null && driven.IsDriving && NearCar(driven.transform, p, 2.6f))
            {
                blocker = driven;
                return a;
            }
            if (player != null && driven == null && FlatDistance(player.position, p) < HalfWidth + 0.8f)
            {
                blocker = player;
                return a;
            }
        }

        // Anything else in the lane: parked cars, the tow truck, mission trucks
        if (checkTraffic && obstacleDistance < look)
        {
            blocker = obstacle;
            return obstacleDistance;
        }
        return float.MaxValue;
    }

    // Physics check for other vehicles - a few times a second is plenty
    private float obstacleDistance = float.MaxValue;
    private UnityEngine.Object obstacle;
    private float nextObstacleScan;
    private static readonly Collider[] NearbyColliders = new Collider[16];

    private void ScanForObstacles()
    {
        if (Time.time < nextObstacleScan) return;
        nextObstacleScan = Time.time + 0.2f;
        obstacleDistance = float.MaxValue;
        obstacle = null;

        float look = 6f + stopGap + currentSpeed * currentSpeed / (2f * braking);
        float front = distance + FrontZ;
        for (float a = 0f; a <= look; a += 2.5f)
        {
            Vector3 p = Path.PointAt(front + a) + Vector3.up * 1f;
            int n = Physics.OverlapSphereNonAlloc(p, HalfWidth * 0.9f, NearbyColliders, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = NearbyColliders[i];
                if (c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<TrafficCar>() != null) continue; // handled above
                bool vehicle = c.attachedRigidbody != null
                               || c.GetComponentInParent<TowTruckDriver>() != null
                               || c.GetComponentInParent<SplineVehicle>() != null;
                if (!vehicle || c.GetComponentInParent<VehicleInteraction>() == VehicleInteraction.Current && VehicleInteraction.Current != null) continue;
                if (c.attachedRigidbody != null && c.attachedRigidbody.CompareTag("Player")) continue; // player on foot: checked above
                obstacleDistance = a;
                obstacle = c.attachedRigidbody != null ? (UnityEngine.Object)c.attachedRigidbody.gameObject : c.gameObject;
                return;
            }
        }
    }

    // Two cars that see each other in their way (a crossing): one of them goes
    private bool GivesWayTo(TrafficCar other)
    {
        return other.BlockedBy == this && GetInstanceID() < other.GetInstanceID();
    }

    // Is this point (plus a margin) on top of this car?
    public bool Overlaps(Vector3 point, float margin)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        return Mathf.Abs(local.x) < HalfWidth + margin && local.z > RearZ - margin && local.z < FrontZ + margin;
    }

    private static bool NearCar(Transform car, Vector3 point, float halfLength)
    {
        Vector3 local = car.InverseTransformPoint(point);
        return Mathf.Abs(local.x) < 2.2f && Mathf.Abs(local.z) < halfLength + 1f;
    }

    private static Transform cachedPlayer;
    private static Transform PlayerTransform()
    {
        if (cachedPlayer == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            cachedPlayer = p != null ? p.transform : null;
        }
        return cachedPlayer;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y;
        return Vector3.Distance(a, b);
    }

    // ================= placing on the road =================

    private void Place()
    {
        float frontAt = distance + rearAxleZ + wheelbase;
        Vector3 front = Path.PointAt(frontAt);

        // Rear axle starts on the lane, then gets dragged behind the front one
        if (!rearPlaced)
        {
            rearPoint = Path.PointAt(frontAt - wheelbase);
            rearPlaced = true;
        }

        Vector3 forward = front - rearPoint;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) forward = Path.DirectionAt(frontAt);
        forward.Normalize();
        rearPoint = front - forward * wheelbase;
        rearPoint.y = front.y;

        // Height + pitch from the road under each axle
        float frontY = GroundHeight(front);
        float rearY = GroundHeight(rearPoint);
        Vector3 frontG = new Vector3(front.x, frontY, front.z);
        Vector3 rearG = new Vector3(rearPoint.x, rearY, rearPoint.z);
        Vector3 axis = (frontG - rearG).normalized;

        Vector3 pos = rearG - axis * rearAxleZ;
        Quaternion rot = Quaternion.LookRotation(axis, Vector3.up);
        transform.SetPositionAndRotation(pos + rot * Vector3.up * groundOffset, rot);

        if (wheelSpinner != null && wheelSpinner.HasSteering)
        {
            // Front wheels: turn in steerLeadTime seconds before a bend
            float needed = Vector3.SignedAngle(forward, Path.DirectionAt(frontAt), Vector3.up);
            float now = Mathf.Tan(Mathf.Clamp(needed, -80f, 80f) * Mathf.Deg2Rad) / wheelbase;
            float ahead = Path.CurvatureAt(frontAt + currentSpeed * steerLeadTime);
            wheelSpinner.SetSteerCurvature(Mathf.Abs(ahead) > Mathf.Abs(now) ? ahead : now);
        }
    }

    // Road (or terrain) height under this point; ignores cars and triggers
    private float GroundHeight(Vector3 lanePoint)
    {
        Vector3 origin = lanePoint + Vector3.up * 4f;
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = lanePoint.y;
        float closest = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            Collider c = Hits[i].collider;
            if (c.attachedRigidbody != null) continue; // cars, props that move
            if (Hits[i].distance < closest)
            {
                closest = Hits[i].distance;
                best = Hits[i].point.y;
            }
        }
        return best;
    }
}
