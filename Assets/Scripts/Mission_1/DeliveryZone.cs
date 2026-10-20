using UnityEngine;

// Put this on the train/delivery gizmo in Mission1_Railway.
// Requires a trigger Collider. The unload prompt shows while the player -
// on foot, or driving - is inside it (checked every frame: getting into the
// taxi while standing in the zone never fires a trigger "exit").
public class DeliveryZone : MonoBehaviour
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
        b.Expand(new Vector3(0f, 6f, 0f)); // a car's pivot can sit a little above or below the gizmo
        return b.Contains(at);
    }

    private void Update()
    {
        bool playerInRange = PlayerInZone();
        // The prompt only shows while there's cargo to unload - it goes the moment it's delivered
        bool carrying = playerInRange && Mission1Manager.Instance != null && Mission1Manager.Instance.IsCarryingCargo;
        if (carrying) NotePopup.Prompt(this, $"Press {NotePopup.Key(GameAction.Interact)} to unload the cargo onto the train");
        else NotePopup.ClearPrompt(this);

        if (!carrying || !GameKeys.Down(GameAction.Interact)) return;

        if (Mission1Manager.Instance.HasTrainDeparted)
        {
            NotePopup.Show("Too late - the train is already moving.", 3f);
            return;
        }

        if (!Mission1Manager.Instance.IsTrainAtPlatform)
        {
            NotePopup.Show("The train hasn't arrived yet. Wait for it.", 3f);
            return;
        }

        if (!Mission1Manager.Instance.IsTaxiNear(transform.position))
        {
            NotePopup.Show("Park the taxi next to the train to unload the cargo.", 3f);
            return;
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.cargoDeliver);
        Mission1Manager.Instance.OnCargoDelivered();
        NotePopup.ClearPrompt(this);
    }

    // Switched off (used up, picked up...) - take the prompt away with it
    private void OnDisable() => NotePopup.ClearPrompt(this);
}
