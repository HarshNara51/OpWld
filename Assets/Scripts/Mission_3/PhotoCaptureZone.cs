using UnityEngine;

// The final photo of the trucks. Put this where the trucks end up
// (or set Focus Point to the lead truck). Only counts while following.
// Works on foot or from the car.
public class PhotoCaptureZone : PhotoTarget
{
    protected override bool CanBePhotographed()
    {
        return Mission3Manager.Instance != null
               && Mission3Manager.Instance.CurrentState == Mission3Manager.MissionState.Following;
    }

    protected override void HandlePhotographed()
    {
        Mission3Manager.Instance.OnPhotoTaken();
    }
}