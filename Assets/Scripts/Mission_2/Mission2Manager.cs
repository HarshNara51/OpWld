using UnityEngine;

public class Mission2Manager : MonoBehaviour, IFailableMission
{
    public static Mission2Manager Instance { get; private set; }

    public enum MissionState { Setup, InProgress, Success, Failed }
    public MissionState CurrentState { get; private set; } = MissionState.Setup;

    public bool TargetEliminated { get; private set; }

    [Tooltip("Optional - toggles mission-only objects off and reveal objects on when the mission succeeds")]
    [SerializeField] private MissionCleanup cleanup;

    private void Awake()
    {
        Instance = this;
    }

    // Called by MissionBriefing once the player has read the
    // instructions and the 3-2-1 countdown finishes.
    public void StartMission()
    {
        CurrentState = MissionState.InProgress;
        TargetEliminated = false;
    }

    // Called by the mob leader's takedown trigger
    public void OnTargetEliminated()
    {
        if (CurrentState != MissionState.InProgress) return;

        TargetEliminated = true;
        Debug.Log("Target eliminated. Grab the item and get out.");
    }

    // Called by the item pickup trigger - only works after the target is down
    public void OnItemRecovered()
    {
        if (CurrentState != MissionState.InProgress || !TargetEliminated) return;

        CurrentState = MissionState.Success;
        Debug.Log("Item recovered. Mission Complete!");
        MissionResultUI.Instance.ShowMessage("Mission Complete!");
        if (cleanup != null) cleanup.ApplySuccessState();
    }

    public void FailMission(string reason)
    {
        if (CurrentState == MissionState.Success || CurrentState == MissionState.Failed) return;

        CurrentState = MissionState.Failed;
        Debug.Log($"Mission Failed: {reason}");
        MissionResultUI.Instance.ShowMessage("Mission Failed", () => GameManager.Instance.ReturnToHub());
    }
}
