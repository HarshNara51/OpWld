using UnityEngine;

// Put this on an empty GameObject in each mission scene. Reusable
// across every mission - just drag different objects into the
// arrays per scene.
public class MissionCleanup : MonoBehaviour
{
    [Tooltip("Mission-only objects to disable once the mission succeeds (trucks, clue gizmos, etc.)")]
    [SerializeField] private GameObject[] disableOnSuccess;

    [Tooltip("Objects to reveal once the mission succeeds (e.g. background parked traffic)")]
    [SerializeField] private GameObject[] enableOnSuccess;

    private void Awake()
    {
        // Make sure "reveal" objects start hidden, even if they were
        // left active in the Editor for easy placement/testing.
        foreach (var obj in enableOnSuccess)
        {
            if (obj != null) obj.SetActive(false);
        }
    }

    public void ApplySuccessState()
    {
        foreach (var obj in disableOnSuccess)
        {
            if (obj != null) obj.SetActive(false);
        }

        foreach (var obj in enableOnSuccess)
        {
            if (obj != null) obj.SetActive(true);
        }
    }
}
