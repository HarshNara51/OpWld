using UnityEngine;

// Shopping Complex: marks the car to steal (a GizmoVFX_Pickup marker + "TARGET CAR" label
// as children of this object, which sits on the car). Visible only while the mission is
// running and the player hasn't got in yet - once they're in, it's gone.
public class TargetCarMarker : MonoBehaviour, IGizmoVisibility
{
    public bool ShowGizmo =>
        Mission5Manager.Instance != null &&
        Mission5Manager.Instance.CurrentState == Mission5Manager.MissionState.InProgress &&
        (BombSystem.Instance == null || !BombSystem.Instance.RevealStarted);
}
