using UnityEngine;

public class Mission1Manager : MonoBehaviour, IFailableMission
{
    public static Mission1Manager Instance { get; private set; }

    public enum MissionState { Setup, InProgress, Success, Failed }
    public MissionState CurrentState { get; private set; } = MissionState.Setup;

    [Tooltip("All cargo pickup points in this mission, in the order they should unlock")]
    [SerializeField] private CargoPickup[] cargoPickups;

    [Tooltip("The locomotive (lead SplineVehicle) - its cars follow automatically")]
    [SerializeField] private SplineVehicle train;

    [Tooltip("Optional - toggles mission-only objects off and reveal objects on when the mission succeeds")]
    [SerializeField] private MissionCleanup cleanup;

    [Header("Taxi rule")]
    [Tooltip("The taxi - cargo can only be loaded/unloaded when it's parked nearby. Leave empty to allow any vehicle.")]
    [SerializeField] private Transform taxi;
    [Tooltip("How close the taxi must be to a pickup point / the train")]
    [SerializeField] private float taxiRange = 15f;

    private int nextPickupIndex;
    private int deliveredCount;

    public bool IsCarryingCargo { get; private set; }
    public int TotalCargo => cargoPickups.Length;
    public int DeliveredCount => deliveredCount;

    // True once the train has started leaving - no more deliveries
    public bool HasTrainDeparted { get; private set; }

    // Deliveries only count while the train is waiting at the platform
    public bool IsTrainAtPlatform => train == null || train.IsWaitingAtStop;

    // True if no taxi is assigned, or the taxi is parked within range of the point
    public bool IsTaxiNear(Vector3 point)
    {
        return taxi == null || Vector3.Distance(taxi.position, point) <= taxiRange;
    }

    private void Awake()
    {
        Instance = this;
    }

    // Called by MissionBriefing once the player has read the
    // instructions and the 3-2-1 countdown finishes.
    public void StartMission()
    {
        CurrentState = MissionState.InProgress;
        deliveredCount = 0;
        nextPickupIndex = 0;
        IsCarryingCargo = false;
        HasTrainDeparted = false;

        // Only the first pickup point is live; the rest wait their turn
        for (int i = 0; i < cargoPickups.Length; i++)
        {
            cargoPickups[i].gameObject.SetActive(i == 0);
        }

        // Train rolls out of the tunnel; its "On Arrived At Stop" event
        // starts TrainTimer once it's waiting at the platform.
        if (train != null) train.BeginMoving();
    }

    public void OnCargoPickedUp()
    {
        if (CurrentState != MissionState.InProgress) return;

        IsCarryingCargo = true;
        nextPickupIndex++;
        Debug.Log($"Cargo picked up ({nextPickupIndex}/{TotalCargo})");
    }

    public void OnCargoDelivered()
    {
        if (CurrentState != MissionState.InProgress || !IsCarryingCargo) return;

        IsCarryingCargo = false;
        deliveredCount++;
        Debug.Log($"Items delivered: {deliveredCount}/{TotalCargo}");

        if (deliveredCount >= TotalCargo)
        {
            CompleteMission();
        }
        else
        {
            cargoPickups[nextPickupIndex].gameObject.SetActive(true);
        }
    }

    // Called by TrainTimer when the countdown hits zero. The train
    // leaves; the mission only fails once it's fully in the tunnel.
    public void OnTrainTimerExpired()
    {
        if (CurrentState != MissionState.InProgress) return;

        HasTrainDeparted = true;
        Debug.Log("Time's up - the train is leaving!");

        if (train != null) train.Depart();
        else FailMission("Failed to deliver all cargo"); // no train assigned
    }

    // Wired to TrainGates' "On All Passed Exit" event - the whole
    // train has vanished into the tunnel.
    public void OnTrainGone()
    {
        if (CurrentState != MissionState.InProgress) return;
        FailMission("Failed to deliver all cargo");
    }

    public void CompleteMission()
    {
        if (CurrentState != MissionState.InProgress) return;

        CurrentState = MissionState.Success;
        HasTrainDeparted = true;
        if (TrainTimer.Instance != null) TrainTimer.Instance.StopTimer();
        if (train != null) train.Depart(); // leaves with the cargo

        Debug.Log("Mission Complete!");
        GameManager.Instance.MarkMissionComplete(gameObject.scene.name); // saves progress
        MissionResultUI.Instance.ShowMessage("Mission Complete!");
        if (cleanup != null) cleanup.ApplySuccessState();
    }

    public void FailMission(string reason)
    {
        if (CurrentState != MissionState.InProgress) return;

        CurrentState = MissionState.Failed;
        HasTrainDeparted = true;
        if (TrainTimer.Instance != null) TrainTimer.Instance.StopTimer();
        if (train != null) train.Depart(); // e.g. failed by suspicion - train leaves anyway

        Debug.Log($"Mission Failed: {reason}");
        MissionResultUI.Instance.ShowFailure(reason); // shared fail screen, then back to Hub
    }
}