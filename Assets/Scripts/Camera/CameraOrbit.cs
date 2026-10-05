using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    [Header("Follow Target")]
    [SerializeField] private Transform target;                              // Drag the mannequin here
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.6f, 0f); // roughly chest/head height

    [Header("Orbit Distance")]
    [SerializeField] private float distance = 4f;
    [Tooltip("Distance used while driving - cars are bigger than the player")]
    [SerializeField] private float vehicleDistance = 6f;
    [SerializeField] private Vector3 extraOffset = new Vector3(0f, 0.3f, 0f); // small lift above the pivot point

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private float topClamp = -40f;   // how far up you can look
    [SerializeField] private float bottomClamp = 70f; // how far down you can look

    [Header("Collision (stops the camera going through walls/terrain)")]
    [Tooltip("Layers the camera bumps into. Defaults to everything.")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [Tooltip("Size of the camera's 'bubble' - bigger keeps it further from walls")]
    [SerializeField] private float collisionRadius = 0.3f;
    [Tooltip("Gap kept between the camera and whatever it hit")]
    [SerializeField] private float wallPadding = 0.1f;
    [Tooltip("Closest the camera may get to the pivot")]
    [SerializeField] private float minDistance = 0.6f;
    [Tooltip("How fast the camera slides back out once the wall is gone")]
    [SerializeField] private float returnSpeed = 6f;

    private float yaw;
    private float pitch;
    private float currentDistance;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        yaw = transform.eulerAngles.y;
        currentDistance = distance;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Mouse input drives yaw/pitch directly - this NEVER reads the mannequin's rotation,
        // so there's no feedback loop no matter how the character's body turns.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, topClamp, bottomClamp);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        float wantedDistance = VehicleInteraction.Current != null ? vehicleDistance : distance;

        // Cast from the pivot back toward where the camera wants to be.
        // If a wall/terrain/building is in the way, stop just in front of it.
        Vector3 pivotPosition = target.position + targetOffset;
        Vector3 backDirection = -(rotation * Vector3.forward);
        float allowedDistance = WallLimitedDistance(pivotPosition, backDirection, wantedDistance);

        // Snap in instantly (never clip), ease back out smoothly
        if (allowedDistance < currentDistance) currentDistance = allowedDistance;
        else currentDistance = Mathf.MoveTowards(currentDistance, allowedDistance, returnSpeed * Time.deltaTime);

        transform.position = pivotPosition + backDirection * currentDistance + extraOffset;
        transform.rotation = rotation;
    }

    private float WallLimitedDistance(Vector3 pivot, Vector3 direction, float wanted)
    {
        float closest = wanted;

        RaycastHit[] hits = Physics.SphereCastAll(pivot, collisionRadius, direction, wanted,
                                                  collisionMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            // Ignore the player / car we're following
            if (hit.transform.IsChildOf(target)) continue;
            if (hit.collider.CompareTag("Player")) continue;

            // distance 0 = the sphere started overlapping something; skip those
            if (hit.distance <= 0f) continue;

            float d = hit.distance - wallPadding;
            if (d < closest) closest = d;
        }

        return Mathf.Max(minDistance, closest);
    }
}