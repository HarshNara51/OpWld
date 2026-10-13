using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    [Header("Follow Target")]
    [SerializeField] private Transform target;                              
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.6f, 0f); 

    [Header("Orbit Distance")]
    [SerializeField] private float distance = 4f;
    [Tooltip("Distance used while driving - cars are bigger than the player")]
    [SerializeField] private float vehicleDistance = 6f;
    [SerializeField] private Vector3 extraOffset = new Vector3(0f, 0.3f, 0f); 

    [Header("Aiming (GTA V Style)")]
    [Tooltip("Tighter distance for an over-the-shoulder feel")]
    [SerializeField] private float aimDistance = 1.2f; 
    [Tooltip("Shift towards the right shoulder (x = right, y = up)")]
    [SerializeField] private Vector2 aimShoulderOffset = new Vector2(0.8f, 0.2f); // Changed Y to positive to look OVER the shoulder
    [Tooltip("Tighter zoom while aiming - field of view in degrees")]
    [SerializeField] private float aimFieldOfView = 35f; 
    [Tooltip("How fast the camera eases in/out of the aim view (per second)")]
    [SerializeField] private float aimBlendSpeed = 12f; // Doubled the speed for a snappier, GTA V-like transition

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private float topClamp = -40f;   
    [SerializeField] private float bottomClamp = 70f; 

    [Header("Driving: swing back behind the car")]
    [SerializeField] private float recenterDelay = 5f;
    [SerializeField] private float recenterAngle = 30f;
    [SerializeField] private float recenterSmoothTime = 0.8f;
    [SerializeField] private float recenterMinSpeed = 2f;

    [Header("Collision (stops the camera going through walls/terrain)")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float wallPadding = 0.1f;
    [SerializeField] private float minDistance = 0.6f;
    [SerializeField] private float returnSpeed = 6f;

    private float yaw;
    private float pitch;
    private float currentDistance;
    private float aimBlend; 
    private Camera cam;
    private float normalFieldOfView;
    private float idleTime;      
    private float recenterVelocity;
    private bool recentering;    

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        yaw = transform.eulerAngles.y;
        currentDistance = distance;
        cam = GetComponent<Camera>();
        if (cam != null) normalFieldOfView = cam.fieldOfView;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, topClamp, bottomClamp);

        RecenterBehindCar(mouseX, mouseY);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Aiming Logic
        bool aiming = PlayerLocomotion.IsAiming && VehicleInteraction.Current == null;
        
        // Use a slightly different easing approach if you want it to feel even punchier, but SmoothStep works great here.
        aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, aimBlendSpeed * Time.deltaTime);
        float ease = Mathf.SmoothStep(0f, 1f, aimBlend);

        float wantedDistance = VehicleInteraction.Current != null ? vehicleDistance : Mathf.Lerp(distance, aimDistance, ease);
        
        if (cam != null && !CameraMode.IsActive) 
        {
            cam.fieldOfView = Mathf.Lerp(normalFieldOfView, aimFieldOfView, ease);
        }

        Vector3 shoulder = rotation * new Vector3(aimShoulderOffset.x, aimShoulderOffset.y, 0f) * ease;
        Vector3 pivotPosition = target.position + targetOffset + shoulder;
        Vector3 backDirection = -(rotation * Vector3.forward);
        float allowedDistance = WallLimitedDistance(pivotPosition, backDirection, wantedDistance);

        if (allowedDistance < currentDistance) currentDistance = allowedDistance;
        else currentDistance = Mathf.MoveTowards(currentDistance, allowedDistance, returnSpeed * Time.deltaTime);

        transform.position = pivotPosition + backDirection * currentDistance + extraOffset;
        transform.rotation = rotation;
    }

    private void RecenterBehindCar(float mouseX, float mouseY)
    {
        VehicleInteraction car = VehicleInteraction.Current;
        bool mouseMoved = Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f;
        if (car == null || mouseMoved || recenterDelay <= 0f || CameraMode.IsActive)
        {
            StopRecentering();
            return;
        }

        Vector3 forward = car.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) return;
        float behind = Quaternion.LookRotation(forward).eulerAngles.y;
        float off = Mathf.Abs(Mathf.DeltaAngle(yaw, behind));

        if (recentering)
        {
            yaw = Mathf.SmoothDampAngle(yaw, behind, ref recenterVelocity, recenterSmoothTime);
            if (off < 2f) StopRecentering(); 
            return;
        }

        Rigidbody rb = car.GetComponent<Rigidbody>();
        float forwardSpeed = rb != null ? Vector3.Dot(rb.linearVelocity, car.transform.forward) : 0f;
        if (forwardSpeed < recenterMinSpeed || off < recenterAngle)
        {
            idleTime = 0f;
            return;
        }

        idleTime += Time.deltaTime;
        if (idleTime >= recenterDelay) recentering = true;
    }

    private void StopRecentering()
    {
        recentering = false;
        idleTime = 0f;
        recenterVelocity = 0f;
    }

    private float WallLimitedDistance(Vector3 pivot, Vector3 direction, float wanted)
    {
        float closest = wanted;

        RaycastHit[] hits = Physics.SphereCastAll(pivot, collisionRadius, direction, wanted,
                                                  collisionMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(target)) continue;
            if (hit.collider.CompareTag("Player")) continue;

            if (hit.distance <= 0f) continue;

            float d = hit.distance - wallPadding;
            if (d < closest) closest = d;
        }

        return Mathf.Max(minDistance, closest);
    }
}