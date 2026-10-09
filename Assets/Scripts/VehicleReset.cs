using UnityEngine;

// Put this on the Managers prefab (any persistent object).
// While driving ANY car (player car, taxi, ...), press the key to
// unstick it: moves the car a little ahead (never into a wall), drops
// it onto the ground, sets it upright facing the same way, and kills
// its velocity. Short cooldown to prevent spam.
public class VehicleReset : MonoBehaviour
{

    [Tooltip("How far ahead the car is moved")]
    [SerializeField] private float forwardDistance = 4f;

    [Tooltip("Height above the ground the car is dropped from")]
    [SerializeField] private float liftHeight = 1f;

    [Tooltip("Gap kept from any wall/obstacle in front")]
    [SerializeField] private float wallClearance = 2.5f;

    [Tooltip("Layers that count as ground/obstacles. Defaults to everything.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("Seconds before the reset can be used again")]
    [SerializeField] private float cooldown = 2f;

    private float nextAllowedTime;

    private void Update()
    {
        if (!GameKeys.Down(GameAction.ResetCar) || Time.time < nextAllowedTime) return;

        VehicleInteraction car = VehicleInteraction.Current;
        if (car == null) return; // only works while driving

        nextAllowedTime = Time.time + cooldown;

        Transform t = car.transform;

        // Keep the direction the car was facing, but level it out
        Vector3 forward = t.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;

        // How far ahead we can safely go - stop short of anything in front
        float distance = forwardDistance;
        Vector3 rayStart = t.position + Vector3.up * 1f;
        if (TryRaycastIgnoringCar(rayStart, forward, forwardDistance + wallClearance, t, out RaycastHit wallHit))
        {
            distance = Mathf.Max(0f, wallHit.distance - wallClearance);
        }

        Vector3 target = t.position + forward * distance;

        // Drop onto the ground at the new spot
        Vector3 groundRayStart = target + Vector3.up * 20f;
        if (TryRaycastIgnoringCar(groundRayStart, Vector3.down, 60f, t, out RaycastHit groundHit))
        {
            target.y = groundHit.point.y + liftHeight;
        }
        else
        {
            target.y += liftHeight;
        }

        t.position = target;
        t.rotation = Quaternion.LookRotation(forward, Vector3.up);

        Rigidbody rb = car.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"{car.name} reset {distance:F1}m ahead.");
    }

    // Raycast that skips the car's own colliders and all triggers
    private bool TryRaycastIgnoringCar(Vector3 origin, Vector3 dir, float maxDist, Transform car, out RaycastHit closest)
    {
        closest = default;
        float best = float.MaxValue;
        bool found = false;

        foreach (RaycastHit hit in Physics.RaycastAll(origin, dir, maxDist, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(car)) continue;
            if (hit.distance < best)
            {
                best = hit.distance;
                closest = hit;
                found = true;
            }
        }
        return found;
    }
}