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
        NotePopup.Prompt(this, $"Press {NotePopup.Key(GameAction.Interact)} to pick up the cargo");
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
            if (!Mission1Manager.Instance.IsTaxiNear(transform.position))
            {
                NotePopup.Show("Bring the taxi here first - the cargo goes in the boot.", 3f);
                return;
            }

            Mission1Manager.Instance.OnCargoPickedUp();
            gameObject.SetActive(false);
        }
    }

    // Switched off (used up, picked up...) - take the prompt away with it
    private void OnDisable() => NotePopup.ClearPrompt(this);
}
