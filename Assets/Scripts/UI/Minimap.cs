using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Round GTA-style minimap in the bottom-left corner:
//
//          N
//      .-------.
//    /  ▒ road ▒  \      the baked map picture, scrolled + turned so the
//   |  ▒▒▒ ▲ ▒▒▒   |     camera's view is always "up"; ▲ = you (your car
//    \   ◆     ○  /      when driving), icons = places + gizmos
//      '-------'
//
// Icons:
//   - Places (home, hospital, police, fire, tow yard) and faint area signs
//     (Farm, Shopping, Construction, Hotels, Railway): the Places list below.
//     The world is the same in every scene, so they show everywhere.
//   - Gizmos with a GizmoLabel: mission starts get the Mission Start star,
//     others go by their title in Icon Rules (icon, tint with the gizmo's
//     colour, or hide - e.g. Farm clues and the payphones). A title with no
//     rule gets the Objective icon in its colour. Same-title gizmos close
//     together (the 4 fuel pumps) show one icon.
// Tools > Minimap > Set Up Icons fills all of this in.
//
// Guidance: only your own waypoint sticks to the edge of the map when it's
// further away (mission objectives can too - Objectives Stick To Edge). Your own
// waypoint (set on the big map, M - see MapScreen) is a pink pin, and an
// on-screen marker with the distance points at it - or, with no waypoint,
// at the nearest mission objective.
//
// The map is a picture of the whole world, baked in the editor per scene:
// Tools > Minimap > Bake Map Images (all 6 scenes) -> Resources/Minimap/
// <scene name>.png (Hub's is used for a scene without one). Only the current
// scene's picture is loaded. Nothing renders the scene a second time, so the
// minimap costs next to nothing.
//
// Zooms out as you drive faster. Hidden in photo mode, menus, briefings and
// while paused. Builds its own UI - lives on the Managers prefab root.
public class Minimap : MonoBehaviour
{
    [Header("Map area (filled in by Tools > Minimap > Bake Map Image)")]
    [Tooltip("World X/Z of the map picture's bottom-left corner")]
    [SerializeField] private Vector2 worldMin = new Vector2(-100f, -100f);
    [Tooltip("World metres the picture covers (X, Z)")]
    [SerializeField] private Vector2 worldSize = new Vector2(1200f, 1200f);
    [Tooltip("Shown outside the picture")]
    [SerializeField] private Color outsideColor = new Color(0.09f, 0.17f, 0.24f);

    [Header("Size & placement")]
    [SerializeField] private float diameter = 260f;
    [Tooltip("Distance from the bottom-left screen corner")]
    [SerializeField] private Vector2 screenMargin = new Vector2(36f, 36f);
    [SerializeField] private float ringWidth = 5f;
    [SerializeField] private Color ringColor = new Color(0.05f, 0.05f, 0.06f, 0.9f);
    [SerializeField] private Color accentColor = new Color(1f, 0.78f, 0.25f);
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("Big map title (Tulpen One)")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private Color waypointColor = new Color(1f, 0.36f, 0.86f);

    [Header("Zoom (metres from the centre to the edge)")]
    [SerializeField] private float onFootRadius = 85f;
    [SerializeField] private float carSlowRadius = 120f;
    [SerializeField] private float carFastRadius = 210f;
    [Tooltip("Car speed (km/h) at which the map is zoomed out all the way")]
    [SerializeField] private float fastSpeedKmh = 130f;
    [SerializeField] private float zoomSmoothing = 2f;

    [Header("Icons")]
    [SerializeField] private bool showGizmoBlips = true;
    [Tooltip("Gizmos that start a mission (MissionEntryPoint)")]
    [SerializeField] private Sprite missionStartIcon;
    [Tooltip("Gizmos without a rule below (tinted with the gizmo's colour). Empty = a dot")]
    [SerializeField] private Sprite objectiveIcon;
    [SerializeField] private IconRule[] iconRules = new IconRule[0];
    [SerializeField] private Place[] places = new Place[0];
    [SerializeField] private float iconSize = 24f;
    [SerializeField] private float objectiveIconSize = 20f;
    [SerializeField] private float areaIconSize = 38f;
    [Range(0f, 1f)] [SerializeField] private float areaAlpha = 0.35f;
    [Tooltip("Same-title gizmos closer together than this show one icon")]
    [SerializeField] private float sameTitleRadius = 40f;
    [SerializeField] private float playerArrowSize = 26f;
    [Tooltip("Far mission objectives (diamonds/rings) wait on the map's edge like the waypoint. Mission stars never do")]
    [SerializeField] private bool objectivesStickToEdge = false;

    [Header("On-screen marker")]
    [SerializeField] private bool showMarker = true;
    [SerializeField] private float markerIconSize = 30f;
    [Tooltip("Keeps this far from the screen edges when the target is off-screen")]
    [SerializeField] private float markerEdgeMargin = 70f;

    [System.Serializable]
    public class IconRule
    {
        [Tooltip("The GizmoLabel title, e.g. Respray (not case-sensitive)")]
        public string title;
        public Sprite icon;
        [Tooltip("Colour the icon with the gizmo's colour (white icons)")]
        public bool tint;
        [Tooltip("Never show this gizmo on the map")]
        public bool hide;
    }

    [System.Serializable]
    public class Place
    {
        public string name;
        public Sprite icon;
        [Tooltip("World X / Z")]
        public Vector2 position;
        [Tooltip("Big faint area sign instead of a place icon")]
        public bool area;
    }

    private class Blip
    {
        public RectTransform rect;
        public Image image;
        public bool tint, hide, star;
        public float size;
    }

    public static Minimap Instance { get; private set; }

    // The nearest visible mission objective (the marker's target with no waypoint set)
    public static GizmoLabel AutoTarget { get; private set; }

    // For the big map
    public Texture2D MapTexture => mapTexture;
    public Vector2 WorldMin => worldMin;
    public Vector2 WorldSize => worldSize;
    public Place[] Places => places;
    public TMP_FontAsset Font => font;
    public TMP_FontAsset TitleFont => titleFont != null ? titleFont : font;
    public Color WaypointColor => waypointColor;
    public Color AccentColor => accentColor;
    public Sprite PinSprite { get; private set; }
    public Sprite PlayerArrowSprite { get; private set; }
    public Sprite DotSprite => blipSprite;
    public Sprite MissionStartIcon => missionStartIcon;
    public Sprite ObjectiveIcon => objectiveIcon;
    public float AreaAlpha => areaAlpha;

    private GameObject root;
    private RectTransform frame, rotator, mapRect, arrow, north;
    private RawImage mapImage;
    private Sprite blipSprite;
    private readonly Dictionary<GizmoLabel, Blip> blips = new Dictionary<GizmoLabel, Blip>();
    private readonly List<GizmoLabel> remove = new List<GizmoLabel>();
    private int starCount;
    private readonly List<(Place place, RectTransform rect)> placeIcons = new List<(Place, RectTransform)>();

    private RectTransform pin;
    private RectTransform marker, markerArrow;
    private Image markerIcon;
    private TMP_Text markerText;
    private CanvasGroup markerGroup;
    private RectTransform canvasRect;

    private Transform player;
    private float radius;
    private Texture2D mapTexture;

    private void Awake()
    {
        Instance = this;
        PinSprite = MakePinSprite(96);
        PlayerArrowSprite = ArrowSprite(64);
        BuildUI();
        radius = onFootRadius;
        root.SetActive(false);
        LoadMap(SceneManager.GetActiveScene());
        if (GetComponent<MapScreen>() == null) gameObject.AddComponent<MapScreen>();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => LoadMap(scene);

    // Only the current scene's picture is in memory
    private void LoadMap(Scene scene)
    {
        Texture2D tex = Resources.Load<Texture2D>("Minimap/" + scene.name);
        if (tex == null) tex = Resources.Load<Texture2D>("Minimap/Hub");
        if (tex == mapTexture) return;
        if (mapTexture != null) Resources.UnloadAsset(mapTexture);
        mapTexture = tex;
        if (mapImage != null) mapImage.texture = tex;
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            player = p != null ? p.transform : null;
        }

        Camera cam = Camera.main;
        VehicleInteraction car = VehicleInteraction.Current;
        Transform me = car != null ? car.transform : player;

        if (me != null) Waypoint.CheckReached(me.position);

        bool show = me != null && cam != null && mapTexture != null
                    && Time.timeScale > 0f && !CameraMode.IsActive && !PauseManager.IsPaused;
        if (root.activeSelf != show) root.SetActive(show);
        if (!show) return;

        // Zoom: out with speed
        float target = onFootRadius;
        if (car != null)
        {
            Rigidbody body = car.GetComponent<Rigidbody>();
            float kmh = body != null ? body.linearVelocity.magnitude * 3.6f : 0f;
            target = Mathf.Lerp(carSlowRadius, carFastRadius, Mathf.Clamp01(kmh / fastSpeedKmh));
        }
        radius = Mathf.Lerp(radius, target, 1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime));
        float scale = diameter * 0.5f / radius; // UI units per metre

        // Map: you in the middle, the camera's view pointing up
        Vector2 here = new Vector2(me.position.x, me.position.z);
        float camYaw = cam.transform.eulerAngles.y;
        rotator.localRotation = Quaternion.Euler(0f, 0f, camYaw);
        mapRect.sizeDelta = worldSize * scale;
        mapRect.anchoredPosition = -(here - worldMin) * scale;

        arrow.localRotation = Quaternion.Euler(0f, 0f, camYaw - me.eulerAngles.y);

        // "N" rides the ring where world north is
        float a = camYaw * Mathf.Deg2Rad;
        float r = diameter * 0.5f;
        north.anchoredPosition = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * r;

        // Icons stay upright while the map turns under them
        Quaternion upright = Quaternion.Euler(0f, 0f, -camYaw);
        UpdatePlaces(here, scale, upright);
        UpdateBlips(here, scale, upright);
        UpdatePin(here, scale, upright);
        UpdateMarker(cam, me);
    }

    // Where a far icon sits: on the edge, in its direction
    private Vector2 Clamp(Vector2 offset, float scale, float size, out bool clamped)
    {
        float edge = radius - size * 0.5f / scale;
        clamped = offset.magnitude > edge;
        return (clamped ? offset.normalized * edge : offset) * scale;
    }

    private void UpdatePin(Vector2 here, float scale, Quaternion upright)
    {
        bool on = Waypoint.Active;
        if (pin.gameObject.activeSelf != on) pin.gameObject.SetActive(on);
        if (!on) return;
        Vector3 w = Waypoint.Position;
        pin.anchoredPosition = Clamp(new Vector2(w.x, w.z) - here, scale, iconSize, out _);
        pin.localRotation = upright;
        if (pin.GetSiblingIndex() != rotator.childCount - 1) pin.SetAsLastSibling();
    }

    // The on-screen marker: your waypoint, or the nearest objective
    private void UpdateMarker(Camera cam, Transform me)
    {
        Vector3 target;
        Sprite icon;
        Color color;
        GizmoLabel label = null;
        if (Waypoint.Active)
        {
            target = Waypoint.Position;
            label = Waypoint.Gizmo;
            icon = PinSprite;
            color = waypointColor;
        }
        else if (AutoTarget != null)
        {
            label = AutoTarget;
            target = label.Anchor;
            icon = blips.TryGetValue(label, out Blip b) ? b.image.sprite : objectiveIcon;
            color = new Color(label.color.r, label.color.g, label.color.b, 1f);
        }
        else
        {
            if (marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
            return;
        }

        Vector3 flat = target - me.position;
        flat.y = 0f;
        float distance = flat.magnitude;
        Vector3 screen = cam.WorldToScreenPoint(target);
        bool onScreen = screen.z > 0f && screen.x > 0f && screen.x < Screen.width && screen.y > 0f && screen.y < Screen.height;

        // Close by, the gizmo's own hologram label takes over
        float handOver = label != null ? label.showDistance : 15f;
        bool show = showMarker && !(onScreen && distance <= handOver);
        if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
        if (!show) return;

        if (markerIcon.sprite != icon) markerIcon.sprite = icon;
        markerIcon.color = color;
        markerText.text = distance >= 1000f ? (distance / 1000f).ToString("0.0") + " KM" : Mathf.RoundToInt(distance) + " M";

        // Screen point -> canvas, pinned inside the edges when off-screen / behind you
        Vector2 size = canvasRect.rect.size;
        Vector2 p = new Vector2(screen.x / Screen.width * size.x, screen.y / Screen.height * size.y) - size * 0.5f;
        if (screen.z < 0f) p = -p;
        Vector2 half = size * 0.5f - new Vector2(markerEdgeMargin, markerEdgeMargin);
        bool pinned = !onScreen;
        if (pinned)
        {
            if (p.sqrMagnitude < 1f) p = Vector2.down;
            float k = Mathf.Min(half.x / Mathf.Max(0.001f, Mathf.Abs(p.x)), half.y / Mathf.Max(0.001f, Mathf.Abs(p.y)));
            p *= k;
        }
        marker.anchoredPosition = p;
        markerArrow.gameObject.SetActive(pinned);
        if (pinned) markerArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg - 90f);
        markerGroup.alpha = 0.92f;
    }

    private void UpdatePlaces(Vector2 here, float scale, Quaternion upright)
    {
        foreach (var (place, rect) in placeIcons)
        {
            Vector2 offset = place.position - here;
            // Area signs may hang over the edge (the round window cuts them); places pop in whole
            float reach = place.area ? radius + areaIconSize / scale : radius - iconSize * 0.5f / scale;
            bool visible = offset.magnitude <= reach;
            if (rect.gameObject.activeSelf != visible) rect.gameObject.SetActive(visible);
            if (!visible) continue;
            rect.anchoredPosition = offset * scale;
            rect.localRotation = upright;
        }
    }

    private void UpdateBlips(Vector2 here, float scale, Quaternion upright)
    {
        remove.Clear();
        foreach (var kv in blips) if (kv.Key == null || !GizmoLabel.All.Contains(kv.Key)) remove.Add(kv.Key);
        foreach (GizmoLabel l in remove)
        {
            if (blips[l].star) starCount--;
            if (blips[l].rect != null) Destroy(blips[l].rect.gameObject);
            blips.Remove(l);
        }

        GizmoLabel nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (GizmoLabel label in GizmoLabel.All)
        {
            if (!blips.TryGetValue(label, out Blip b)) blips[label] = b = MakeBlip(label);

            Vector3 p = label.Anchor;
            Vector2 offset = new Vector2(p.x, p.z) - here;
            float distance = offset.magnitude;
            bool tracked = objectivesStickToEdge && b.tint && !b.star;
            bool usable = !b.hide && label.Visible;
            if (usable && b.tint && !b.star && distance < nearestDistance) { nearest = label; nearestDistance = distance; }

            bool visible = showGizmoBlips && usable
                           && (tracked || distance <= radius - b.size * 0.5f / scale)
                           && !HasCloserTwin(label, here, distance);
            if (b.rect.gameObject.activeSelf != visible) b.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            b.rect.anchoredPosition = Clamp(offset, scale, b.size, out bool clamped);
            b.rect.localScale = Vector3.one * (clamped ? 0.8f : 1f);
            b.rect.localRotation = upright;
            if (b.tint)
            {
                Color c = new Color(label.color.r, label.color.g, label.color.b, 1f);
                if (b.image.color != c) b.image.color = c;
            }
        }
        AutoTarget = nearest;
    }

    // Another visible gizmo with the same title nearby that's closer to you shows instead
    private bool HasCloserTwin(GizmoLabel label, Vector2 here, float distance)
    {
        foreach (GizmoLabel other in GizmoLabel.All)
        {
            if (other == label || other.title != label.title || !other.Visible) continue;
            Vector3 a = label.Anchor, o = other.Anchor;
            if (new Vector2(o.x - a.x, o.z - a.z).magnitude > sameTitleRadius) continue;
            float d = (new Vector2(o.x, o.z) - here).magnitude;
            if (d < distance || (Mathf.Approximately(d, distance) && other.GetInstanceID() < label.GetInstanceID())) return true;
        }
        return false;
    }

    // ---------- building the UI ----------

    private void BuildUI()
    {
        root = new GameObject("MinimapHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -6; // menus, notes and results draw on top

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite circle = CircleSprite(256, 0f);
        float outer = diameter + ringWidth * 2f;

        // Frame: bottom-left, everything else is centred on it
        frame = Rect("Minimap", root.transform);
        frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.zero;
        frame.sizeDelta = new Vector2(outer, outer);
        frame.anchoredPosition = screenMargin;

        Image shadow = Img("Shadow", frame, CircleSprite(256, 0.35f), new Color(0f, 0f, 0f, 0.45f));
        Centre(shadow.rectTransform, outer + 26f);

        Image ring = Img("Ring", frame, circle, ringColor);
        Centre(ring.rectTransform, outer);

        // Round window the map shows through
        Image window = Img("Window", frame, circle, Color.white);
        Centre(window.rectTransform, diameter);
        window.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        Image outside = Img("Outside", window.rectTransform, null, outsideColor);
        Centre(outside.rectTransform, diameter);

        rotator = Rect("Rotator", window.rectTransform);
        Centre(rotator, 0f);

        mapRect = Rect("Map", rotator);
        mapRect.anchorMin = mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapRect.pivot = Vector2.zero;
        mapImage = mapRect.gameObject.AddComponent<RawImage>();
        mapImage.raycastTarget = false;

        blipSprite = BlipSprite(64);

        // Area signs under place icons, gizmo icons (made later) on top
        foreach (Place place in places) if (place != null && place.area && place.icon != null) AddPlace(place);
        foreach (Place place in places) if (place != null && !place.area && place.icon != null) AddPlace(place);

        // Your waypoint (kept on top of the icons)
        Image pinImg = Img("Waypoint", rotator, PinSprite, waypointColor);
        pin = pinImg.rectTransform;
        pin.anchorMin = pin.anchorMax = new Vector2(0.5f, 0.5f);
        pin.sizeDelta = new Vector2(iconSize + 4f, iconSize + 4f);
        pin.gameObject.SetActive(false);

        // Soft dark edge inside the window, so the map fades into the ring
        Image vignette = Img("EdgeShade", window.rectTransform, EdgeShadeSprite(256), new Color(0f, 0f, 0f, 0.55f));
        Centre(vignette.rectTransform, diameter);

        // You
        Image you = Img("Player", window.rectTransform, PlayerArrowSprite, Color.white);
        arrow = you.rectTransform;
        Centre(arrow, playerArrowSize);

        // Thin accent line just inside the dark ring
        Image accent = Img("Accent", frame, RingSprite(256, 0.985f, 0.94f), new Color(accentColor.r, accentColor.g, accentColor.b, 0.85f));
        Centre(accent.rectTransform, diameter + 4f);

        // North
        Image nBack = Img("North", frame, circle, ringColor);
        north = nBack.rectTransform;
        Centre(north, 26f);
        var nText = Rect("N", north).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) nText.font = font;
        nText.text = "N";
        nText.fontSize = 17f;
        nText.fontStyle = FontStyles.Bold;
        nText.color = accentColor;
        nText.alignment = TextAlignmentOptions.Center;
        nText.raycastTarget = false;
        nText.rectTransform.anchorMin = Vector2.zero;
        nText.rectTransform.anchorMax = Vector2.one;
        nText.rectTransform.offsetMin = nText.rectTransform.offsetMax = Vector2.zero;

        BuildMarker();
    }

    private void BuildMarker()
    {
        canvasRect = (RectTransform)root.transform;
        marker = Rect("Marker", root.transform);
        marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.sizeDelta = new Vector2(markerIconSize, markerIconSize);
        markerGroup = marker.gameObject.AddComponent<CanvasGroup>();
        markerGroup.blocksRaycasts = false;

        // Arrow pointing off-screen (only while pinned to the edge)
        Image arrowImg = Img("Arrow", marker, PlayerArrowSprite, Color.white);
        markerArrow = arrowImg.rectTransform;
        Centre(markerArrow, 0f);
        markerArrow.sizeDelta = new Vector2(markerIconSize * 0.55f, markerIconSize * 0.55f);
        markerArrow.pivot = new Vector2(0.5f, -1.15f); // orbits the icon

        markerIcon = Img("Icon", marker, PinSprite, waypointColor);
        markerIcon.preserveAspect = true;
        Centre(markerIcon.rectTransform, markerIconSize);

        markerText = Rect("Distance", marker).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) markerText.font = font;
        markerText.fontSize = 17f;
        markerText.fontStyle = FontStyles.Bold;
        markerText.alignment = TextAlignmentOptions.Center;
        markerText.textWrappingMode = TextWrappingModes.NoWrap;
        markerText.raycastTarget = false;
        markerText.outlineWidth = 0.25f;
        markerText.outlineColor = new Color32(0, 0, 0, 200);
        RectTransform t = markerText.rectTransform;
        t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
        t.sizeDelta = new Vector2(140f, 24f);
        t.anchoredPosition = new Vector2(0f, -markerIconSize * 0.5f - 12f);
        marker.gameObject.SetActive(false);
    }

    private void AddPlace(Place place)
    {
        float size = place.area ? areaIconSize : iconSize;
        Image img = Img((place.area ? "Area " : "Place ") + place.name, rotator, place.icon,
                        new Color(1f, 1f, 1f, place.area ? areaAlpha : 1f));
        img.preserveAspect = true;
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.sizeDelta = new Vector2(size, size);
        img.gameObject.SetActive(false);
        placeIcons.Add((place, img.rectTransform));
    }

    // Which icon a gizmo gets (also used by the big map)
    public void Describe(GizmoLabel label, out Sprite sprite, out bool tint, out bool hide, out bool star, out float size)
    {
        tint = hide = star = false;
        IconRule rule = FindRule(label.title);
        if (label.GetComponent<MissionEntryPoint>() != null && missionStartIcon != null)
        {
            sprite = missionStartIcon;   // the star keeps its own colours
            size = iconSize;
            star = true;
        }
        else if (rule != null)
        {
            sprite = rule.icon != null ? rule.icon : blipSprite;
            tint = rule.tint || rule.icon == null;
            hide = rule.hide;
            size = rule.tint ? objectiveIconSize : iconSize;
        }
        else
        {
            sprite = objectiveIcon != null ? objectiveIcon : blipSprite;
            tint = true;
            size = objectiveIcon != null ? objectiveIconSize : 14f;
        }
    }

    // Same-title gizmos close together: does another one stand in for this one?
    public bool IsTwinHidden(GizmoLabel label, Vector2 from)
    {
        Vector3 a = label.Anchor;
        return HasCloserTwin(label, from, (new Vector2(a.x, a.z) - from).magnitude);
    }

    private Blip MakeBlip(GizmoLabel label)
    {
        var b = new Blip();
        Describe(label, out Sprite sprite, out b.tint, out b.hide, out b.star, out b.size);

        b.image = Img("Icon " + label.title, rotator, sprite, Color.white);
        b.image.preserveAspect = true;
        b.rect = b.image.rectTransform;
        b.rect.anchorMin = b.rect.anchorMax = new Vector2(0.5f, 0.5f);
        b.rect.sizeDelta = new Vector2(b.size, b.size);
        // Above the map and places; mission stars above everything else
        if (b.star) starCount++;
        b.rect.SetSiblingIndex(b.star ? rotator.childCount - 1 : rotator.childCount - 1 - starCount);
        b.rect.gameObject.SetActive(false);
        return b;
    }

    // The icon a gizmo title's rule uses (the big map's legend)
    public Sprite RuleIcon(string title) => FindRule(title)?.icon;

    private IconRule FindRule(string title)
    {
        if (iconRules == null || string.IsNullOrEmpty(title)) return null;
        foreach (IconRule r in iconRules)
            if (r != null && string.Equals(r.title, title.Trim(), System.StringComparison.OrdinalIgnoreCase)) return r;
        return null;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image Img(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = Rect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Centre(RectTransform rt, float size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
    }

    // ---------- procedural sprites (no extra art needed) ----------

    // Filled circle; softness > 0 blurs the edge inward (0-1 of the radius)
    private static Sprite CircleSprite(int size, float softness)
    {
        return MakeSprite(size, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);              // 0 centre .. 1 edge
            if (softness > 0f) return Mathf.Clamp01((1f - d) / softness);
            return Mathf.Clamp01((1f - d) * size * 0.5f);
        });
    }

    private static Sprite RingSprite(int size, float outer, float inner)
    {
        float px = 2f / size;
        return MakeSprite(size, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((outer - d) / px) * Mathf.Clamp01((d - inner) / px);
        });
    }

    private static Sprite EdgeShadeSprite(int size)
    {
        return MakeSprite(size, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Pow(Mathf.Clamp01((d - 0.72f) / 0.28f), 2f);
        });
    }

    // White dot with a dark rim (tinted by the Image colour, the rim stays dark)
    private static Sprite BlipSprite(int size)
    {
        var tex = NewTex(size);
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float x = (px + 0.5f) / size * 2f - 1f, y = (py + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Clamp01((1f - d) * size * 0.5f);
                float inner = Mathf.Clamp01((0.68f - d) * size * 0.5f);
                float v = Mathf.Lerp(0.08f, 1f, inner);
                tex.SetPixel(px, py, new Color(v, v, v, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // GTA-ish chevron pointing up, white with a dark outline
    private static Sprite ArrowSprite(int size)
    {
        Vector2[] shape = { new Vector2(0f, 0.92f), new Vector2(0.72f, -0.82f), new Vector2(0f, -0.42f), new Vector2(-0.72f, -0.82f) };
        Vector2[] inner = { new Vector2(0f, 0.66f), new Vector2(0.5f, -0.58f), new Vector2(0f, -0.26f), new Vector2(-0.5f, -0.58f) };
        var tex = NewTex(size);
        const int ss = 4;
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float outerHits = 0f, innerHits = 0f;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        Vector2 p = new Vector2((px + (sx + 0.5f) / ss) / size * 2f - 1f, (py + (sy + 0.5f) / ss) / size * 2f - 1f);
                        if (Inside(shape, p)) outerHits++;
                        if (Inside(inner, p)) innerHits++;
                    }
                float a = outerHits / (ss * ss);
                float v = Mathf.Lerp(0.1f, 1f, innerHits / (ss * ss));
                tex.SetPixel(px, py, new Color(v, v, v, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // Map pin: round head + point, white with a dark outline and a hole (tinted by the Image)
    private static Sprite MakePinSprite(int size)
    {
        var tex = NewTex(size);
        const int ss = 3;
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float outer = 0f, inner = 0f;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        Vector2 p = new Vector2((px + (sx + 0.5f) / ss) / size * 2f - 1f, (py + (sy + 0.5f) / ss) / size * 2f - 1f);
                        if (InPin(p, 0f)) outer++;
                        if (InPin(p, 0.13f) && (p - new Vector2(0f, 0.3f)).magnitude > 0.2f) inner++;
                    }
                float a = outer / (ss * ss);
                float v = Mathf.Lerp(0.1f, 1f, inner / (ss * ss));
                tex.SetPixel(px, py, new Color(v, v, v, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static bool InPin(Vector2 p, float shrink)
    {
        Vector2 c = new Vector2(0f, 0.3f);
        float r = 0.62f - shrink;
        if ((p - c).magnitude <= r) return true;
        // point: a triangle from the head's sides down to the tip
        float tipY = -0.95f + shrink * 1.6f;
        if (p.y < tipY || p.y > c.y) return false;
        float t = (p.y - tipY) / (c.y - tipY);       // 0 at the tip, 1 at the head's centre
        return Mathf.Abs(p.x) <= r * 0.86f * t;
    }

    private static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        return inside;
    }

    private static Sprite MakeSprite(int size, System.Func<float, float, float> alpha)
    {
        var tex = NewTex(size);
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float x = (px + 0.5f) / size * 2f - 1f, y = (py + 0.5f) / size * 2f - 1f;
                tex.SetPixel(px, py, new Color(1f, 1f, 1f, alpha(x, y)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Texture2D NewTex(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
    }

#if UNITY_EDITOR
    // Used by the baker
    public void SetIcons(Sprite missionStart, Sprite objective, IconRule[] rules, Place[] placeList, TMP_FontAsset title)
    {
        titleFont = title;
        missionStartIcon = missionStart;
        objectiveIcon = objective;
        iconRules = rules;
        places = placeList;
    }

    public void SetArea(Vector2 min, Vector2 size)
    {
        worldMin = min;
        worldSize = size;
    }
#endif
}
