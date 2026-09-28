using UnityEngine;

// Put this on each cargo pickup gizmo in Mission1_Railway.
// Requires a trigger Collider and the player tagged "Player".
public class CargoPickup : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log($"Press {interactKey} to pick up cargo");
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
            Mission1Manager.Instance.OnCargoPickedUp();
            gameObject.SetActive(false);
        }
    }
}
