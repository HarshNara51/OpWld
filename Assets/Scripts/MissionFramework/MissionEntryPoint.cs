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
        Debug.Log($"Press {GameKeys.Label(GameAction.Interact)} to start {missionSceneName}");
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
            GameManager.Instance.LoadMission(missionSceneName);
        }
    }
}
