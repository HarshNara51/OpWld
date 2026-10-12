using UnityEngine;

// Put this on each mission gizmo in the Hub scene.
// Requires a Collider with "Is Trigger" checked, and your
// player GameObject tagged "Player".
public class MissionEntryPoint : MonoBehaviour
{
    [Tooltip("Exact scene name to load, e.g. Mission1_Railway")]
    public string missionSceneName;


    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        NotePopup.Prompt(this, $"Press {NotePopup.Key(GameAction.Interact)} to start the mission: <b>{GizmoLabel.MissionName(missionSceneName)}</b>");
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
            GameManager.Instance.LoadMission(missionSceneName);
        }
    }

    // Switched off (used up, picked up...) - take the prompt away with it
    private void OnDisable() => NotePopup.ClearPrompt(this);
}
