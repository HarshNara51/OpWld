using UnityEngine;

// Put this at the police station.
public class PoliceDeliveryZone : MonoBehaviour
{

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log($"Press {GameKeys.Label(GameAction.Interact)} to deliver the evidence");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }

    private void Update()
    {
        if (!playerInRange || !GameKeys.Down(GameAction.Interact)) return;

        var state = Mission3Manager.Instance.CurrentState;
        if (state == Mission3Manager.MissionState.Delivering)
        {
            Mission3Manager.Instance.OnEvidenceDelivered();
        }
        else if (state == Mission3Manager.MissionState.Following)
        {
            NotePopup.Show("You don't have the photo of the trucks yet. Follow them and take it first.", 4f);
        }
    }
}