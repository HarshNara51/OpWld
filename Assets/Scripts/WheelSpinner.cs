using UnityEngine;

// Spins wheel meshes based on how far this object actually moved - for
// script-driven vehicles (tow truck, trains, trucks on splines) that
// don't use physics. No wheel colliders or Rigidbody needed.
// Put it on the vehicle's ROOT and drag the wheel meshes into the list.
//
// The spin axis and wheel size are worked out automatically, so it
// doesn't matter how the wheels were rotated/scaled on import (Blender
// models often come in rotated 90 degrees with a 100x scale).
// Optional: wheels in "Steer Wheels" also turn left/right into corners.
public class WheelSpinner : MonoBehaviour
{
    [Tooltip("ALL the wheel mesh objects (their pivot should be at the wheel's center)")]
    [SerializeField] private Transform[] wheels;

    [Tooltip("Wheels that also turn into corners (usually the front ones). They must ALSO be in the Wheels list.")]
    [SerializeField] private Transform[] steerWheels;

    [Tooltip("Measure each wheel's radius from its mesh. Turn off to use Wheel Radius below.")]
    [SerializeField] private bool autoRadius = true;

    [Tooltip("Wheel radius in metres (only used when Auto Radius is off) - bigger wheels spin slower")]
    [SerializeField] private float wheelRadius = 0.5f;

    [Tooltip("Tick this if the wheels roll backwards")]
    [SerializeField] private bool reverseSpin = false;

    [Header("Steering")]
    [SerializeField] private float maxSteerAngle = 35f;
    [Tooltip("Higher = wheels snap to the new angle faster")]
    [SerializeField] private float steerResponsiveness = 6f;

    private Quaternion[] restRotation;
    private Vector3[] localAxle;      // each wheel's axle, in its own local space
    private Vector3[] parentUp;       // vehicle's up, in each wheel's parent space
    private float[] radius;
    private float[] spin;
    private float[] steerLever;       // how far each steering wheel sits ahead of the rear axle(s)
    private bool[] steers;

    private Vector3 lastPosition;
    private Vector3 lastForward;
    private float steerCurvature;     // 1/turning radius, smoothed
    private float externalCurvature;
    private int externalFrame = -1;
    private bool ready;

    // Middle of the rear (non-steering) and front (steering) axles, in metres in front of the pivot
    public float RearAxleZ { get; private set; }
    public float FrontAxleZ { get; private set; }
    public bool HasSteering { get; private set; }

    // A driver script that knows the road ahead (e.g. TowTruckDriver) can call
    // this every frame to steer the wheels early, instead of reacting to movement.
    // curvature = 1 / turning radius in metres, positive = turning right.
    public void SetSteerCurvature(float curvature)
    {
        externalCurvature = curvature;
        externalFrame = Time.frameCount;
    }

    private void Awake()
    {
        Setup();
    }

    public void Setup()
    {
        if (ready || wheels == null) return;
        ready = true;

        int n = wheels.Length;
        restRotation = new Quaternion[n];
        localAxle = new Vector3[n];
        parentUp = new Vector3[n];
        radius = new float[n];
        spin = new float[n];
        steerLever = new float[n];
        steers = new bool[n];

        float rearZ = 0f;
        int rearCount = 0;

        for (int i = 0; i < n; i++)
        {
            Transform w = wheels[i];
            if (w == null) continue;

            restRotation[i] = w.localRotation;
            localAxle[i] = w.InverseTransformDirection(transform.right).normalized;
            parentUp[i] = w.parent != null ? w.parent.InverseTransformDirection(transform.up).normalized : transform.up;
            radius[i] = autoRadius ? MeasureRadius(w, localAxle[i]) : wheelRadius;
            steers[i] = System.Array.IndexOf(steerWheels ?? new Transform[0], w) >= 0;

            if (!steers[i])
            {
                rearZ += MetresAhead(w);
                rearCount++;
            }
        }

        if (rearCount > 0) rearZ /= rearCount;
        float frontZ = 0f;
        int frontCount = 0;
        for (int i = 0; i < n; i++)
            if (wheels[i] != null && steers[i])
            {
                float z = MetresAhead(wheels[i]);
                steerLever[i] = z - rearZ;
                frontZ += z;
                frontCount++;
            }

        RearAxleZ = rearZ;
        FrontAxleZ = frontCount > 0 ? frontZ / frontCount : rearZ;
        HasSteering = frontCount > 0 && rearCount > 0;
    }

    // How far in front of the pivot a wheel is, in real metres (even on a scaled vehicle)
    private float MetresAhead(Transform w)
    {
        return Vector3.Dot(w.position - transform.position, transform.forward);
    }

    // Half the wheel's size across the two directions that aren't the axle
    private float MeasureRadius(Transform w, Vector3 axle)
    {
        MeshFilter mf = w.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return wheelRadius;

        Vector3 size = Vector3.Scale(mf.sharedMesh.bounds.size, w.lossyScale);
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        Vector3 a = new Vector3(Mathf.Abs(axle.x), Mathf.Abs(axle.y), Mathf.Abs(axle.z));

        // Drop whichever axis the axle points along, keep the bigger of the other two
        float r;
        if (a.x >= a.y && a.x >= a.z) r = Mathf.Max(size.y, size.z);
        else if (a.y >= a.z) r = Mathf.Max(size.x, size.z);
        else r = Mathf.Max(size.x, size.y);

        return r > 0.01f ? r * 0.5f : wheelRadius;
    }

    private void OnEnable()
    {
        Setup();
        lastPosition = transform.position;
        lastForward = transform.forward;
    }

    private void LateUpdate()
    {
        if (!ready) return;

        Vector3 delta = transform.position - lastPosition;
        Vector3 forward = transform.forward;
        lastPosition = transform.position;

        // Teleported (e.g. tow truck placed at its spline start) - don't spin like mad
        if (delta.sqrMagnitude > 25f)
        {
            lastForward = forward;
            return;
        }

        // Forward/backward distance only (ignores sliding sideways)
        float distance = Vector3.Dot(delta, forward);

        float blend = 1f - Mathf.Exp(-steerResponsiveness * Time.deltaTime);
        if (externalFrame == Time.frameCount)
        {
            // Driver told us how the road ahead bends
            steerCurvature = Mathf.Lerp(steerCurvature, externalCurvature, blend);
        }
        else if (Mathf.Abs(distance) > 0.001f)
        {
            // Otherwise: how sharply we're turning = heading change per metre travelled
            float yaw = Vector3.SignedAngle(Flat(lastForward), Flat(forward), Vector3.up) * Mathf.Deg2Rad;
            steerCurvature = Mathf.Lerp(steerCurvature, yaw / distance, blend);
        }
        lastForward = forward;

        float direction = reverseSpin ? -1f : 1f;

        for (int i = 0; i < wheels.Length; i++)
        {
            Transform w = wheels[i];
            if (w == null) continue;

            spin[i] = Mathf.Repeat(spin[i] + direction * distance / radius[i] * Mathf.Rad2Deg, 360f);

            Quaternion steer = Quaternion.identity;
            if (steers[i])
            {
                float angle = Mathf.Atan(steerLever[i] * steerCurvature) * Mathf.Rad2Deg;
                angle = Mathf.Clamp(angle, -maxSteerAngle, maxSteerAngle);
                steer = Quaternion.AngleAxis(angle, parentUp[i]);
            }

            // Steer around the vehicle's up, then roll around the wheel's own axle
            w.localRotation = steer * restRotation[i] * Quaternion.AngleAxis(spin[i], localAxle[i]);
        }
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
