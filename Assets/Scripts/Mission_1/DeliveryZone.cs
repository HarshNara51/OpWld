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
        if (Mission1Manager.Instance != null && Mission1Manager.Instance.IsCarryingCargo)
            NotePopup.Prompt(this, $"Press {NotePopup.Key(GameAction.Interact)} to unload the cargo onto the train");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        NotePopup.ClearPrompt(this);
    }

    private void Update()
    {
        if (!playerInRange || !GameKeys.Down(GameAction.Interact)) return;
        if (!Mission1Manager.Instance.IsCarryingCargo) return;

        if (Mission1Manager.Instance.HasTrainDeparted)
        {
            NotePopup.Show("Too late - the train is already moving.", 3f);
            return;
        }

        if (!Mission1Manager.Instance.IsTrainAtPlatform)
        {
            NotePopup.Show("The train hasn't arrived yet. Wait for it.", 3f);
            return;
        }

        if (!Mission1Manager.Instance.IsTaxiNear(transform.position))
        {
            NotePopup.Show("Park the taxi next to the train to unload the cargo.", 3f);
            return;
        }

        Mission1Manager.Instance.OnCargoDelivered();
    }

    // Switched off (used up, picked up...) - take the prompt away with it
    private void OnDisable() => NotePopup.ClearPrompt(this);
}
