using UnityEngine;

// Put this on a trigger zone (e.g. the hotel's bedroom door, and
// another one inside the bedroom pointing back). Press the key to
// teleport the player to Destination. Uses B, matching your gizmo
// interaction convention.
public class TeleportPoint : MonoBehaviour
{
    [SerializeField] private Transform destination;
    [SerializeField] private KeyCode interactKey = KeyCode.B;

    private bool playerInRange;
    private Transform player;

    private void Awake()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log($"Press {interactKey} to enter");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }

    private void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactKey) && player != null && destination != null)
        {
            // CharacterController can silently reject a direct position
            // change while enabled - disabling it around the teleport
            // is the standard, reliable way around that.
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.position = destination.position;
            player.rotation = destination.rotation;

            if (cc != null) cc.enabled = true;

            playerInRange = false; // don't instantly re-trigger if the destination sits near another one
        }
    }
}
