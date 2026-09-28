using UnityEngine;

public class Mission4Manager : MonoBehaviour, IFailableMission
{
    public static Mission4Manager Instance { get; private set; }

    public enum MissionState { Setup, InProgress, Success, Failed }
    public MissionState CurrentState { get; private set; } = MissionState.Setup;

    [Tooltip("Enemies to eliminate before the remaining ones flee in the truck")]
    [SerializeField] private int enemiesToEliminate = 8;

    [Tooltip("The escaping truck - BeginMoving() is called on it once enough enemies are down")]
    [SerializeField] private MissionTruckFollower truck;

    [Tooltip("The EMP bar's panel - hidden until the truck actually starts fleeing")]
    [SerializeField] private GameObject empBarPanel;

    [Tooltip("Optional - toggles mission-only objects off and reveal objects on when the mission succeeds")]
    [SerializeField] private MissionCleanup cleanup;

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
            if (truck != null) truck.BeginMoving();
            if (empBarPanel != null) empBarPanel.SetActive(true);
        }
    }

    public void CompleteMission()
    {
        if (CurrentState != MissionState.InProgress) return;

        CurrentState = MissionState.Success;
        Debug.Log("Mission Complete!");
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