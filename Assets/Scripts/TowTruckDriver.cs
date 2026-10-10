using System;
using UnityEngine;
using UnityEngine.Splines;

// Put this on the ROOT of the tow truck (truck model + dummy cars on its
// bed as children). It stays hidden until a TowPhone calls it, then
// drives its spline from start to end, eases to a stop, and reports
// back. Self-contained - doesn't touch your other spline scripts.
public class TowTruckDriver : MonoBehaviour
{
    [Serializable]
    public class CarDummy
    {
        [Tooltip("The player's real car (its VehicleInteraction)")]
        public VehicleInteraction car;
        [Tooltip("The look-alike, visual-only copy sitting on the truck's bed")]
        public GameObject dummy;
    }

    [SerializeField] private SplineContainer spline;
    [SerializeField] private float speed = 12f;
    [SerializeField] private float acceleration = 6f;
    [Tooltip("Starts slowing down this far before the end")]
    [SerializeField] private float brakingDistance = 15f;

    [Header("Height")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Nudge up/down if the wheels float or sink")]
    [SerializeField] private float groundOffset = 0f;

    [Header("Steering")]
    [Tooltip("How many seconds BEFORE a corner the front wheels start turning in (needs a WheelSpinner with Steer Wheels)")]
    [SerializeField] private float steerLeadTime = 1.5f;

    [Header("Cars this truck can carry")]
    [Tooltip("ONLY cars listed here can be towed (keeps mission cars like the taxi out of it)")]
    [SerializeField] private CarDummy[] carDummies;

    private float distance;
    private float splineLength;
    private float worldToLocal = 1f;
    private float currentSpeed;
    private bool driving;
    private Action onArrived;

    // True while the truck is out on a delivery
    public bool IsBusy => driving || gameObject.activeSelf && onArrived != null && !arrivedHandled;

    private bool arrivedHandled = true;

    public bool CanTow(VehicleInteraction car)
    {
        if (carDummies == null) return false;
        foreach (CarDummy d in carDummies) if (d.car == car) return true;
        return false;
    }

    private bool initialized;

    // Steered like a real truck: the front axle follows the spline and the
    // rear axle is pulled along behind it (cutting slightly inside corners),
    // so the back never slides sideways.
    private WheelSpinner wheelSpinner;
    private bool axleSteering;
    private float rearAxleZ;   // rear axle, metres in front of the pivot
    private float wheelbase;   // rear axle -> front axle
    private Vector3 rearPoint;
    private bool rearPlaced;

    private void Init()
    {
        if (initialized || spline == null) return;
        initialized = true;
        splineLength = spline.CalculateLength();
        float localLength = spline.Spline.GetLength();
        worldToLocal = splineLength > 0f ? localLength / splineLength : 1f;

        wheelSpinner = GetComponent<WheelSpinner>();
        if (wheelSpinner != null)
        {
            wheelSpinner.Setup();
            axleSteering = wheelSpinner.HasSteering && wheelSpinner.FrontAxleZ - wheelSpinner.RearAxleZ > 0.5f;
            rearAxleZ = wheelSpinner.RearAxleZ;
            wheelbase = wheelSpinner.FrontAxleZ - wheelSpinner.RearAxleZ;
        }
    }

    private void Awake()
    {
        Init();
        if (!driving) gameObject.SetActive(false); // hidden until called
    }

    public void Drive(VehicleInteraction forCar, Action arrived)
    {
        Init();
        driving = true; // set BEFORE activating, so Awake doesn't hide us again

        // Show only the dummy that matches the car being delivered
        if (carDummies != null)
            foreach (CarDummy d in carDummies)
                if (d.dummy != null) d.dummy.SetActive(d.car == forCar);

        distance = 0f;
        currentSpeed = 0f;
        rearPlaced = false;
        onArrived = arrived;
        arrivedHandled = false;
        gameObject.SetActive(true);
        Place();
    }

    public void Hide()
    {
        driving = false;
        arrivedHandled = true;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!driving || spline == null) return;

        float remaining = splineLength - distance;
        float target = remaining <= brakingDistance
            ? Mathf.Max(1f, speed * remaining / brakingDistance) // ease in, never fully stall
            : speed;

        currentSpeed = Mathf.MoveTowards(currentSpeed, target, acceleration * Time.deltaTime);
        distance += currentSpeed * Time.deltaTime;

        if (distance >= splineLength - 0.05f)
        {
            distance = splineLength;
            driving = false;
            Place();
            onArrived?.Invoke();
            return;
        }

        Place();
    }

    private void Place()
    {
        if (axleSteering)
        {
            PlaceOnAxles();
            return;
        }

        float t = spline.Spline.ConvertIndexUnit(distance * worldToLocal, PathIndexUnit.Distance, PathIndexUnit.Normalized);
        Vector3 pos = spline.EvaluatePosition(t);
        Vector3 tangent = spline.EvaluateTangent(t);

        if (snapToGround) pos.y = GroundHeight(pos);
        transform.position = pos + Vector3.up * groundOffset;

        tangent.y = 0f;
        if (tangent.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
    }

    private void PlaceOnAxles()
    {
        // 'distance' is still where the pivot would be on a straight road,
        // so start/stop spots are the same as before
        float frontAt = distance + rearAxleZ + wheelbase;
        Vector3 front = PointAt(frontAt);

        // Rear axle starts on the spline, then gets dragged behind the front one
        if (!rearPlaced)
        {
            rearPoint = PointAt(frontAt - wheelbase);
            rearPlaced = true;
        }

        Vector3 forward = front - rearPoint;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = DirectionAt(frontAt);
        forward.Normalize();
        rearPoint = front - forward * wheelbase;

        Vector3 pos = rearPoint - forward * rearAxleZ;
        if (snapToGround) pos.y = GroundHeight(pos);
        transform.position = pos + Vector3.up * groundOffset;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        // Front wheels: turn in steerLeadTime seconds before a bend, and stay
        // turned until the front axle is through it (whichever bend is sharper)
        // 'now' = the angle the front wheels really need to follow the road
        float needed = Vector3.SignedAngle(forward, DirectionAt(frontAt), Vector3.up);
        float now = Mathf.Tan(Mathf.Clamp(needed, -80f, 80f) * Mathf.Deg2Rad) / wheelbase;
        float ahead = CurvatureAt(frontAt + currentSpeed * steerLeadTime);
        wheelSpinner.SetSteerCurvature(Mathf.Abs(ahead) > Mathf.Abs(now) ? ahead : now);
    }

    // How sharply the spline bends here: 1 / turning radius, positive = right
    private float CurvatureAt(float d)
    {
        const float window = 2f;
        float yaw = Vector3.SignedAngle(DirectionAt(d - window), DirectionAt(d + window), Vector3.up);
        return yaw * Mathf.Deg2Rad / (2f * window);
    }

    // World point at this many metres along the spline. Past either end it
    // carries on in a straight line, so the truck can overhang the ends.
    private Vector3 PointAt(float d)
    {
        float clamped = Mathf.Clamp(d, 0f, splineLength);
        float t = spline.Spline.ConvertIndexUnit(clamped * worldToLocal, PathIndexUnit.Distance, PathIndexUnit.Normalized);
        Vector3 p = spline.EvaluatePosition(t);
        if (d != clamped) p += DirectionAt(clamped) * (d - clamped);
        return p;
    }

    // Flat (no up/down) travel direction at this many metres along the spline
    private Vector3 DirectionAt(float d)
    {
        d = Mathf.Clamp(d, 0f, splineLength);
        float t = spline.Spline.ConvertIndexUnit(d * worldToLocal, PathIndexUnit.Distance, PathIndexUnit.Normalized);
        Vector3 tangent = spline.EvaluateTangent(t);
        tangent.y = 0f;
        return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : transform.forward;
    }

    private float GroundHeight(Vector3 pos)
    {
        float best = pos.y;
        float closest = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(pos + Vector3.up * 30f, Vector3.down, 60f, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform)) continue; // ignore the truck itself
            if (hit.distance < closest) { closest = hit.distance; best = hit.point.y; }
        }
        return best;
    }
}