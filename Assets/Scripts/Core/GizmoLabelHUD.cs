using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Draws every GizmoLabel as a small hologram over its marker:
//
//      R E T U R N   H O M E        <- title, in the marker's colour, soft glow
//        LEAVE MISSION · 24 M        <- subtitle + distance
//      ───────────◆───────────       <- thin line + diamond
//                 ┊                  <- tether fading down to the marker
//
// Half see-through, with a faint flicker and a slow bob. Fades in as you
// get near, ghosts out behind walls, lights up when you're close enough to
// use it. Hidden in photo mode and while the game is paused / in a briefing.
// Labels with the same name next to each other (e.g. the 4 fuel pumps) only
// show the nearest one.
//
// Made automatically (no setup) and kept across scenes.
public class GizmoLabelHUD : MonoBehaviour
{
    // ---- look ----
    private const float BaseAlpha = 0.55f;       // "barely visible, but visible"
    private const float UseAlpha = 0.95f;       // when you're close enough to use it
    private const float BehindWallFactor = 0.3f;
    private const float FadeSpeed = 3f;         // alpha per second
    private const float TitleSize = 30f;
    private const float SubSize = 14f;
    private const float SameNameRadius = 10f;   // same-title labels closer than this: only the nearest shows

    private static GizmoLabelHUD instance;

    private class Widget
    {
        public RectTransform root;
        public TMP_Text title, sub;
        public Image line, diamond, tether;
        public CanvasGroup group;
        public float alpha;
        public float seed;
        public bool occluded;
        public float nextOcclusionCheck;
        public Color color;
    }

    private readonly Dictionary<GizmoLabel, Widget> widgets = new Dictionary<GizmoLabel, Widget>();
    private readonly Dictionary<Color, Material> glowMaterials = new Dictionary<Color, Material>();
    private readonly List<GizmoLabel> remove = new List<GizmoLabel>();
    private readonly RaycastHit[] hits = new RaycastHit[16];
    private RectTransform canvasRect;
    private Sprite tetherSprite;
    private TMP_FontAsset font;
    private Transform player;

    public static void Ensure()
    {
        if (instance != null || !Application.isPlaying) return;
        var go = new GameObject("GizmoLabelHUD");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GizmoLabelHUD>();
    }

    private void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -20; // under the rest of the HUD
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)transform;

        font = TMP_Settings.defaultFontAsset;

        // Tether: a 1-pixel-wide strip fading out towards the bottom
        var tex = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++) tex.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.Pow(y / 31f, 1.5f)));
        tex.Apply();
        tetherSprite = Sprite.Create(tex, new Rect(0, 0, 1, 32), new Vector2(0.5f, 0f));
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        bool hideAll = cam == null || Time.timeScale == 0f || CameraMode.IsActive;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            player = p != null ? p.transform : null;
        }
        Transform me = VehicleInteraction.Current != null ? VehicleInteraction.Current.transform : player;
        Vector3 from = me != null ? me.position : (cam != null ? cam.transform.position : Vector3.zero);

        // Labels that went away
        remove.Clear();
        foreach (var kv in widgets) if (kv.Key == null || !GizmoLabel.All.Contains(kv.Key)) remove.Add(kv.Key);
        foreach (GizmoLabel l in remove) { if (widgets[l].root != null) Destroy(widgets[l].root.gameObject); widgets.Remove(l); }

        float t = Time.unscaledTime;
        foreach (GizmoLabel label in GizmoLabel.All)
        {
            if (!widgets.TryGetValue(label, out Widget w)) widgets[label] = w = Create(label);

            Vector3 anchor = label.Anchor;
            float distance = Flat(anchor - from);
            bool want = !hideAll && label.Visible && distance <= label.showDistance && !HasCloserTwin(label, from, distance);

            Vector3 screen = cam != null ? cam.WorldToScreenPoint(anchor) : Vector3.back;
            if (screen.z <= 0f) want = false;

            // Behind a wall? (checked a few times a second)
            if (want && Time.unscaledTime >= w.nextOcclusionCheck)
            {
                w.nextOcclusionCheck = Time.unscaledTime + 0.25f + w.seed * 0.1f;
                w.occluded = Blocked(cam.transform.position, anchor, label, me);
            }

            // Target brightness: far fade-in, near light-up, ghost behind walls
            float target = 0f;
            if (want)
            {
                float edge = Mathf.InverseLerp(label.showDistance, label.showDistance * 0.75f, distance);
                float near = Mathf.InverseLerp(label.useDistance + 2f, label.useDistance, distance);
                target = Mathf.Lerp(BaseAlpha, UseAlpha, near) * edge * (w.occluded ? BehindWallFactor : 1f);
            }
            w.alpha = Mathf.MoveTowards(w.alpha, target, FadeSpeed * Time.unscaledDeltaTime);

            bool show = w.alpha > 0.005f && screen.z > 0f;
            if (w.root.gameObject.activeSelf != show) w.root.gameObject.SetActive(show);
            if (!show) continue;

            // Hologram life: faint flicker, a rare glitch, slow bob
            float flicker = 0.9f + 0.1f * Mathf.PerlinNoise(t * 7f, w.seed * 13f);
            bool glitch = Mathf.PerlinNoise(t * 2.3f, w.seed * 31f) > 0.83f && Mathf.PerlinNoise(t * 40f, w.seed) > 0.6f;
            w.group.alpha = w.alpha * flicker * (glitch ? 0.55f : 1f);

            float scale = Mathf.Lerp(1.15f, 0.72f, Mathf.InverseLerp(4f, Mathf.Max(8f, label.showDistance), distance));
            w.root.localScale = Vector3.one * scale;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
            local.y += Mathf.Sin(t * 1.6f + w.seed * 6.28f) * 3f;
            w.root.anchoredPosition = local;
            w.title.rectTransform.anchoredPosition = new Vector2(glitch ? (w.seed > 0.5f ? 3f : -3f) : 0f, w.title.rectTransform.anchoredPosition.y);

            string sub = string.IsNullOrEmpty(label.subtitle) ? "" : label.subtitle.ToUpperInvariant() + "  ·  ";
            w.sub.text = sub + Mathf.RoundToInt(distance) + " M";
            if (w.title.text != label.title.ToUpperInvariant()) w.title.text = label.title.ToUpperInvariant();
            if (w.color != label.color) Recolor(w, label.color);

            // Line as wide as the title
            float width = Mathf.Max(60f, w.title.preferredWidth + 28f);
            w.line.rectTransform.sizeDelta = new Vector2(width, 1.5f);
        }
    }

    // Another label with the same name nearby that's closer to you - let that one talk
    private static bool HasCloserTwin(GizmoLabel label, Vector3 from, float distance)
    {
        foreach (GizmoLabel other in GizmoLabel.All)
        {
            if (other == label || other.title != label.title || !other.Visible) continue;
            if (Flat(other.Anchor - label.Anchor) > SameNameRadius) continue;
            float d = Flat(other.Anchor - from);
            if (d < distance || (Mathf.Approximately(d, distance) && other.GetInstanceID() < label.GetInstanceID())) return true;
        }
        return false;
    }

    private bool Blocked(Vector3 eye, Vector3 anchor, GizmoLabel label, Transform me)
    {
        Vector3 dir = anchor - eye;
        int n = Physics.RaycastNonAlloc(eye, dir.normalized, hits, dir.magnitude, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Transform h = hits[i].transform;
            if (h.IsChildOf(label.transform)) continue;                     // the gizmo / its prop
            if (me != null && h.IsChildOf(me)) continue;                    // you / your car
            if (player != null && h.IsChildOf(player)) continue;
            if (h.CompareTag("Player") || h.CompareTag("PlayerCar")) continue;
            if (hits[i].collider.attachedRigidbody != null) continue;       // cars, props that move
            return true;
        }
        return false;
    }

    // ---------- building a label ----------

    private Widget Create(GizmoLabel label)
    {
        var w = new Widget { seed = Random.value };

        var root = new GameObject("Label " + label.title, typeof(RectTransform), typeof(CanvasGroup));
        w.root = (RectTransform)root.transform;
        w.root.SetParent(transform, false);
        w.root.sizeDelta = Vector2.zero;
        w.group = root.GetComponent<CanvasGroup>();
        w.group.blocksRaycasts = false;
        w.group.interactable = false;

        // Bottom to top: tether, diamond, line, subtitle, title
        w.tether = MakeImage(w.root, "Tether", new Vector2(1.5f, 34f), new Vector2(0f, 0f), new Vector2(0.5f, 0f));
        w.tether.sprite = tetherSprite;
        w.diamond = MakeImage(w.root, "Diamond", new Vector2(7f, 7f), new Vector2(0f, 38f), new Vector2(0.5f, 0.5f));
        w.diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        w.line = MakeImage(w.root, "Line", new Vector2(120f, 1.5f), new Vector2(0f, 38f), new Vector2(0.5f, 0.5f));

        w.sub = MakeText(w.root, "Sub", SubSize, 18f, new Vector2(0f, 52f));
        w.title = MakeText(w.root, "Title", TitleSize, 14f, new Vector2(0f, 82f));
        w.title.text = label.title.ToUpperInvariant();

        w.alpha = 0f;
        Recolor(w, label.color);
        root.SetActive(false);
        return w;
    }

    private void Recolor(Widget w, Color c)
    {
        w.color = c;
        Color solid = new Color(c.r, c.g, c.b, 1f);
        w.title.color = solid;
        w.sub.color = new Color(c.r, c.g, c.b, 0.75f);
        w.line.color = new Color(c.r, c.g, c.b, 0.6f);
        w.diamond.color = solid;
        w.tether.color = new Color(c.r, c.g, c.b, 0.7f);
        w.title.fontSharedMaterial = GlowMaterial(c);
        w.sub.fontSharedMaterial = GlowMaterial(c);
    }

    // The font's own material with a soft halo: a deep shade of the label colour,
    // so it glows on dark backgrounds and still reads on bright ones (works with
    // every TMP shader, so it's safe in builds)
    private Material GlowMaterial(Color c)
    {
        if (glowMaterials.TryGetValue(c, out Material m) && m != null) return m;
        Material baseMat = font != null ? font.material : null;
        if (baseMat == null) return null;
        m = new Material(baseMat) { name = "GizmoLabelGlow" };
        m.EnableKeyword("UNDERLAY_ON");
        Color halo = Color.Lerp(c, Color.black, 0.6f);
        m.SetColor("_UnderlayColor", new Color(halo.r, halo.g, halo.b, 0.7f));
        m.SetFloat("_UnderlayOffsetX", 0f);
        m.SetFloat("_UnderlayOffsetY", 0f);
        m.SetFloat("_UnderlayDilate", 0.45f);
        m.SetFloat("_UnderlaySoftness", 0.75f);
        glowMaterials[c] = m;
        return m;
    }

    private TMP_Text MakeText(RectTransform parent, string name, float size, float spacing, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.characterSpacing = spacing;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        var rt = text.rectTransform;
        rt.sizeDelta = new Vector2(600f, size * 1.6f);
        rt.anchoredPosition = pos;
        return text;
    }

    private static Image MakeImage(RectTransform parent, string name, Vector2 size, Vector2 pos, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return img;
    }

    private static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }
}
