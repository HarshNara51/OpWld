using UnityEngine;

// Put this on the target stealable car instead of VehicleInteraction.
// First entry requires solving stealPuzzle; after that it behaves
// exactly like a normal car.
public class StealableCarInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private MonoBehaviour carControllerScript;
    [SerializeField] private CameraOrbit cameraOrbit;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private PuzzleMinigame stealPuzzle;

    [Header("Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private bool isDriving;
    private bool hasBeenStolen;

    private void Awake()
    {
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
            if (!hasBeenStolen && stealPuzzle != null)
            {
                stealPuzzle.Open(); // EnterVehicle() runs once solved, via the puzzle's OnSolved event
            }
            else
            {
                EnterVehicle();
            }
        }
    }

    // Public so PuzzleMinigame's OnSolved event can call it directly
    public void EnterVehicle()
    {
        hasBeenStolen = true;
        isDriving = true;
        player.gameObject.SetActive(false);
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
