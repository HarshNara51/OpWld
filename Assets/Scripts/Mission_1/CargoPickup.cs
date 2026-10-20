using UnityEngine;

// Put this on each cargo pickup gizmo in Mission1_Railway.
// Requires a trigger Collider. The prompt shows while the player - on foot,
// or driving - is inside it (checked every frame: getting into the taxi
// while standing in the zone never fires a trigger "exit").
public class CargoPickup : MonoBehaviour
{
    private Collider zone;
    private Transform player;

    private void Awake() => zone = GetComponent<Collider>();

    // On foot: the player. Driving: the car.
    private bool PlayerInZone()
    {
        Vector3 at;
        if (VehicleInteraction.Current != null) at = VehicleInteraction.Current.transform.position;
        else
        {
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) return false;
                player = p.transform;
            }
            if (!player.gameObject.activeInHierarchy) return false;
            at = player.position;
        }
        if (zone == null) return false;
        Bounds b = zone.bounds;
        b.Expand(new Vector3(3f, 6f, 3f)); // small boxes: 1.5 m of reach around them (the old trigger fired on touch)
        return b.Contains(at);
    }

    private void Update()
    {
        bool playerInRange = PlayerInZone();
        if (playerInRange) NotePopup.Prompt(this, $"Press {NotePopup.Key(GameAction.Interact)} to pick up the cargo");
        else NotePopup.ClearPrompt(this);

        if (playerInRange && GameKeys.Down(GameAction.Interact))
        {
            if (!Mission1Manager.Instance.IsTaxiNear(transform.position))
            {
                NotePopup.Show("Bring the taxi here first - the cargo goes in the boot.", 3f);
                return;
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.cargoPickup);
            Mission1Manager.Instance.OnCargoPickedUp();
            gameObject.SetActive(false);
        }
    }

    // Switched off (used up, picked up...) - take the prompt away with it
    private void OnDisable() => NotePopup.ClearPrompt(this);
}
