using UnityEngine;
using UnityEngine.Events;

// A destination - e.g. the vantage point. Fires "On Reached" when the
// player gets within Radius, on foot OR in any car. No collider needed.
// Reusable: escape points, checkpoints, "go here" objectives...
public class ReachZone : MonoBehaviour
{
    [SerializeField] private float radius = 6f;
    [Tooltip("Only fire once")]
    [SerializeField] private bool oneShot = true;

    public UnityEvent onReached;

    private Transform player;
    private bool reached;

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (oneShot && reached) return;

        Transform target = null;
        if (player != null && player.gameObject.activeInHierarchy) target = player;
        else if (VehicleInteraction.Current != null) target = VehicleInteraction.Current.transform;
        if (target == null) return;

        Vector3 offset = target.position - transform.position;
        offset.y = 0f;
        bool inside = offset.magnitude <= radius;

        if (inside && !reached)
        {
            reached = true;
            onReached.Invoke();
        }
        else if (!inside && !oneShot)
        {
            reached = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
