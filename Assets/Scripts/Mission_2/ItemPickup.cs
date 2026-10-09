using UnityEngine;

// Put this on the item to recover in the bedroom.
public class ItemPickup : MonoBehaviour
{

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log($"Press {GameKeys.Label(GameAction.Interact)} to grab the item");
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
            Mission2Manager.Instance.OnItemRecovered();
            gameObject.SetActive(false);
        }
    }
}
