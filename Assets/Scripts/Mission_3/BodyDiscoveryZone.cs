using UnityEngine;

// Put this on the body (or the trigger zone around it). Photographing
// the body with Camera Mode is what starts the countdown and the
// trucks moving.
public class BodyDiscoveryZone : PhotoTarget
{
    protected override bool CanBePhotographed()
    {
        return Mission3Manager.Instance != null
               && Mission3Manager.Instance.CurrentState == Mission3Manager.MissionState.Searching;
    }

    protected override void HandlePhotographed()
    {
        Mission3Manager.Instance.OnBodyFound();
    }
}