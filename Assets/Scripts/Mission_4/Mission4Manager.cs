using UnityEngine;

public class Mission4Manager : MonoBehaviour, IFailableMission
{
    public static Mission4Manager Instance { get; private set; }

    public enum MissionState { Setup, InProgress, Success, Failed }
    public MissionState CurrentState { get; private set; } = MissionState.Setup;

    [Tooltip("Enemies to eliminate before the remaining ones flee in the truck")]
    [SerializeField] private int enemiesToEliminate = 8;

    [Tooltip("The escaping truck - BeginMoving() is called on it once enough enemies are down")]
    [SerializeField] private SplineVehicle truck;

    [Tooltip("The EMP bar's panel - hidden until the truck actually starts fleeing")]
    [SerializeField] private GameObject empBarPanel;

    [Tooltip("Optional - toggles mission-only objects off and reveal objects on when the mission succeeds")]
    [SerializeField] private MissionCleanup cleanup;

    [Header("Guidance")]
    [Tooltip("Shown the moment the gang scatters for cover (the first shot)")]
    [TextArea(2, 4)]
    [SerializeField] private string takeCoverNote = "They're scattering for cover! Get behind the trash cans at the Hide Here marker and shoot from there.";
    [Tooltip("Shown right after, about the mission area ring")]
    [TextArea(2, 4)]
    [SerializeField] private string missionAreaNote = "Stay inside the red ring on the ground. Leave the mission area and the job is blown.";

    public int EnemiesDown { get; private set; }

    private bool combatNoted;

    // True once enough enemies are down and the truck flees - ends the shootout part
    public bool ShootoutOver => EnemiesDown >= enemiesToEliminate;

    private void Awake()
    {
        Instance = this;
        EnemyCover.CombatActive = false; // static - would otherwise stay true on a replay
    }

    // Called by MissionBriefing once the countdown finishes
    public void StartMission()
    {
        CurrentState = MissionState.InProgress;
        EnemiesDown = 0;
    }

    // First shot fired: tell the player where to take cover and where the fight's limits are
    private void Update()
    {
        if (combatNoted || CurrentState != MissionState.InProgress || !EnemyCover.CombatActive) return;
        combatNoted = true;
        if (!string.IsNullOrEmpty(takeCoverNote)) NotePopup.Show(takeCoverNote, 6f);
        if (!string.IsNullOrEmpty(missionAreaNote)) NotePopup.Show(missionAreaNote, 5f); // queues after
    }

    // Wire this to every fighting enemy's Health > On Death
    public void OnEnemyDown()
    {
        if (CurrentState != MissionState.InProgress) return;

        EnemiesDown++;
        if (EnemiesDown < enemiesToEliminate)
            NotePopup.Show($"Enemy down ({EnemiesDown}/{enemiesToEliminate})", 2f);

        if (EnemiesDown >= enemiesToEliminate)
        {
            NotePopup.Show("The rest of the gang is getting away in the truck! Get in your car and follow it. " +
                           $"Once you're close enough, hold {GameKeys.Label(GameAction.EMP)} to fire the EMP and disable the truck.");
            if (truck != null) truck.BeginMoving();
            if (empBarPanel != null) empBarPanel.SetActive(true);
        }
    }

    public void CompleteMission()
    {
        if (CurrentState != MissionState.InProgress) return;

        CurrentState = MissionState.Success;
        GameManager.Instance.MarkMissionComplete(gameObject.scene.name); // saves progress
        MissionResultUI.Instance.ShowMessage("Mission Complete!");
        if (cleanup != null) cleanup.ApplySuccessState();
    }

    public void FailMission(string reason)
    {
        if (CurrentState == MissionState.Success || CurrentState == MissionState.Failed) return;

        CurrentState = MissionState.Failed;
        MissionResultUI.Instance.ShowFailure(reason); // shared fail screen, then back to Hub
    }
}