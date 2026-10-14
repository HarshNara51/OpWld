using UnityEngine;

// Put this on any persistent object (e.g. your Managers prefab, or the player).
// Requires: the car tagged "PlayerCar", the player tagged "Player".
public class VehicleSummon : MonoBehaviour
{

    [Tooltip("How far in front of the player the car appears")]
    public float distanceInFront = 4f;

    [Tooltip("Layers considered 'ground' for placing the car. Defaults to everything.")]
    public LayerMask groundMask = ~0;

    // A mission can switch summoning off (e.g. Farm: the car must stay parked).
    // Tied to the mission object, so it switches back on by itself when that
    // scene unloads - this script lives on Managers and outlives scenes.
    private static Object blocker;
    private static string blockedMessage;

    public static void Block(Object by, string message) { blocker = by; blockedMessage = message; }
    public static void Unblock(Object by) { if (blocker == by) blocker = null; }

    private Transform car;
    private Rigidbody carRigidbody;
    private Transform player;
    private Camera playerCamera;

    // This lives on the Managers object, which carries over between scenes,
    // so the car/player/camera are looked up again whenever they're gone
    // (a new scene was loaded) instead of only once at the start.
    private void FindSceneObjects()
    {
        if (car == null)
        {
            carRigidbody = null;
            foreach (GameObject carObj in GameObject.FindGameObjectsWithTag("PlayerCar"))
            {
                // The real car, not a look-alike dummy (e.g. on the tow truck)
                if (carObj.GetComponent<VehicleInteraction>() == null) continue;
                car = carObj.transform;
                carRigidbody = carObj.GetComponent<Rigidbody>();
                break;
            }
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            player = playerObj != null ? playerObj.transform : null;
        }

        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void Update()
    {
        if (GameKeys.Down(GameAction.SummonCar))
        {
            SummonCar();
        }
    }

    private void SummonCar()
    {
        FindSceneObjects();
        if (car == null || player == null || playerCamera == null) return;

        // Don't summon while the player is already driving it.
        // Adjust this check to match however VehicleInteraction actually
        // disables the player object when entering the car.
        if (!player.gameObject.activeInHierarchy) return;

        if (blocker != null) // Unity null once the mission's scene is gone
        {
            NotePopup.Show(blockedMessage, 3f);
            return;
        }

        Vector3 forward = playerCamera.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 targetPos = player.position + forward * distanceInFront;

        // Snap to the ground so the car doesn't spawn floating or buried.
        Vector3 rayOrigin = targetPos + Vector3.up * 10f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 40f, groundMask))
        {
            targetPos.y = hit.point.y;
        }

        car.position = targetPos;
        car.rotation = Quaternion.LookRotation(forward, Vector3.up);

        if (carRigidbody != null)
        {
            carRigidbody.linearVelocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
        }
    }
}
