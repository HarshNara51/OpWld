using UnityEngine;

public class Mission3Manager : MonoBehaviour, IFailableMission
{
    public static Mission3Manager Instance { get; private set; }

    public enum MissionState
    {
        Setup, Searching, Investigating, TrucksAtFarm,
        Following, Delivering, Success, Failed
    }
    public MissionState CurrentState { get; private set; } = MissionState.Setup;

    [Tooltip("Total clues to find before the trucks arrive")]
    [SerializeField] private int totalClues = 5;

    [Tooltip("Starts the instant the body is photographed")]
    [SerializeField] private TruckArrivalCountdown truckCountdown;

    [Tooltip("Starts moving the instant the body is photographed - same moment as the countdown")]
    [SerializeField] private MissionTruckFollower leadTruck;

    [Tooltip("Player must be inside this when the trucks arrive, and stay inside until they leave")]
    [SerializeField] private HideZone hideZone;

    [Tooltip("Only turned on during the Following phase, so it can't trigger while hiding near the parked trucks")]
    [SerializeField] private SuspicionManager suspicion;

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

    // Called by BodyDiscoveryZone when the player presses the key at
    // the body - this is the moment everything else kicks off.
    public void OnBodyFound()
    {
        if (CurrentState != MissionState.Searching || BodyFound) return;

        BodyFound = true;
        CurrentState = MissionState.Investigating;
        Debug.Log("Photograph captured.");
        Debug.Log("Capture all 5 clues before the trucks arrive, and hide immediately!");

        if (truckCountdown != null) truckCountdown.StartCountdown();
        if (leadTruck != null) leadTruck.BeginMoving();
    }

    public void OnClueFound()
    {
        if (CurrentState != MissionState.Investigating) return;

        CluesFound++;
        Debug.Log($"Photograph captured. Clue {CluesFound}/{totalClues}");
    }

    // Called by TruckArrivalCountdown at zero - this is also exactly
    // when the trucks reach the farm entrance.
    public void OnCountdownFinished()
    {
        if (CurrentState != MissionState.Investigating) return;

        if (CluesFound < totalClues)
        {
            FailMission("Spotted");
            return;
        }

        if (hideZone != null && !hideZone.PlayerInside)
        {
            FailMission("Spotted");
            return;
        }

        CurrentState = MissionState.TrucksAtFarm;
        Debug.Log("The trucks have arrived. Stay hidden.");
    }

    // Called by the lead MissionTruckFollower once it resumes after its pause
    public void OnTrucksLeavingFarm()
    {
        if (CurrentState != MissionState.TrucksAtFarm) return;

        CurrentState = MissionState.Following;
        Debug.Log("Trucks are leaving. Get in your car and follow them - don't get too close.");
        if (suspicion != null) suspicion.SetActive(true);
    }

    // Hook point: called by a photo-capture trigger, next up
    public void OnPhotoTaken()
    {
        if (CurrentState != MissionState.Following) return;

        CurrentState = MissionState.Delivering;
        Debug.Log("Final photo captured! Now deliver it to the police station.");
        if (suspicion != null) suspicion.SetActive(false);
    }

    // Hook point: called by a police delivery trigger, next up
    public void OnEvidenceDelivered()
    {
        if (CurrentState != MissionState.Delivering) return;

        CurrentState = MissionState.Success;
        Debug.Log("All photos and evidence delivered.");
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