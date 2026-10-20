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
        UpdateObjective();
    }

    private void Update()
    {
        UpdateObjective(); // cheap; only changes the HUD when the text changes
    }

    // The top-left objective line - follows the bomb's progress, so the
    // bomb is only mentioned once it has been revealed
    private void UpdateObjective()
    {
        if (CurrentState != MissionState.InProgress) { ObjectiveHUD.Clear(); return; }

        BombSystem bomb = BombSystem.Instance;
        if (bomb != null && bomb.RevealNoteDone)
            ObjectiveHUD.Set("Defuse the bomb - or get out");
        else if (bomb != null && bomb.RevealStarted)
            ObjectiveHUD.Set("Drive it away");
        else
            ObjectiveHUD.Set("Break into the car");
    }

    // Called once the defuse puzzle is solved
    public void OnCarSaved()
    {
        if (CurrentState != MissionState.InProgress) return;

        CurrentState = MissionState.Success;
        UpdateObjective();
        GameManager.Instance.MarkMissionComplete(gameObject.scene.name); // saves progress
        MissionResultUI.Instance.ShowSuccess(""); // the art only - the details go in a note
        NotePopup.Show("Bomb defused! The car is yours now - you'll find it in the garage.", 6f);
        GameManager.Instance.UnlockCar2();
        if (cleanup != null) cleanup.ApplySuccessState();
    }

    // The bomb went off but the player got out in time: the mission is
    // failed, but no trip back to the Hub - free roam continues.
    public void OnBombWentOffOutside()
    {
        if (CurrentState != MissionState.InProgress) return;

        CurrentState = MissionState.Failed;
        UpdateObjective();
        MissionResultUI.Instance.ShowFailureStay(""); // the art only - the details go in the note below
        NotePopup.Show("The bomb went off! You got out just in time... but that car's wrecked. Come back and try again anytime.", 6f);
    }

    public void FailMission(string reason)
    {
        if (CurrentState == MissionState.Success || CurrentState == MissionState.Failed) return;

        CurrentState = MissionState.Failed;
        UpdateObjective();
        if (!string.IsNullOrEmpty(reason)) NotePopup.Show(reason + ".", 5f); // the reason as a note, not centre-screen text
        MissionResultUI.Instance.ShowFailure(""); // shared fail screen, then back to Hub
    }
}