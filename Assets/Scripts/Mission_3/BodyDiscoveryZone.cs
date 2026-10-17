using UnityEngine;

// Put this on the body (or the trigger zone around it). Photographing
// the body with Camera Mode is what starts the countdown and the
// trucks moving.
public class BodyDiscoveryZone : PhotoTarget
{
    [Tooltip("Shown once, the first time the player walks up to the body (the player came to help the son)")]
    [TextArea(2, 5)]
    [SerializeField] private string discoveryNote =
        "Oh no... the farmer's son is dead. The man who once helped my family... and I came too late. " +
        "Maybe there are clues around the farm that can tell me what really happened here.";

    private bool discovered;

    protected override void OnTriggerEnter(Collider other)
    {
        if (!discovered && !Photographed && CanBePhotographed()
            && (other.CompareTag("Player") || other.CompareTag("PlayerCar")))
        {
            discovered = true;
            NotePopup.Show(discoveryNote, 7f);
        }
        base.OnTriggerEnter(other);
    }

    protected override bool CanBePhotographed()
    {
        return Mission3Manager.Instance != null
               && Mission3Manager.Instance.CurrentState == Mission3Manager.MissionState.Searching;
    }

    // The car has to be in its parking spot first - the player walks back to it later
    protected override string NotYetReason()
    {
        if (Mission3Manager.Instance == null || Mission3Manager.Instance.CarParkedOrNoSpot) return null;
        return "Park your car at the Park Here spot first, and come back on foot.";
    }

    protected override void HandlePhotographed()
    {
        Mission3Manager.Instance.OnBodyFound();
    }
}