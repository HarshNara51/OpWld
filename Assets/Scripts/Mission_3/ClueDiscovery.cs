using UnityEngine;

// Put this on each of the 5 clues. Order doesn't matter. The clue is
// photographed with Camera Mode (C, then left click) - type its note
// in the Inspector. Only counts after the body has been photographed.
public class ClueDiscovery : PhotoTarget
{
    protected override bool CanBePhotographed()
    {
        return Mission3Manager.Instance != null
               && Mission3Manager.Instance.CurrentState == Mission3Manager.MissionState.Investigating;
    }

    protected override void HandlePhotographed()
    {
        Mission3Manager.Instance.OnClueFound();
    }
}