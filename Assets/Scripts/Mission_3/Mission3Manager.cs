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

    [Header("Guidance")]
    [Tooltip("Seconds after the trucks leave before the 'keep following' reminder")]
    [SerializeField] private float followReminderDelay = 12f;
    [Tooltip("Seconds between delivering the evidence and the Mission Passed screen")]
    [SerializeField] private float passedScreenDelay = 6f;

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

    // Called by BodyDiscoveryZone when the body is photographed -
    // this is the moment everything else kicks off.
    public void OnBodyFound()
    {
        if (CurrentState != MissionState.Searching || BodyFound) return;

        BodyFound = true;
        CurrentState = MissionState.Investigating;
        Debug.Log("Photograph captured.");
        Debug.Log("Capture all 5 clues before the trucks arrive, and hide immediately!");
        NotePopup.Show($"Photograph all {totalClues} clues before the trucks arrive... then hide!");

        if (truckCountdown != null) truckCountdown.StartCountdown();
        if (leadTruck != null) leadTruck.BeginMoving();
    }

    public void OnClueFound()
    {
        if (CurrentState != MissionState.Investigating) return;

        CluesFound++;
        Debug.Log($"Photograph captured. Clue {CluesFound}/{totalClues}");

        if (CluesFound >= totalClues)
        {
            NotePopup.Show("That's everything. The trucks will be here any minute. Hide. Now.");
        }
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
        NotePopup.Show("The trucks have arrived. Stay hidden...", 4f);
    }

    // Wired to the lead truck's SplineVehicle "On Departed Stop" event
    public void OnTrucksLeavingFarm()
    {
        if (CurrentState != MissionState.TrucksAtFarm) return;

        CurrentState = MissionState.Following;
        Debug.Log("Trucks are leaving. Get in your car and follow them - don't get too close.");
        NotePopup.Show("They're leaving. Get in your car and follow them... but keep your distance.");
        if (suspicion != null) suspicion.SetActive(true);
        StartCoroutine(FollowReminder());
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
        NotePopup.Show($"They've stopped. Get a clear shot of the trucks - press {CameraKey()}, then left click.", 6f);
    }

    private static string CameraKey()
    {
        return CameraMode.Instance != null ? CameraMode.Instance.ToggleKey.ToString() : "P";
    }

    // Called by PhotoCaptureZone when the trucks are photographed
    public void OnPhotoTaken()
    {
        if (CurrentState != MissionState.Following) return;

        CurrentState = MissionState.Delivering;
        Debug.Log("Final photo captured! Now deliver it to the police station.");
        NotePopup.Show("Got them red-handed. Get this to the police station.");
        if (suspicion != null) suspicion.SetActive(false);
    }

    // Called by PoliceDeliveryZone
    public void OnEvidenceDelivered()
    {
        if (CurrentState != MissionState.Delivering) return;

        CurrentState = MissionState.Success;
        Debug.Log("All photos and evidence delivered.");
        Debug.Log("Mission Complete!");
        GameManager.Instance.MarkMissionComplete(gameObject.scene.name); // saves progress
        NotePopup.Show("Evidence handed over. The police will take it from here... the farmer's son will get justice.", passedScreenDelay - 0.5f);
        MissionResultUI.Instance.ShowSuccess(null, passedScreenDelay);
    }

    public void FailMission(string reason)
    {
        if (CurrentState == MissionState.Success || CurrentState == MissionState.Failed) return;

        CurrentState = MissionState.Failed;
        if (truckCountdown != null) truckCountdown.StopCountdown();
        Debug.Log($"Mission Failed: {reason}");
        MissionResultUI.Instance.ShowFailure(reason); // shared fail screen, then back to Hub
    }
}