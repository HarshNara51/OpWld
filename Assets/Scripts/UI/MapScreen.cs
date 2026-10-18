using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The big map (M, rebindable). Pauses the game like GTA's map:
//
//   MAP                                              LEGEND
//   ┌──────────────────────────────────────┐        ⌂  Home
//   │   north-up picture of the world,      │        ★  Mission start
//   │   every icon a bit bigger; hover one  │        ◆  Go here
//   │   to grow it and see its name         │        ○  Do something here
//   └──────────────────────────────────────┘        📍 Your waypoint
//   Left click an icon: set waypoint · Right click: drop a pin · ...
//
// Left click an icon = waypoint there (click it again to remove it). Right
// click anywhere = a pin there (right click the pin to remove it). Drag to
// move, scroll to zoom, M or Esc to close. Area signs aren't waypoints.
//
// Added automatically by the Minimap (same Managers object) and uses its
// picture, icons and fonts.
public class MapScreen : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private const float HoverGrow = 0.25f;
    private const float PlaceSize = 36f, ObjectiveSize = 30f, AreaSize = 66f, PinSize = 40f, ArrowSize = 32f;

    private class Icon
    {
        public RectTransform rect;
        public Image image;
        public string name;
        public Vector2 world;
        public float size;
        public float hover;
        public bool clickable;
        public GizmoLabel label;
        public Minimap.Place place;
    }

    private Minimap mm;
    private GameObject root;
    private RectTransform viewport, iconLayer, mapRect, tooltip, pin, player;
    private RawImage map;
    private TMP_Text tooltipName, tooltipHint;
    private readonly List<Icon> icons = new List<Icon>();
    private Icon pinIcon;

    private float zoom, minZoom, maxZoom;  // UI units per metre
    private Vector2 center;                // world X/Z in the middle of the view
    private bool pressing, dragging;
    private Vector2 pressPos, lastMouse;
    private float prevTimeScale = 1f;
    private CursorLockMode prevLock;
    private bool prevCursor;

    private void Start()
    {
        mm = GetComponent<Minimap>();
    }

    private void Update()
    {
        if (mm == null) return;
        if (!IsOpen)
        {
            if (Input.GetKeyDown(GameKeys.Get(GameAction.Map)) && CanOpen()) Open();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(GameKeys.Get(GameAction.Map))) { Close(); return; }
        HandleInput();
        Layout();
    }

    // Only during normal play: not paused, in a briefing, a puzzle, photo mode or a menu
    private bool CanOpen()
    {
        return mm.MapTexture != null && Time.timeScale > 0f && !PauseManager.IsPaused && !PauseManager.InputBlocked
               && !CameraMode.IsActive && Cursor.lockState == CursorLockMode.Locked && Me() != null;
    }

    private Transform Me()
    {
        if (VehicleInteraction.Current != null) return VehicleInteraction.Current.transform;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        return p != null ? p.transform : null;
    }

    // ---------- open / close ----------

    private void Open()
    {
        if (root == null) Build();
        IsOpen = true;
        root.SetActive(true);

        prevTimeScale = Time.timeScale;
        prevLock = Cursor.lockState;
        prevCursor = Cursor.visible;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PauseManager.InputBlocked = true; // Esc closes the map, not opens the pause menu

        map.texture = mm.MapTexture;
        Vector2 view = viewport.rect.size;
        minZoom = Mathf.Min(view.x / mm.WorldSize.x, view.y / mm.WorldSize.y);
        maxZoom = minZoom * 6f;
        zoom = minZoom * 2.2f;
        Transform me = Me();
        center = new Vector2(me.position.x, me.position.z);
        pressing = dragging = false;

        MakeIcons(center);
        Layout();
    }

    private void Close()
    {
        IsOpen = false;
        root.SetActive(false);
        foreach (Icon i in icons) Destroy(i.rect.gameObject);
        icons.Clear();

        Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
        Cursor.lockState = prevLock;
        Cursor.visible = prevCursor;
        StartCoroutine(UnblockNextFrame()); // the same Esc press mustn't open the pause menu
    }

    private IEnumerator UnblockNextFrame()
    {
        yield return null;
        if (!IsOpen) PauseManager.InputBlocked = false;
    }

    private void OnDisable()
    {
        if (IsOpen) { IsOpen = false; Time.timeScale = 1f; PauseManager.InputBlocked = false; }
    }

    // ---------- icons ----------

    private void MakeIcons(Vector2 from)
    {
        // Areas first (underneath), then places, then gizmos; mission stars on top
        foreach (Minimap.Place p in mm.Places)
            if (p != null && p.area && p.icon != null)
                AddIcon(p.icon, new Color(1f, 1f, 1f, Mathf.Clamp01(mm.AreaAlpha + 0.15f)), p.name, p.position, AreaSize, false).place = p;
        foreach (Minimap.Place p in mm.Places)
            if (p != null && !p.area && p.icon != null)
                AddIcon(p.icon, Color.white, p.name, p.position, PlaceSize, true).place = p;

        var stars = new List<GizmoLabel>();
        foreach (GizmoLabel l in GizmoLabel.All)
        {
            mm.Describe(l, out Sprite sprite, out bool tint, out bool hide, out bool star, out _);
            if (hide || !l.Visible || mm.IsTwinHidden(l, from)) continue;
            if (star) { stars.Add(l); continue; }
            Color c = tint ? new Color(l.color.r, l.color.g, l.color.b, 1f) : Color.white;
            AddIcon(sprite, c, l.title, Flat(l.Anchor), tint ? ObjectiveSize : PlaceSize, true).label = l;
        }
        foreach (GizmoLabel l in stars)
        {
            string name = string.IsNullOrEmpty(l.subtitle) ? l.title : l.title + "  ·  " + l.subtitle;
            AddIcon(mm.MissionStartIcon, Color.white, name, Flat(l.Anchor), PlaceSize, true).label = l;
        }

        player.SetAsLastSibling();
        pin.SetAsLastSibling();
    }

    private Icon AddIcon(Sprite sprite, Color color, string name, Vector2 world, float size, bool clickable)
    {
        Image img = Img("Icon " + name, iconLayer, sprite, color);
        img.preserveAspect = true;
        img.rectTransform.sizeDelta = new Vector2(size, size);
        var icon = new Icon { rect = img.rectTransform, image = img, name = name, world = world, size = size, clickable = clickable };
        icons.Add(icon);
        return icon;
    }

    private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

    // ---------- input ----------

    private void HandleInput()
    {
        Vector2 mouse = Input.mousePosition;
        bool inside = RectTransformUtility.RectangleContainsScreenPoint(viewport, mouse, null);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, mouse, null, out Vector2 local);
        local -= viewport.rect.center;

        // Zoom around the cursor
        float wheel = Input.mouseScrollDelta.y;
        if (inside && Mathf.Abs(wheel) > 0.01f)
        {
            Vector2 under = center + local / zoom;
            zoom = Mathf.Clamp(zoom * Mathf.Pow(1.18f, wheel), minZoom, maxZoom);
            center = under - local / zoom;
        }

        // Drag to move / click
        if (Input.GetMouseButtonDown(0) && inside) { pressing = true; dragging = false; pressPos = mouse; lastMouse = mouse; }
        if (pressing && Input.GetMouseButton(0))
        {
            if (!dragging && (mouse - pressPos).magnitude > 6f) dragging = true;
            if (dragging)
            {
                Vector2 delta = (mouse - lastMouse) / root.GetComponent<Canvas>().scaleFactor;
                center -= delta / zoom;
            }
            lastMouse = mouse;
        }

        Icon hovered = inside && !dragging ? IconUnder(local) : null;
        if (pressing && Input.GetMouseButtonUp(0))
        {
            if (!dragging && hovered != null && hovered.clickable) Toggle(hovered);
            pressing = dragging = false;
        }

        // Right click: drop a pin (or remove it)
        if (inside && Input.GetMouseButtonDown(1))
        {
            if (hovered == pinIcon || (hovered != null && IsWaypoint(hovered))) Waypoint.Clear();
            else Waypoint.Set(center + local / zoom);
        }

        // Hover: grow + name
        foreach (Icon i in icons) i.hover = Mathf.MoveTowards(i.hover, i == hovered ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        pinIcon.hover = Mathf.MoveTowards(pinIcon.hover, hovered == pinIcon ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        ShowTooltip(hovered, local);

        Vector2 half = mm.WorldSize * 0.5f;
        Vector2 mid = mm.WorldMin + half;
        center = new Vector2(Mathf.Clamp(center.x, mid.x - half.x, mid.x + half.x), Mathf.Clamp(center.y, mid.y - half.y, mid.y + half.y));
    }

    private bool IsWaypoint(Icon i) => (i.label != null && Waypoint.Is(i.label)) || (i.place != null && Waypoint.Is(i.place));

    private void Toggle(Icon i)
    {
        if (IsWaypoint(i)) { Waypoint.Clear(); return; }
        if (i.label != null) Waypoint.Set(i.label);
        else if (i.place != null) Waypoint.Set(i.place);
    }

    // Topmost icon under the cursor (the pin counts too)
    private Icon IconUnder(Vector2 local)
    {
        if (pin.gameObject.activeSelf && (pin.anchoredPosition + new Vector2(0f, PinSize * 0.45f) - local).magnitude < PinSize * 0.45f) return pinIcon;
        for (int k = icons.Count - 1; k >= 0; k--)
        {
            Icon i = icons[k];
            float r = i.size * (i.place != null && i.place.area ? 0.4f : 0.55f);
            if ((i.rect.anchoredPosition - local).magnitude <= r) return i;
        }
        return null;
    }

    private void ShowTooltip(Icon i, Vector2 local)
    {
        bool on = i != null;
        if (tooltip.gameObject.activeSelf != on) tooltip.gameObject.SetActive(on);
        if (!on) return;

        tooltipName.text = i == pinIcon ? "Your waypoint" : i.name;
        if (i == pinIcon) tooltipHint.text = "Right click: remove";
        else if (!i.clickable) tooltipHint.text = "Area";
        else tooltipHint.text = IsWaypoint(i) ? "Click: remove waypoint" : "Click: set waypoint";

        float w = Mathf.Max(tooltipName.preferredWidth, tooltipHint.preferredWidth) + 28f;
        tooltip.sizeDelta = new Vector2(w, 58f);
        Vector2 p = local + new Vector2(24f, 22f);
        Vector2 half = viewport.rect.size * 0.5f;
        if (p.x + w > half.x) p.x = local.x - 24f - w;   // keep it inside the view
        if (p.y + 58f > half.y) p.y = local.y - 22f - 58f;
        tooltip.anchoredPosition = p;
    }

    // ---------- layout every frame ----------

    private void Layout()
    {
        mapRect.sizeDelta = mm.WorldSize * zoom;
        mapRect.anchoredPosition = (mm.WorldMin - center) * zoom;

        float iconScale = Mathf.Lerp(0.85f, 1.15f, Mathf.InverseLerp(minZoom, maxZoom, zoom));
        foreach (Icon i in icons)
        {
            i.rect.anchoredPosition = (i.world - center) * zoom;
            i.rect.localScale = Vector3.one * iconScale * (1f + HoverGrow * Smooth(i.hover));
        }

        Transform me = Me();
        player.gameObject.SetActive(me != null);
        if (me != null)
        {
            player.anchoredPosition = (Flat(me.position) - center) * zoom;
            player.localRotation = Quaternion.Euler(0f, 0f, -me.eulerAngles.y);
        }

        bool hasPin = Waypoint.Active;
        if (pin.gameObject.activeSelf != hasPin) pin.gameObject.SetActive(hasPin);
        if (hasPin)
        {
            pin.anchoredPosition = (Flat(Waypoint.Position) - center) * zoom;
            pin.localScale = Vector3.one * (1f + HoverGrow * Smooth(pinIcon.hover));
        }
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    // ---------- building ----------

    private void Build()
    {
        root = new GameObject("MapScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Color accent = mm.AccentColor;
        Image back = Img("Backdrop", root.transform, null, new Color(0.03f, 0.035f, 0.05f, 0.95f));
        Stretch(back.rectTransform, Vector2.zero, Vector2.zero);

        TMP_Text title = Text("Title", root.transform, "MAP", mm.TitleFont, 78f, accent, TextAlignmentOptions.Left);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.sizeDelta = new Vector2(600f, 90f);
        title.rectTransform.anchoredPosition = new Vector2(64f, -14f);

        // Frame + view
        Image frame = Img("Frame", root.transform, null, new Color(accent.r, accent.g, accent.b, 0.55f));
        Stretch(frame.rectTransform, new Vector2(58f, 84f), new Vector2(-414f, -104f));
        Image view = Img("View", root.transform, null, new Color(0.09f, 0.17f, 0.24f));
        viewport = view.rectTransform;
        Stretch(viewport, new Vector2(61f, 87f), new Vector2(-417f, -107f));
        view.gameObject.AddComponent<RectMask2D>();

        mapRect = Rect("Map", viewport);
        mapRect.anchorMin = mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapRect.pivot = Vector2.zero;
        map = mapRect.gameObject.AddComponent<RawImage>();
        map.raycastTarget = false;

        iconLayer = Rect("Icons", viewport);
        iconLayer.anchorMin = iconLayer.anchorMax = new Vector2(0.5f, 0.5f);
        iconLayer.sizeDelta = Vector2.zero;

        player = Img("You", iconLayer, mm.PlayerArrowSprite, Color.white).rectTransform;
        player.sizeDelta = new Vector2(ArrowSize, ArrowSize);

        Image pinImg = Img("Waypoint", iconLayer, mm.PinSprite, mm.WaypointColor);
        pin = pinImg.rectTransform;
        pin.pivot = new Vector2(0.5f, 0.03f); // the tip sits on the spot
        pin.sizeDelta = new Vector2(PinSize, PinSize);
        pinIcon = new Icon { rect = pin, image = pinImg, name = "Your waypoint", size = PinSize, clickable = false };

        // Tooltip
        Image tip = Img("Tooltip", viewport, null, new Color(0.04f, 0.045f, 0.06f, 0.92f));
        tooltip = tip.rectTransform;
        tooltip.anchorMin = tooltip.anchorMax = new Vector2(0.5f, 0.5f);
        tooltip.pivot = Vector2.zero;
        Image tipLine = Img("Line", tooltip, null, accent);
        tipLine.rectTransform.anchorMin = new Vector2(0f, 0f);
        tipLine.rectTransform.anchorMax = new Vector2(0f, 1f);
        tipLine.rectTransform.pivot = new Vector2(0f, 0.5f);
        tipLine.rectTransform.sizeDelta = new Vector2(3f, 0f);
        tooltipName = Text("Name", tooltip, "", mm.Font, 22f, Color.white, TextAlignmentOptions.TopLeft);
        tooltipName.fontStyle = FontStyles.Bold;
        Stretch(tooltipName.rectTransform, new Vector2(14f, 26f), new Vector2(-6f, -4f));
        tooltipHint = Text("Hint", tooltip, "", mm.Font, 15f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.BottomLeft);
        Stretch(tooltipHint.rectTransform, new Vector2(14f, 6f), new Vector2(-6f, -30f));
        tooltip.gameObject.SetActive(false);

        BuildLegend(accent);

        string key = GameKeys.Label(GameAction.Map);
        TMP_Text hint = Text("Hint", root.transform,
            $"Left click an icon: set waypoint   ·   Right click: drop a pin   ·   Drag: move   ·   Scroll: zoom   ·   {key} / Esc: close",
            mm.Font, 19f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center);
        hint.rectTransform.anchorMin = new Vector2(0f, 0f);
        hint.rectTransform.anchorMax = new Vector2(1f, 0f);
        hint.rectTransform.pivot = new Vector2(0.5f, 0f);
        hint.rectTransform.sizeDelta = new Vector2(-356f, 40f);
        hint.rectTransform.anchoredPosition = new Vector2(-178f, 30f);

        root.SetActive(false);
    }

    private void BuildLegend(Color accent)
    {
        RectTransform panel = Rect("Legend", root.transform);
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.sizeDelta = new Vector2(330f, -190f);
        panel.anchoredPosition = new Vector2(-48f, -104f);

        float y = 0f;
        TMP_Text head = Text("Title", panel, "LEGEND", mm.TitleFont, 46f, accent, TextAlignmentOptions.TopLeft);
        Row(head.rectTransform, ref y, 56f);

        foreach (Minimap.Place p in mm.Places)
            if (p != null && !p.area && p.icon != null) LegendRow(panel, p.icon, Color.white, p.name, ref y);
        if (mm.MissionStartIcon != null) LegendRow(panel, mm.MissionStartIcon, Color.white, "Mission start", ref y);
        if (mm.ObjectiveIcon != null) LegendRow(panel, mm.ObjectiveIcon, accent, "Go here", ref y);
        Sprite ring = mm.RuleIcon("Cargo 1");
        if (ring != null && ring != mm.ObjectiveIcon) LegendRow(panel, ring, accent, "Do something here", ref y);
        LegendRow(panel, mm.PinSprite, mm.WaypointColor, "Your waypoint", ref y);
        LegendRow(panel, mm.PlayerArrowSprite, Color.white, "You", ref y);

        y -= 10f;
        TMP_Text areas = Text("Areas", panel, "AREAS", mm.TitleFont, 32f, accent, TextAlignmentOptions.TopLeft);
        Row(areas.rectTransform, ref y, 40f);
        foreach (Minimap.Place p in mm.Places)
            if (p != null && p.area && p.icon != null) LegendRow(panel, p.icon, new Color(1f, 1f, 1f, 0.75f), p.name, ref y);
    }

    private void LegendRow(RectTransform panel, Sprite icon, Color color, string label, ref float y)
    {
        RectTransform row = Rect("Row " + label, panel);
        Row(row, ref y, 38f);
        Image img = Img("Icon", row, icon, color);
        img.preserveAspect = true;
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        img.rectTransform.pivot = new Vector2(0f, 0.5f);
        img.rectTransform.sizeDelta = new Vector2(30f, 30f);
        TMP_Text t = Text("Label", row, label, mm.Font, 20f, new Color(0.92f, 0.93f, 0.95f), TextAlignmentOptions.Left);
        Stretch(t.rectTransform, new Vector2(46f, 0f), Vector2.zero);
    }

    private static void Row(RectTransform rt, ref float y, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = new Vector2(0f, y);
        y -= height;
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
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        return img;
    }

    private static TMP_Text Text(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = min;
        rt.offsetMax = max;
    }
}
