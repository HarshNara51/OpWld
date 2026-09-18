using UnityEngine;

// Put this on Car 2, positioned in the garage in every scene (it's
// baked into the base map like everything else duplicated across all
// 6 scenes). Hides the car - visuals, collision, and interaction -
// until GameManager.isCar2Unlocked is true. Don't tag this car
// "PlayerCar" - that tag is what VehicleSummon looks for, and Car 2
// should never be V-summonable, only reachable by walking to it.
public class GarageCar : MonoBehaviour
{
    [SerializeField] private Renderer[] renderersToHide;
    [SerializeField] private Collider[] collidersToDisable;
    [Tooltip("The car's VehicleInteraction (or equivalent) component")]
    [SerializeField] private MonoBehaviour interactionScript;

    private void Start()
    {
        bool unlocked = GameManager.Instance != null && GameManager.Instance.isCar2Unlocked;
        SetVisible(unlocked);
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in renderersToHide) if (r != null) r.enabled = visible;
        foreach (var c in collidersToDisable) if (c != null) c.enabled = visible;
        if (interactionScript != null) interactionScript.enabled = visible;
    }
}
