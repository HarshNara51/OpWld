using UnityEngine;
using UnityEngine.UI;

// On the Managers prefab (every scene). The player's health bar: a thin bar right above
// the round minimap, shown and hidden together with it. Amber, turning red and pulsing
// when low; flashes white on a hit. Builds its own UI, nothing to wire.
public class PlayerHealthHUD : MonoBehaviour
{
    [SerializeField] private float height = 12f;
    [Tooltip("Gap between the minimap and the bar - clears the map's shadow and its N marker")]
    [SerializeField] private float gap = 30f;
    [SerializeField] private Color fullColor = new Color(1f, 0.72f, 0.3f);
    [SerializeField] private Color lowColor = new Color(0.95f, 0.22f, 0.18f);
    [Range(0f, 1f)] [SerializeField] private float lowBelow = 0.3f;

    private GameObject root;
    private RectTransform bar;
    private Image fill, flash;
    private Health watched;
    private float shown = 1f, flashAlpha;

    private void Awake() => Build();

    private void Build()
    {
        root = new GameObject("HealthHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -6; // same layer as the minimap; menus, notes and results draw on top
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        bar = new GameObject("Bar", typeof(RectTransform)).GetComponent<RectTransform>();
        bar.SetParent(root.transform, false);
        bar.anchorMin = bar.anchorMax = bar.pivot = Vector2.zero;
        var back = bar.gameObject.AddComponent<Image>(); back.color = new Color(0.05f, 0.05f, 0.06f, 0.85f); back.raycastTarget = false;

        fill = Child("Fill", new Color(1f, 1f, 1f, 1f));
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
        fill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f); // Filled needs a sprite
        flash = Child("Flash", new Color(1f, 1f, 1f, 0f));
        root.SetActive(false);
    }

    private Image Child(string name, Color c)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(bar, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(2f, 2f); r.offsetMax = new Vector2(-2f, -2f);
        var img = r.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
        return img;
    }

    private void OnHit(float amount) => flashAlpha = 0.9f;

    private void LateUpdate()
    {
        Health h = PlayerHealthSystem.PlayerHealth;
        if (h != watched)
        {
            if (watched != null) watched.Damaged -= OnHit;
            watched = h;
            if (watched != null) { watched.Damaged += OnHit; shown = watched.Percent01; }
        }

        Minimap map = Minimap.Instance;
        bool show = h != null && map != null && map.IsShowing;
        if (root.activeSelf != show) root.SetActive(show);
        if (!show) return;

        // Sit right above the minimap, same width
        bar.sizeDelta = new Vector2(map.Diameter, height);
        bar.anchoredPosition = map.ScreenMargin + new Vector2(0f, map.Diameter + gap);

        float p = Mathf.Clamp01(h.Percent01);
        shown = Mathf.MoveTowards(shown, p, Time.unscaledDeltaTime * 1.5f); // smooth drop / refill
        fill.fillAmount = shown;
        Color c = p < lowBelow ? lowColor : Color.Lerp(lowColor, fullColor, Mathf.InverseLerp(lowBelow, 0.6f, p));
        if (p < lowBelow && p > 0f) c.a = 0.65f + 0.35f * Mathf.PingPong(Time.unscaledTime * 2.5f, 1f); // low: pulse
        fill.color = c;

        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 3f);
        flash.color = new Color(1f, 1f, 1f, flashAlpha);
    }

    private void OnDestroy()
    {
        if (watched != null) watched.Damaged -= OnHit;
    }
}
