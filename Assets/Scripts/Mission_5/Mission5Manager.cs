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
        GameManager.Instance.MarkMissionComplete(gameObject.scene.name); // saves progress
        MissionResultUI.Instance.ShowSuccess("Bomb defused! The car is yours now - you'll find it in the garage.");
        GameManager.Instance.UnlockCar2();
        if (cleanup != null) cleanup.ApplySuccessState();
    }

    // The bomb went off but the player got out in time: the mission is
    // failed, but no trip back to the Hub - free roam continues.
    public void OnBombWentOffOutside()
    {
        if (CurrentState != MissionState.InProgress) return;

        CurrentState = MissionState.Failed;
        Debug.Log("Mission Failed: bomb went off (player escaped).");
        MissionResultUI.Instance.ShowMessage(
            "MISSION FAILED\n<size=60%>The bomb went off. The car is no longer drivable.\nBetter luck next time!</size>");
        NotePopup.Show("You got out just in time... but that car's wrecked. Come back and try again anytime.", 5f);
    }

    public void FailMission(string reason)
    {
        if (CurrentState == MissionState.Success || CurrentState == MissionState.Failed) return;

        CurrentState = MissionState.Failed;
        Debug.Log($"Mission Failed: {reason}");
        MissionResultUI.Instance.ShowFailure(reason); // shared fail screen, then back to Hub
    }
}