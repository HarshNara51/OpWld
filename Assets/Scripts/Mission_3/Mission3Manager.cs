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

    [Header("Don't lose the trucks")]
    [Tooltip("Further than this from the lead truck (m) = a 'too far' warning")]
    [SerializeField] private float warnDistance = 120f;
    [Tooltip("Further than this for 'Lose Seconds' = mission failed, the trucks escaped")]
    [SerializeField] private float loseDistance = 200f;
    [SerializeField] private float loseSeconds = 5f;
    [Tooltip("After the trucks leave, no distance checks until the player is in a car or this many seconds have passed (running back to the car)")]
    [SerializeField] private float followGraceSeconds = 30f;

    public bool BodyFound { get; private set; }
    public int CluesFound { get; private set; }
    public int TotalClues => totalClues;
    public bool CarParkedOrNoSpot => parkingSpot == null || parkingSpot.CarParked;

    private bool trucksStopped;
    private float followStartTime, nextFarNote, tooFarFor;
    private Transform player;

    private void Awake()
    {
        Instance = this;
    }

    // While following: warn when falling too far behind, fail if the trucks get clean away
    private void Update()
    {
        if (CurrentState != MissionState.Following || trucksStopped || leadTruck == null) return;

        VehicleInteraction car = VehicleInteraction.Current;
        if (car == null && Time.time < followStartTime + followGraceSeconds) return; // still running back to the car

        Vector3 me;
        if (car != null) me = car.transform.position;
        else
        {
            if (player == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p == null) return; player = p.transform; }
            me = player.position;
        }

        Vector3 gap = leadTruck.transform.position - me; gap.y = 0f;
        float distance = gap.magnitude;

        if (distance > warnDistance && Time.time >= nextFarNote)
        {
            nextFarNote = Time.time + 8f;
            NotePopup.Show("You're too far away - the trucks are escaping! Don't lose them.", 3.5f);
        }

        tooFarFor = distance > loseDistance ? tooFarFor + Time.deltaTime : 0f;
        if (tooFarFor >= loseSeconds) FailMission("The trucks escaped");
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
                ObjectiveHUD.Set(BodyDiscovered ? "Photograph the body" : "Find the farmer's son");
                break;
            case MissionState.Investigating:
                ObjectiveHUD.Set(CluesFound < totalClues
                    ? $"Photograph the clues ({CluesFound}/{totalClues})"
                    : "Hide behind the truck!");
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
    // Walked up to the body (before the photo) - the objective now says to photograph it
    public bool BodyDiscovered { get; private set; }
    public void OnBodyDiscovered()
    {
        if (CurrentState != MissionState.Searching || BodyDiscovered) return;
        BodyDiscovered = true;
        UpdateObjective();
    }

    public void OnBodyFound()
    {
        if (CurrentState != MissionState.Searching || BodyFound) return;

        BodyFound = true;
        CurrentState = MissionState.Investigating;
        NotePopup.Show($"There must be more clues around the farm. Photograph all {totalClues} of them as evidence - before the trucks arrive.", 6f);
        UpdateObjective();

        // The countdown starts the lead truck itself, timed to reach the farm entrance at 00:00
        if (truckCountdown != null) truckCountdown.StartCountdown();
        else if (leadTruck != null) leadTruck.BeginMoving();
    }

    public void OnClueFound()
    {
        if (CurrentState != MissionState.Investigating) return;

        CluesFound++;

        if (CluesFound >= totalClues)
        {
            NotePopup.Show("That's everything. The trucks will be here any minute! Hide behind the truck - the HIDE HERE marker. Now!", 6f);
        }
        UpdateObjective();
    }

    // Called by TruckArrivalCountdown the moment the lead truck passes the
    // farm entrance (the countdown is timed so that's also 00:00).
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
        followStartTime = Time.time;
        tooFarFor = 0f;
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