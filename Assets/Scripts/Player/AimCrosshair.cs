using UnityEngine;
using UnityEngine.UI;

// Put this on the player (next to WeaponHolster). Shows a small crosshair
// in the middle of the screen while the rifle is out - that's exactly
// where WeaponFire's shots go. Brighter while aiming (right mouse).
// Builds its own UI, nothing to wire.
public class AimCrosshair : MonoBehaviour
{
    [SerializeField] private float size = 6f;
    [SerializeField] private Color aimColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color hipColor = new Color(1f, 1f, 1f, 0.45f);

    private WeaponHolster holster;
    private GameObject canvasGo;
    private Image dot;

    private void Awake()
    {
        holster = GetComponent<WeaponHolster>();

        canvasGo = new GameObject("Crosshair");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        GameObject dotGo = new GameObject("Dot", typeof(RectTransform));
        dotGo.transform.SetParent(canvasGo.transform, false);
        dot = dotGo.AddComponent<Image>();
        dot.raycastTarget = false;
        RectTransform rt = dotGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);

        canvasGo.SetActive(false);
    }

    private void Update()
    {
        bool show = holster != null && holster.IsRifleEquipped;
        if (canvasGo.activeSelf != show) canvasGo.SetActive(show);
        if (show) dot.color = PlayerLocomotion.IsAiming ? aimColor : hipColor;
    }

    private void OnDisable()
    {
        if (canvasGo != null) canvasGo.SetActive(false); // e.g. while driving
    }

    private void OnDestroy()
    {
        if (canvasGo != null) Destroy(canvasGo);
    }
}
