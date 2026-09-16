using UnityEngine;

public class Mission3Manager : MonoBehaviour
{
    public static Mission3Manager Instance { get; private set; }

    public enum MissionState
    {
        Setup, Searching, Investigating, WaitingForTrucks,
        TrucksAtFarm, Following, Delivering, Success, Failed
    }
    public MissionState CurrentState { get; private set; } = MissionState.Setup;

    [Tooltip("Total clues to find before the countdown can start")]
    [SerializeField] private int totalClues = 5;

    [Tooltip("Starts once all clues are found")]
    [SerializeField] private TruckArrivalCountdown truckCountdown;

    public bool BodyFound { get; private set; }
    public int CluesFound { get; private set; }
    public int TotalClues => totalClues;

    private void Awake()
    {
        Instance = this;
    }

    // Called by MissionBriefing once the player has read the
    // instructions and the 3-2-1 countdown finishes (same pattern as
    // Mission 1).
    public void StartMission()
    {
        CurrentState = MissionState.Searching;
        CluesFound = 0;
        BodyFound = false;
    }

    public void OnBodyFound()
    {
        if (CurrentState != MissionState.Searching || BodyFound) return;

        BodyFound = true;
        CurrentState = MissionState.Investigating;
        Debug.Log("Farmer's body discovered. Find the clues before the trucks arrive.");
    }

    public void OnClueFound()
    {
        if (CurrentState != MissionState.Investigating) return;

        CluesFound++;
        Debug.Log($"Clue found ({CluesFound}/{totalClues})");

        if (CluesFound >= totalClues)
        {
            CurrentState = MissionState.WaitingForTrucks;
            Debug.Log("All clues found. Hide before the trucks arrive!");
            if (truckCountdown != null) truckCountdown.StartCountdown();
        }
    }

    // Called by TruckArrivalCountdown once it hits zero
    public void OnCountdownFinished()
    {
        if (CurrentState != MissionState.WaitingForTrucks) return;

        CurrentState = MissionState.TrucksAtFarm;
        Debug.Log("The trucks have arrived.");
    }

    // Called by the lead MissionTruckFollower once it resumes after its pause
    public void OnTrucksLeavingFarm()
    {
        if (CurrentState != MissionState.TrucksAtFarm) return;

        CurrentState = MissionState.Following;
        Debug.Log("Trucks are leaving. Get in your car and follow them - don't get too close.");
    }

    // Hook point: called by a photo-capture trigger, next up
    public void OnPhotoTaken()
    {
        if (CurrentState != MissionState.Following) return;

        CurrentState = MissionState.Delivering;
        Debug.Log("Photo captured. Deliver it to the police.");
    }

    // Hook point: called by a police delivery trigger, next up
    public void OnEvidenceDelivered()
    {
        if (CurrentState != MissionState.Delivering) return;

        CurrentState = MissionState.Success;
        Debug.Log("Mission Complete!");
        MissionResultUI.Instance.ShowMessage("Mission Complete!");
    }

    public void FailMission(string reason)
    {
        if (CurrentState == MissionState.Success || CurrentState == MissionState.Failed) return;

        CurrentState = MissionState.Failed;
        Debug.Log($"Mission Failed: {reason}");
        GameManager.Instance.ReturnToHub();
    }
}
