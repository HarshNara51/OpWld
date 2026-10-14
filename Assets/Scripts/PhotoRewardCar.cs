using UnityEngine;

// Put this on the reward car parked in the garage (in every scene).
// Keeps it hidden - visuals, collision and interaction - until the photo
// hunt is complete. Appears instantly if you finish the hunt in this scene.
public class PhotoRewardCar : MonoBehaviour
{
    [SerializeField] private Renderer[] renderersToHide;
    [SerializeField] private Collider[] collidersToDisable;
    [Tooltip("The car's VehicleInteraction")]
    [SerializeField] private MonoBehaviour interactionScript;

    // Frozen while hidden - with every collider off, gravity would drop it through the ground
    private Rigidbody body;
    private bool wasKinematic;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body != null) wasKinematic = body.isKinematic;
    }

    private void OnEnable() => PhotoCollectionManager.RewardUnlocked += Show;
    private void OnDisable() => PhotoCollectionManager.RewardUnlocked -= Show;

    private void Start()
    {
        bool unlocked = PhotoCollectionManager.Instance != null && PhotoCollectionManager.Instance.IsRewardUnlocked;
        SetVisible(unlocked);
    }

    private void Show() => SetVisible(true);

    private void SetVisible(bool visible)
    {
        if (body != null)
        {
            if (!visible && !body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = visible ? wasKinematic : true;
        }
        if (renderersToHide != null) foreach (var r in renderersToHide) if (r != null) r.enabled = visible;
        if (collidersToDisable != null) foreach (var c in collidersToDisable) if (c != null) c.enabled = visible;
        if (interactionScript != null) interactionScript.enabled = visible;
    }
}
