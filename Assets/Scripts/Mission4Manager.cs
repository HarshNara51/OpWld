using UnityEngine;

public class Mission4Manager : MonoBehaviour, IFailableMission
{
    public static Mission4Manager Instance { get; private set; }

    public enum MissionState { Setup, InProgress, Success, Failed }
    public MissionState CurrentState { get; private set; } = MissionState.Setup;

    [Tooltip("Enemies to eliminate before the remaining ones flee in the truck")]
    [SerializeField] private int enemiesToEliminate = 8;

    public int EnemiesDown { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // Called by MissionBriefing once the countdown finishes
    public void StartMission()
    {
        CurrentState = MissionState.InProgress;
        EnemiesDown = 0;
    }

    // Wire this to every fighting enemy's Health > On Death
    public void OnEnemyDown()
    {
        if (CurrentState != MissionState.InProgress) return;

        EnemiesDown++;
        Debug.Log($"Enemy down ({EnemiesDown}/{enemiesToEliminate})");

        if (EnemiesDown >= enemiesToEliminate)
        {
            Debug.Log("Remaining enemies are fleeing - get to your car!");
            // Hook point: trigger the truck's MissionTruckFollower.BeginMoving()
            // here once that piece is wired in for this mission
        }
    }

    public void FailMission(string reason)
    {
        if (CurrentState == MissionState.Success || CurrentState == MissionState.Failed) return;

        CurrentState = MissionState.Failed;
        Debug.Log($"Mission Failed: {reason}");
        MissionResultUI.Instance.ShowMessage("Mission Failed", () => GameManager.Instance.ReturnToHub());
    }
}
