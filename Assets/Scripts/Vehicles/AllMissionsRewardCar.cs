using UnityEngine;

// Bonus car (the Railway taxi), parked at TaxiParkSpace near the repair shop in every scene.
// Hidden - visuals, collision and interaction - until 100% completion: every mission passed
// AND the photo hunt complete (the same hunt that unlocks Blaze). Appears right away if the
// last step happens in this scene, and a one-time thank-you note tells the player where it is.
public class AllMissionsRewardCar : MonoBehaviour
{
    [SerializeField] private Renderer[] renderersToHide;
    [SerializeField] private Collider[] collidersToDisable;
    [Tooltip("The car's VehicleInteraction")]
    [SerializeField] private MonoBehaviour interactionScript;

    [Header("One-time unlock note")]
    [TextArea(2, 5)]
    [SerializeField] private string unlockNote =
        "Thank you for playing! You've unlocked a bonus car - the taxi. It's always parked near the repair shop, " +
        "in every scene, so go drive around and have fun!";
    [Tooltip("Waits this long before the note, so it doesn't land on top of the Mission Passed screen")]
    [SerializeField] private float noteDelay = 8f;

    public const string NoteShownKey = "BonusTaxiNoteShown";

    // Frozen while hidden - with every collider off, gravity would drop it through the ground
    private Rigidbody body;
    private bool wasKinematic;
    private bool visible = true;
    private float nextCheck;
    private float noteAt = -1f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body != null) wasKinematic = body.isKinematic;
    }

    private void Start()
    {
        SetVisible(Unlocked);
        if (visible) QueueNote(2f); // e.g. a save that was already at 100% before this car existed
    }

    private void Update()
    {
        if (!visible && Time.unscaledTime >= nextCheck)
        {
            // While locked, check now and then (the last mission / photo may happen in this scene)
            nextCheck = Time.unscaledTime + 1f;
            if (Unlocked) { SetVisible(true); QueueNote(noteDelay); }
        }

        if (noteAt > 0f && Time.unscaledTime >= noteAt)
        {
            noteAt = -1f;
            if (PlayerPrefs.GetInt(NoteShownKey, 0) == 0)
            {
                PlayerPrefs.SetInt(NoteShownKey, 1);
                PlayerPrefs.Save();
                NotePopup.Show(unlockNote, 8f);
            }
        }
    }

    private void QueueNote(float delay)
    {
        if (PlayerPrefs.GetInt(NoteShownKey, 0) == 0) noteAt = Time.unscaledTime + delay;
    }

    // 100%: all missions passed + the photo hunt reward (Blaze) unlocked
    private static bool Unlocked =>
        GameManager.Instance != null && GameManager.Instance.AllMissionsComplete &&
        PhotoCollectionManager.Instance != null && PhotoCollectionManager.Instance.IsRewardUnlocked;

    private void SetVisible(bool show)
    {
        visible = show;
        if (body != null)
        {
            if (!show && !body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = show ? wasKinematic : true;
        }
        if (renderersToHide != null) foreach (var r in renderersToHide) if (r != null) r.enabled = show;
        if (collidersToDisable != null) foreach (var c in collidersToDisable) if (c != null) c.enabled = show;
        if (interactionScript != null) interactionScript.enabled = show;
    }
}
