using UnityEngine;

// Put this on the "return home" gizmo in every mission scene.
// Requires a Collider with "Is Trigger" checked, and your
// player GameObject tagged "Player".
// The key has to be HELD (not tapped) so nobody quits a mission by
// accident while mashing the interact key.
public class ReturnHomeTrigger : MonoBehaviour
{

    [Tooltip("Seconds the key must be held")]
    public float holdDuration = 1f;

    private bool playerInRange;
    private float heldTime;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log($"Hold {GameKeys.Label(GameAction.Interact)} to return to Hub");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        heldTime = 0f;
    }

    private void Update()
    {
        if (!playerInRange || !GameKeys.Held(GameAction.Interact))
        {
            heldTime = 0f;
            return;
        }

        heldTime += Time.deltaTime; // doesn't count while paused
        if (heldTime >= holdDuration)
        {
            heldTime = 0f;
            GameManager.Instance.ReturnToHub();
        }
    }
}
