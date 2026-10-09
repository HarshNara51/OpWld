using UnityEngine;

// Put this on the train/delivery gizmo in Mission1_Railway.
// Requires a trigger Collider and the player tagged "Player".
public class DeliveryZone : MonoBehaviour
{

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
        if (!playerInRange || !GameKeys.Down(GameAction.Interact)) return;
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

        if (!Mission1Manager.Instance.IsTaxiNear(transform.position))
        {
            Debug.Log("Park the taxi near the train to unload the cargo");
            return;
        }

        Mission1Manager.Instance.OnCargoDelivered();
    }
}