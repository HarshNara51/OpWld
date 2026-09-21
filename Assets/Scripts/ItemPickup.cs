using UnityEngine;

// Put this on the item to recover in the bedroom.
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.I;

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log($"Press {interactKey} to grab the item");
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
            Mission2Manager.Instance.OnItemRecovered();
            gameObject.SetActive(false);
        }
    }
}
