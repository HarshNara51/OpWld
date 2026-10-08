using UnityEngine;

public class Mission2Manager : MonoBehaviour, IFailableMission
{
    public static Mission2Manager Instance { get; private set; }

    public enum MissionState { Setup, InProgress, Escaping, Success, Failed }
    public MissionState CurrentState { get; private set; } = MissionState.Setup;

    public bool TargetEliminated { get; private set; }

    [Tooltip("Optional - toggles mission-only objects off and reveal objects on when the mission succeeds")]
    [SerializeField] private MissionCleanup cleanup;

    [Header("Objectives (HUD)")]
    [Tooltip("The guards outside the bedroom - used for the 'Take down the guards (x/2)' objective")]
    [SerializeField] private GameObject[] guards;

    [Header("Escape")]
    [Tooltip("Starts when the item is grabbed. Wire On Midpoint to OnCopsDispatched, On Finished to OnEscapeTimeUp")]
    [SerializeField] private MissionCountdown escapeCountdown;

    [Tooltip("Cop cars - hidden until dispatched halfway through the countdown (SplineVehicle, Auto Start OFF)")]
    [SerializeField] private SplineVehicle[] copCars;

    [Tooltip("Their lights - switched on as they drive in")]
    [SerializeField] private CopLights[] copLights;

    [Tooltip("Center of the hotel. Once the cops arrive, being within Safe Distance of here = busted. Far enough = safe, but you still need to reach the vantage point to pass.")]
    [SerializeField] private Transform hotelCenter;
    [SerializeField] private float safeDistance = 60f;

    [TextArea(2, 4)]
    [SerializeField]
    private string escapeMessage =
        "Quick! You've got the item. Get out of the hotel and reach the vantage point before the cops arrive!";

    private bool copsDispatched;
    private bool copsArrived;

    private void Awake()
    {
        Instance = this;
        SetCopCarsVisible(false); // nowhere to be seen until they're called in
    }

    // Called by MissionBriefing once the player has read the
    // instructions and the 3-2-1 countdown finishes.
    public void StartMission()
    {
        CurrentState = MissionState.InProgress;
        TargetEliminated = false;
        UpdateObjective();
    }

    // Keeps the top-left objective line in sync with the mission
    private void UpdateObjective()
    {
        switch (CurrentState)
        {
            case MissionState.InProgress:
                if (!TargetEliminated)
                {
                    int total = guards != null ? guards.Length : 0;
                    int down = 0;
                    if (guards != null)
                        foreach (GameObject g in guards) if (g == null || !g.activeInHierarchy) down++;

                    ObjectiveHUD.Set(total > 0 && down < total
                        ? $"Take down the guards ({down}/{total})"
                        : "Eliminate the target");
                }
                else
                {
                    ObjectiveHUD.Set("Grab the item");
                }
                break;

            case MissionState.Escaping:
                ObjectiveHUD.Set("Reach the vantage point");
                break;

            default:
                ObjectiveHUD.Clear();
                break;
        }
    }

    // Called by the mob leader's takedown
    public void OnTargetEliminated()
    {
        if (CurrentState != MissionState.InProgress) return;

        TargetEliminated = true;
        Debug.Log("Target eliminated. Grab the item and get out.");
        NotePopup.Show("Target down. Grab the item and get out of here.", 4f);
    }

    // Called by the item pickup - only works after the target is down.
    // Starts the escape instead of ending the mission.
    public void OnItemRecovered()
    {
        if (CurrentState != MissionState.InProgress || !TargetEliminated) return;

        CurrentState = MissionState.Escaping;
        Debug.Log("Item recovered. Escape!");
        NotePopup.Show(escapeMessage, 6f);

        if (escapeCountdown != null) escapeCountdown.StartCountdown();
        else OnEscapeTimeUp(); // no countdown assigned - cops come straight away
    }

    // Wired to the escape countdown's On Midpoint - the cops appear
    // on their splines and start driving in, lights on.
    public void OnCopsDispatched()
    {
        if (CurrentState != MissionState.Escaping || copsDispatched) return;
        copsDispatched = true;

        Debug.Log("Cops dispatched!");
        NotePopup.Show("Sirens in the distance... the cops are on their way!", 3f);

        SetCopCarsVisible(true);

        foreach (CopLights lights in copLights)
        {
            if (lights != null) lights.TurnOn();
        }

        foreach (SplineVehicle car in copCars)
        {
            if (car != null) car.BeginMoving();
        }
    }

    // Wired to the escape countdown's On Finished - time's up, they're here
    public void OnEscapeTimeUp()
    {
        if (CurrentState != MissionState.Escaping) return;

        if (!copsDispatched) OnCopsDispatched();
        Debug.Log("Time's up - the cops are here!");
        CopsCheckForPlayer();
    }

    private void SetCopCarsVisible(bool visible)
    {
        if (copCars == null) return;
        foreach (SplineVehicle car in copCars)
        {
            if (car != null) car.gameObject.SetActive(visible);
        }
    }

    // Wired to a cop car's SplineVehicle "On Arrived At Stop" (its last knot)
    public void OnCopsArrived()
    {
        if (CurrentState != MissionState.Escaping) return;
        CopsCheckForPlayer();
    }

    // The cops are here: near the hotel = busted. Far enough = safe for
    // now, but the mission only passes at the vantage point.
    private void CopsCheckForPlayer()
    {
        if (!PlayerIsFarEnough())
        {
            FailMission("Busted");
            return;
        }

        if (!copsArrived)
        {
            copsArrived = true;
            NotePopup.Show("The cops are swarming the hotel. Stay away from it and get to the vantage point!", 4f);
        }
    }

    // Once the cops are at the hotel, wandering back near it gets you caught
    private void Update()
    {
        UpdateObjective(); // cheap; only changes the HUD when the text changes

        if (CurrentState == MissionState.Escaping && copsArrived && !PlayerIsFarEnough())
        {
            FailMission("Busted");
        }
    }

    private bool PlayerIsFarEnough()
    {
        if (hotelCenter == null) return false;

        Transform target = null;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.activeInHierarchy) target = player.transform;
        else if (VehicleInteraction.Current != null) target = VehicleInteraction.Current.transform;
        if (target == null) return false;

        Vector3 offset = target.position - hotelCenter.position;
        offset.y = 0f;
        return offset.magnitude >= safeDistance;
    }

    // Wired to the vantage point's ReachZone "On Reached"
    public void OnEscaped()
    {
        if (CurrentState != MissionState.Escaping) return;

        CurrentState = MissionState.Success;
        if (escapeCountdown != null) escapeCountdown.StopCountdown();

        Debug.Log("Escaped. Mission Complete!");
        GameManager.Instance.MarkMissionComplete(gameObject.scene.name); // saves progress
        NotePopup.Show("You made it out. Clean getaway.", 3f);
        MissionResultUI.Instance.ShowSuccess(null, 2f);
        if (cleanup != null) cleanup.ApplySuccessState();
    }

    public void FailMission(string reason)
    {
        if (CurrentState == MissionState.Success || CurrentState == MissionState.Failed) return;

        CurrentState = MissionState.Failed;
        if (escapeCountdown != null) escapeCountdown.StopCountdown();

        Debug.Log($"Mission Failed: {reason}");
        MissionResultUI.Instance.ShowFailure(reason); // shared fail screen, then back to Hub
    }
}