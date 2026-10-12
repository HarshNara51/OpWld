using UnityEngine;

// Put this on the call-cops interact point, near where the truck
// ends up stopped. Starts inactive in the scene - TruckEMP reveals
// it automatically once the truck's actually been disabled, so it
// can't be used early. Hold the interact key for holdDuration
// seconds near the truck to complete the mission - letting go or
// stepping away just pauses progress, doesn't reset it.
public class CallCopsTrigger : MonoBehaviour
{
    [SerializeField] private float holdDuration = 5f;

    private bool playerInRange;
    private float heldTime;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        NotePopup.Prompt(this, $"Hold {NotePopup.Key(GameAction.Interact)} to call the cops");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        NotePopup.ClearPrompt(this);
    }

    private void Update()
    {
        if (!playerInRange || !GameKeys.Held(GameAction.Interact)) return;

        heldTime += Time.deltaTime;

        // TEMP DIAGNOSTIC - remove once a real UI bar exists, if wanted
        int second = Mathf.Min(Mathf.FloorToInt(heldTime), Mathf.FloorToInt(holdDuration));
        NotePopup.Prompt(this, $"Calling the cops... keep holding {NotePopup.Key(GameAction.Interact)} ({second}/{holdDuration:0}s)");

        if (heldTime >= holdDuration)
        {
            Mission4Manager.Instance.CompleteMission();
            gameObject.SetActive(false);
        }
    }

    // Switched off (used up, picked up...) - take the prompt away with it
    private void OnDisable() => NotePopup.ClearPrompt(this);
}
