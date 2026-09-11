using UnityEngine;

// Put this on the "return home" gizmo in every mission scene.
// Requires a Collider with "Is Trigger" checked, and your
// player GameObject tagged "Player".
public class ReturnHomeTrigger : MonoBehaviour
{
    [Tooltip("Key the player presses to return to the hub")]
    public KeyCode interactKey = KeyCode.E;

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log($"Press {interactKey} to return to Hub");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }

    private void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            GameManager.Instance.ReturnToHub();
        }
    }
}