using UnityEngine;

// Put this on Main Camera, alongside CameraOrbit and CameraShake.
// IMPORTANT: in Project Settings > Script Execution Order, add this
// AFTER CameraOrbit (same rule as CameraShake) - it needs CameraOrbit's
// normal orbit position already applied before it can check for walls.
public class CameraCollision : MonoBehaviour
{
    [Tooltip("Same target CameraOrbit is following - usually the player or car")]
    [SerializeField] private Transform target;
    [Tooltip("Matches CameraOrbit's own Target Offset, so the check originates from the same pivot point")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private LayerMask collisionMask = ~0;
    [Tooltip("Small gap kept between the camera and whatever it pulled in against")]
    [SerializeField] private float buffer = 0.2f;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 pivot = target.position + targetOffset;
        Vector3 desiredPos = transform.position;
        Vector3 direction = desiredPos - pivot;
        float desiredDistance = direction.magnitude;

        if (desiredDistance < 0.01f) return;
        direction /= desiredDistance;

        if (Physics.SphereCast(pivot, collisionRadius, direction, out RaycastHit hit, desiredDistance, collisionMask))
        {
            float allowedDistance = Mathf.Max(hit.distance - buffer, 0f);
            transform.position = pivot + direction * allowedDistance;
        }
    }
}
