using UnityEngine;

// Put this on each cargo pickup gizmo in Mission1_Railway.
// Requires a trigger Collider and the player tagged "Player".
public class CargoPickup : MonoBehaviour
{

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log($"Press {GameKeys.Label(GameAction.Interact)} to pick up cargo");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }

    private void Update()
    {
        if (playerInRange && GameKeys.Down(GameAction.Interact))
        {
            if (!Mission1Manager.Instance.IsTaxiNear(transform.position))
            {
                Debug.Log("Bring the taxi here to load the cargo");
                return;
            }

            Mission1Manager.Instance.OnCargoPickedUp();
            gameObject.SetActive(false);
        }
    }
}