using UnityEngine;

// Put this on the train/delivery gizmo in Mission1_Railway.
// Requires a trigger Collider and the player tagged "Player".
public class DeliveryZone : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }

    private void Update()
    {
        if (!playerInRange || !Input.GetKeyDown(interactKey)) return;
        if (!Mission1Manager.Instance.IsCarryingCargo) return;

        Mission1Manager.Instance.OnCargoDelivered();
    }
}
