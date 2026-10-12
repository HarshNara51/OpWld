using UnityEngine;

// Put this on the item to recover in the bedroom.
public class ItemPickup : MonoBehaviour
{

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        NotePopup.Prompt(this, $"Press {NotePopup.Key(GameAction.Interact)} to grab the item");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        NotePopup.ClearPrompt(this);
    }

    private void Update()
    {
        if (playerInRange && GameKeys.Down(GameAction.Interact))
        {
            Mission2Manager.Instance.OnItemRecovered();
            gameObject.SetActive(false);
        }
    }

    // Switched off (used up, picked up...) - take the prompt away with it
    private void OnDisable() => NotePopup.ClearPrompt(this);
}
