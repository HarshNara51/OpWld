using UnityEngine;

// Put this on the hiding spot trigger zone. Mission3Manager checks
// PlayerInside the moment the trucks arrive; this script itself fails
// the mission if the player leaves while the trucks are still there.
public class HideZone : MonoBehaviour, IGizmoVisibility
{
    public bool PlayerInside { get; private set; }

    // The marker shows once every clue is photographed, until the trucks arrive
    public bool ShowGizmo
    {
        get
        {
            Mission3Manager m = Mission3Manager.Instance;
            if (m == null) return true;
            return m.CurrentState == Mission3Manager.MissionState.Investigating && m.CluesFound >= m.TotalClues;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        PlayerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        PlayerInside = false;

        if (Mission3Manager.Instance.CurrentState == Mission3Manager.MissionState.TrucksAtFarm)
        {
            Mission3Manager.Instance.FailMission("Spotted");
        }
    }
}
