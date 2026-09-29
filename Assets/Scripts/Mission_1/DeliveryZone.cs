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

        if (Mission1Manager.Instance.HasTrainDeparted)
        {
            Debug.Log("Train is moving, not able to deliver");
            return;
        }

        if (!Mission1Manager.Instance.IsTrainAtPlatform)
        {
            Debug.Log("Train hasn't arrived yet");
            return;
        }

        Mission1Manager.Instance.OnCargoDelivered();
    }
}