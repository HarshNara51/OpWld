using UnityEngine;

// Put this on each cop / thief / guard. By default it notices the
// player in a full circle (cops, vans). Tick "Use Vision Cone" for
// guards: they only see what's in front of them, and barely notice
// someone sneaking up right behind.
public class SuspicionSource : MonoBehaviour
{
    [Tooltip("Distance at which this source starts detecting the player")]
    public float detectRadius = 15f;

    [Header("Vision cone (optional)")]
    public bool useVisionCone = false;
    [Tooltip("Total width of the cone in front, in degrees")]
    [Range(10f, 360f)] public float viewAngle = 110f;
    [Tooltip("Outside the cone, only noticed within this fraction of the radius (0 = never)")]
    [Range(0f, 1f)] public float rearAwarenessFraction = 0.1f;
    [Tooltip("How strongly he notices someone right behind him (0-1)")]
    [Range(0f, 1f)] public float rearAwarenessStrength = 0.4f;

    // 0 = not noticed at all, 1 = right on top of it in plain view
    public float DetectionRatio(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        float distance = toTarget.magnitude;
        if (distance >= detectRadius) return 0f;

        float ratio = 1f - distance / detectRadius;
        if (!useVisionCone) return ratio;

        Vector3 flatForward = transform.forward; flatForward.y = 0f;
        Vector3 flatTo = toTarget; flatTo.y = 0f;
        if (Vector3.Angle(flatForward, flatTo) <= viewAngle * 0.5f) return ratio; // in view

        float rearRadius = detectRadius * rearAwarenessFraction;
        if (rearRadius <= 0f || distance >= rearRadius) return 0f;
        return (1f - distance / rearRadius) * rearAwarenessStrength;
    }

    // Draws the range (and cone) in the Scene view when selected
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        if (!useVisionCone) return;

        Gizmos.color = Color.yellow;
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(transform.position, transform.position + left * detectRadius);
        Gizmos.DrawLine(transform.position, transform.position + right * detectRadius);
    }
}