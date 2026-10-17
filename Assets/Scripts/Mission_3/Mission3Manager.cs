using System.Collections;
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
    [SerializeField] private SplineVehicle leadTruck;

    [Tooltip("Player must be inside this when the trucks arrive, and stay inside until they leave")]
    [SerializeField] private HideZone hideZone;

    [Tooltip("Only turned on during the Following phase, so it can't trigger while hiding near the parked trucks")]
    [SerializeField] private SuspicionManager suspicion;

    [Tooltip("The car must be parked here before the body photo counts. Summoning the car (V) is off until the trucks are photographed, so the player walks back to it.")]
    [SerializeField] private ParkingSpot parkingSpot;

    [Header("Guidance")]
    [Tooltip("Seconds after the trucks leave before the 'keep following' reminder")]
    [SerializeField] private float followReminderDelay = 12f;
    [Tooltip("Seconds between delivering the evidence and the Mission Passed screen")]
    [SerializeField] private float passedScreenDelay = 6f;

    public bool BodyFound { get; private set; }
    public int CluesFound { get; private set; }
    public int TotalClues => totalClues;
    public bool CarParkedOrNoSpot => parkingSpot == null || parkingSpot.CarParked;

    private bool trucksStopped;

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
        trucksStopped = false;
        if (parkingSpot != null)
            VehicleSummon.Block(this, "You can't call your car on this job. Walk back to it.");
        UpdateObjective();
    }

    // The top-left objective line - only ever shows the current step
    private void UpdateObjective()
    {
        switch (CurrentState)
        {
            case MissionState.Searching:
                ObjectiveHUD.Set("Find the farmer's son");
                break;
            case MissionState.Investigating:
                ObjectiveHUD.Set(CluesFound < totalClues
                    ? $"Photograph the clues ({CluesFound}/{totalClues})"
                    : "Hide!");
                break;
            case MissionState.TrucksAtFarm:
                ObjectiveHUD.Set("Stay hidden");
                break;
            case MissionState.Following:
                ObjectiveHUD.Set(trucksStopped
                    ? "Photograph the trucks"
                    : "Follow the trucks - keep your distance");
                break;
            case MissionState.Delivering:
                ObjectiveHUD.Set("Take the evidence to the police station");
                break;
            default:
                ObjectiveHUD.Clear();
                break;
        }
    }

    // Called by BodyDiscoveryZone when the body is photographed -
    // this is the moment everything else kicks off.
    public void OnBodyFound()
    {
        if (CurrentState != MissionState.Searching || BodyFound) return;

        BodyFound = true;
        CurrentState = MissionState.Investigating;
        NotePopup.Show($"Photograph all {totalClues} clues before the trucks arrive... then hide!");
        UpdateObjective();

        if (truckCountdown != null) truckCountdown.StartCountdown();
        if (leadTruck != null) leadTruck.BeginMoving();
    }

    public void OnClueFound()
    {
        if (CurrentState != MissionState.Investigating) return;

        CluesFound++;

        if (CluesFound >= totalClues)
        {
            NotePopup.Show("That's everything. The trucks will be here any minute. Hide. Now.");
        }
        UpdateObjective();
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
        NotePopup.Show("The trucks have arrived. Stay hidden...", 4f);
        UpdateObjective();
    }

    // Wired to the lead truck's SplineVehicle "On Departed Stop" event
    public void OnTrucksLeavingFarm()
    {
        if (CurrentState != MissionState.TrucksAtFarm) return;

        CurrentState = MissionState.Following;
        NotePopup.Show(parkingSpot != null
            ? "They're leaving. Run back to your parked car and follow them... but keep your distance."
            : "They're leaving. Get in your car and follow them... but keep your distance.");
        if (suspicion != null) suspicion.SetActive(true);
        StartCoroutine(FollowReminder());
        UpdateObjective();
    }

    private IEnumerator FollowReminder()
    {
        yield return new WaitForSeconds(followReminderDelay);
        if (CurrentState != MissionState.Following) yield break;

        NotePopup.Show($"Keep following them, slowly. Wait for them to come to a complete stop, " +
                       $"then take a photo ({CameraKey()}) and deliver it to the police station.", 7f);
    }

    // Wire to the lead truck's SplineVehicle "On Reached End" event
    public void OnTrucksStopped()
    {
        if (CurrentState != MissionState.Following) return;
        trucksStopped = true;
        UpdateObjective();
        NotePopup.Show($"They've stopped. Get a clear shot of the trucks - press {CameraKey()}, then left click.", 6f);
    }

    private static string CameraKey()
    {
        return GameKeys.Label(GameAction.PhotoMode);
    }

    // Called by PhotoCaptureZone when the trucks are photographed
    public void OnPhotoTaken()
    {
        if (CurrentState != MissionState.Following) return;

        CurrentState = MissionState.Delivering;
        NotePopup.Show("Got them red-handed. Get this to the police station.");
        if (suspicion != null) suspicion.SetActive(false);
        VehicleSummon.Unblock(this);
        UpdateObjective();
    }

    // Called by PoliceDeliveryZone
    public void OnEvidenceDelivered()
    {
        if (CurrentState != MissionState.Delivering) return;

        CurrentState = MissionState.Success;
        UpdateObjective();
        GameManager.Instance.MarkMissionComplete(gameObject.scene.name); // saves progress
        NotePopup.Show("Evidence handed over. The police will take it from here... the farmer's son will get justice.", passedScreenDelay - 0.5f);
        MissionResultUI.Instance.ShowSuccess(null, passedScreenDelay);
    }

    public void FailMission(string reason)
    {
        if (CurrentState == MissionState.Success || CurrentState == MissionState.Failed) return;

        CurrentState = MissionState.Failed;
        UpdateObjective();
        VehicleSummon.Unblock(this);
        if (truckCountdown != null) truckCountdown.StopCountdown();
        MissionResultUI.Instance.ShowFailure(reason); // shared fail screen, then back to Hub
    }
}