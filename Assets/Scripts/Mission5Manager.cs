using UnityEngine;

public class Mission5Manager : MonoBehaviour, IFailableMission
{
    public static Mission5Manager Instance { get; private set; }

    public enum MissionState { Setup, InProgress, Success, Failed }
    public MissionState CurrentState { get; private set; } = MissionState.Setup;

    [Tooltip("Optional - toggles mission-only objects off and reveal objects on when the mission succeeds")]
    [SerializeField] private MissionCleanup cleanup;

    private void Awake()
    {
        Instance = this;
    }

    // Called by MissionBriefing once the player has read the
    // instructions and the 3-2-1 countdown finishes (same pattern as
    // Mission 1 and Mission 3).
    public void StartMission()
    {
        CurrentState = MissionState.InProgress;
    }

    // Called once the defuse puzzle is solved
    public void OnCarSaved()
    {
        if (CurrentState != MissionState.InProgress) return;

        CurrentState = MissionState.Success;
        Debug.Log("Car saved!");
        MissionResultUI.Instance.ShowMessage("Car saved! It'll be waiting in the garage from now on.");
        GameManager.Instance.UnlockCar2();
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
