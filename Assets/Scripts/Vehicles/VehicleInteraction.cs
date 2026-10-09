using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Put this script directly on every drivable car (player car, taxi, ...).
// Works with any number of cars: only the closest car in range responds
// to E, and one key press can never enter/exit two cars at once.
// Optional: lock a car behind a puzzle (lockpick, hotwire...) - the
// player has to solve it the first time before they can get in.
public class VehicleInteraction : MonoBehaviour
{
    // The car the player is driving right now (null when on foot)
    public static VehicleInteraction Current { get; private set; }

    private static readonly List<VehicleInteraction> all = new List<VehicleInteraction>();
    private static int lastSwitchFrame = -1;

    [Header("References")]
    [SerializeField] private Transform player;                  // HumanDummy_F White
    [SerializeField] private MonoBehaviour carControllerScript;  // Prometeo's driving script component
    [SerializeField] private CameraOrbit cameraOrbit;             // the script on Main Camera
    [SerializeField] private Transform exitPoint;                 // empty child transform beside the driver door

    [Header("Settings")]
    [SerializeField] private float interactionRange = 3f;

    [Header("Locked car (optional)")]
    [Tooltip("Leave empty for a normal car. Assign a puzzle (e.g. LockpickPuzzle) to make it locked until solved.")]
    [SerializeField] private MonoBehaviour unlockPuzzle;

    [Tooltip("Fires once, the first time the player gets in (e.g. start the bomb's reveal timer)")]
    public UnityEvent onFirstEnter;

    private bool disabledForGood;
    private float nextWreckNoteTime;
    private ICarUnlockPuzzle puzzle;
    private bool unlocked;
    private bool hasEntered;

    private bool isDriving = false;

    public bool IsDriving => isDriving;

    private void Awake()
    {
        // Make sure the car doesn't respond to input until someone actually gets in
        if (carControllerScript != null) carControllerScript.enabled = false;

        puzzle = unlockPuzzle as ICarUnlockPuzzle;
        if (unlockPuzzle != null && puzzle == null)
            Debug.LogWarning($"{name}: Unlock Puzzle doesn't implement ICarUnlockPuzzle.");
        unlocked = puzzle == null;
    }

    private void OnEnable() => all.Add(this);

    private void OnDisable()
    {
        all.Remove(this);
        if (Current == this) Current = null;
    }

    // Wrecked (e.g. the bomb went off): stops driving and can't be entered again
    public void DisableForGood()
    {
        disabledForGood = true;
        if (carControllerScript != null) carControllerScript.enabled = false;
    }

    private void Update()
    {
        if (disabledForGood)
        {
            // Trying to get into the wreck: remind the player why nothing happens
            if (GameKeys.Down(GameAction.EnterExitCar) && Time.time >= nextWreckNoteTime &&
                player != null && player.gameObject.activeInHierarchy &&
                Vector3.Distance(player.position, transform.position) <= interactionRange)
            {
                nextWreckNoteTime = Time.time + 3f;
                NotePopup.Show("It's wrecked. This car won't drive again.", 2.5f);
            }
            return;
        }
        if (!GameKeys.Down(GameAction.EnterExitCar)) return;

        // Another car already handled this key press this frame
        if (Time.frameCount == lastSwitchFrame) return;

        if (isDriving)
        {
            ExitVehicle();
            return;
        }

        // Already driving a different car, or player missing/hidden/dead
        if (Current != null || player == null || !player.gameObject.activeInHierarchy || PlayerDeath.IsDead) return;

        float distance = Vector3.Distance(player.position, transform.position);
        if (distance <= interactionRange && IsClosestCarInRange(distance))
        {
            if (!unlocked)
            {
                if (!puzzle.IsOpen)
                {
                    lastSwitchFrame = Time.frameCount;
                    puzzle.Open(OnPuzzleSolved);
                }
                return;
            }

            EnterVehicle();
        }
    }

    private void OnPuzzleSolved()
    {
        unlocked = true;
        if (Current == null && player != null && player.gameObject.activeInHierarchy) EnterVehicle();
    }

    // When two cars are parked close together, only the nearest one gets entered
    private bool IsClosestCarInRange(float myDistance)
    {
        foreach (VehicleInteraction other in all)
        {
            if (other == this || other.player == null) continue;

            float d = Vector3.Distance(other.player.position, other.transform.position);
            if (d <= other.interactionRange && d < myDistance) return false;
        }
        return true;
    }

    private void EnterVehicle()
    {
        isDriving = true;
        Current = this;
        lastSwitchFrame = Time.frameCount;

        player.gameObject.SetActive(false);   // hides + disables all player scripts/collider in one go
        carControllerScript.enabled = true;
        cameraOrbit.SetTarget(transform);

        if (!hasEntered)
        {
            hasEntered = true;
            onFirstEnter?.Invoke();
        }
    }

    private void ExitVehicle()
    {
        isDriving = false;
        if (Current == this) Current = null;
        lastSwitchFrame = Time.frameCount;

        carControllerScript.enabled = false;

        player.position = exitPoint.position;
        player.rotation = exitPoint.rotation;
        player.gameObject.SetActive(true);

        cameraOrbit.SetTarget(player);
    }
}