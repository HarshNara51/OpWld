using UnityEngine;

// Put this script directly on the car (e.g. "Prometheus")
public class VehicleInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;                  // HumanDummy_F White
    [SerializeField] private MonoBehaviour carControllerScript;  // Prometeo's driving script component
    [SerializeField] private CameraOrbit cameraOrbit;             // the script on Main Camera
    [SerializeField] private Transform exitPoint;                 // empty child transform beside the driver door

    [Header("Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private bool isDriving = false;

    private void Awake()
    {
        // Make sure the car doesn't respond to input until someone actually gets in
        if (carControllerScript != null) carControllerScript.enabled = false;
    }

    private void Update()
    {
        if (isDriving)
        {
            if (Input.GetKeyDown(interactKey))
                ExitVehicle();
            return;
        }

        float distance = Vector3.Distance(player.position, transform.position);
        if (distance <= interactionRange && Input.GetKeyDown(interactKey))
        {
            EnterVehicle();
        }
    }

    private void EnterVehicle()
    {
        isDriving = true;

        player.gameObject.SetActive(false);   // hides + disables all player scripts/collider in one go
        carControllerScript.enabled = true;
        cameraOrbit.SetTarget(transform);
    }

    private void ExitVehicle()
    {
        isDriving = false;

        carControllerScript.enabled = false;

        player.position = exitPoint.position;
        player.rotation = exitPoint.rotation;
        player.gameObject.SetActive(true);

        cameraOrbit.SetTarget(player);
    }
}