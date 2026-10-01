using System.Collections.Generic;
using UnityEngine;

// Put this script directly on every drivable car (player car, taxi, ...).
// Works with any number of cars: only the closest car in range responds
// to E, and one key press can never enter/exit two cars at once.
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
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private bool isDriving = false;

    public bool IsDriving => isDriving;

    private void Awake()
    {
        // Make sure the car doesn't respond to input until someone actually gets in
        if (carControllerScript != null) carControllerScript.enabled = false;
    }

    private void OnEnable() => all.Add(this);

    private void OnDisable()
    {
        all.Remove(this);
        if (Current == this) Current = null;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(interactKey)) return;

        // Another car already handled this key press this frame
        if (Time.frameCount == lastSwitchFrame) return;

        if (isDriving)
        {
            ExitVehicle();
            return;
        }

        // Already driving a different car, or player missing/hidden
        if (Current != null || player == null || !player.gameObject.activeInHierarchy) return;

        float distance = Vector3.Distance(player.position, transform.position);
        if (distance <= interactionRange && IsClosestCarInRange(distance))
        {
            EnterVehicle();
        }
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