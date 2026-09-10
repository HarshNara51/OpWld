using UnityEngine;

// Put this on any persistent object (e.g. your Managers prefab, or the player).
// Requires: the car tagged "PlayerCar", the player tagged "Player".
public class VehicleSummon : MonoBehaviour
{
    [Tooltip("Key that summons the car")]
    public KeyCode summonKey = KeyCode.V;

    [Tooltip("How far in front of the player the car appears")]
    public float distanceInFront = 4f;

    [Tooltip("Layers considered 'ground' for placing the car. Defaults to everything.")]
    public LayerMask groundMask = ~0;

    private Transform car;
    private Rigidbody carRigidbody;
    private Transform player;
    private Camera playerCamera;

    private void Start()
    {
        GameObject carObj = GameObject.FindGameObjectWithTag("PlayerCar");
        if (carObj != null)
        {
            car = carObj.transform;
            carRigidbody = carObj.GetComponent<Rigidbody>();
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        playerCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetKeyDown(summonKey))
        {
            SummonCar();
        }
    }

    private void SummonCar()
    {
        if (car == null || player == null || playerCamera == null) return;

        // Don't summon while the player is already driving it.
        // Adjust this check to match however VehicleInteraction actually
        // disables the player object when entering the car.
        if (!player.gameObject.activeInHierarchy) return;

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
