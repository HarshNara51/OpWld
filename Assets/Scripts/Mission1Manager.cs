using UnityEngine;

public class Mission1Manager : MonoBehaviour
{
    public static Mission1Manager Instance { get; private set; }

    public enum MissionState { Setup, InProgress, Success, Failed }
    public MissionState CurrentState { get; private set; } = MissionState.Setup;

    [Tooltip("All cargo pickup points in this mission, in the order they should unlock")]
    [SerializeField] private CargoPickup[] cargoPickups;

    private int nextPickupIndex;
    private int deliveredCount;

    public bool IsCarryingCargo { get; private set; }
    public int TotalCargo => cargoPickups.Length;
    public int DeliveredCount => deliveredCount;

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

        // Only the first pickup point is live; the rest wait their turn
        for (int i = 0; i < cargoPickups.Length; i++)
        {
            cargoPickups[i].gameObject.SetActive(i == 0);
        }
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

    public void CompleteMission()
    {
        if (CurrentState != MissionState.InProgress) return;

        CurrentState = MissionState.Success;
        Debug.Log("Mission Complete!");
        GameManager.Instance.ReturnToHub();
    }

    public void FailMission(string reason)
    {
        if (CurrentState != MissionState.InProgress) return;

        CurrentState = MissionState.Failed;
        Debug.Log($"Mission Failed: {reason}");
        GameManager.Instance.ReturnToHub();
    }
}
