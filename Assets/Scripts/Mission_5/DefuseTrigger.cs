using UnityEngine;
using UnityEngine.Events;

// Put this at the secluded defuse spot. Works on foot or from ANY car
// (including the stolen one) - no tags or colliders needed, it just
// checks distance. Press the key to start defusing.
public class DefuseTrigger : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float radius = 6f;

    [Tooltip("Wire to the defuse puzzle's Open")]
    public UnityEvent onUse;

    private Transform player;
    private bool wasInRange;

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return; // a puzzle or pause is open

        bool inRange = IsInRange();

        if (inRange && !wasInRange) Debug.Log($"Press {interactKey} to defuse the bomb");
        wasInRange = inRange;

        if (inRange && Input.GetKeyDown(interactKey)) onUse.Invoke();
    }

    private bool IsInRange()
    {
        Transform target = null;
        if (player != null && player.gameObject.activeInHierarchy) target = player;
        else if (VehicleInteraction.Current != null) target = VehicleInteraction.Current.transform;
        if (target == null) return false;

        Vector3 offset = target.position - transform.position;
        offset.y = 0f;
        return offset.magnitude <= radius;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}