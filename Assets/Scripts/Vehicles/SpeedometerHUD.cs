using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Dashboard HUD for an all-in-one gauge cluster image (RPM + speed + fuel
// drawn together) with separate needle images. Builds its own UI - put it
// on the Managers prefab root, assign the sprites, and tune positions/
// angles live in Play mode.
//
// Needle rotation point = the needle sprite's PIVOT (set in the Sprite
// Editor), or the Pivot Override below (0-1 across the needle image).
//
// TUNING: tick "Tuning Mode", press Play - the dashboard shows even on foot
// and the needles follow the Test sliders (0 = zero/empty, 1 = max/full).
// Changes made in Play mode are lost when you stop: right-click the
// component -> Copy Component, stop, right-click -> Paste Component Values.
public class SpeedometerHUD : MonoBehaviour
{
    [Header("Art")]
    [Tooltip("The whole dashboard image (all gauges, no needles)")]
    [SerializeField] private Sprite dashboard;
    [SerializeField] private Sprite speedNeedle;
    [SerializeField] private Sprite rpmNeedle;
    [SerializeField] private Sprite fuelNeedle;
    [Tooltip("Optional: a small fuel icon that blinks when low (white art is best). Empty = a built-in glowing lamp")]
    [SerializeField] private Sprite lowFuelIcon;
    [Tooltip("Colour of the low-fuel light when lit")]
    [SerializeField] private Color lowFuelColor = new Color(1f, 0.32f, 0.08f);

    [Header("Size & placement")]
    [Tooltip("On-screen width of the dashboard (height follows the image)")]
    [SerializeField] private float dashboardWidth = 620f;
    [Tooltip("Distance from the bottom-right screen corner")]
    [SerializeField] private Vector2 screenMargin = new Vector2(30f, 30f);

    [Header("Gauge centers (0-1 across the dashboard image, from bottom-left)")]
    [SerializeField] private Vector2 speedCenter = new Vector2(0.5f, 0.52f);
    [SerializeField] private Vector2 rpmCenter = new Vector2(0.19f, 0.47f);
    [SerializeField] private Vector2 fuelCenter = new Vector2(0.83f, 0.47f);
    [SerializeField] private Vector2 lowFuelIconPosition = new Vector2(0.78f, 0.3f);

    [Header("Needle sizes (1 = same scale as the dashboard art)")]
    [SerializeField] private float speedNeedleScale = 1f;
    [SerializeField] private float rpmNeedleScale = 0.7f;
    [SerializeField] private float fuelNeedleScale = 0.5f;
    [SerializeField] private float lowFuelIconSize = 28f;

    [Header("Needle pivots (0-1 across the needle image, -1 = use the sprite's pivot)")]
    [SerializeField] private Vector2 speedNeedlePivot = new Vector2(-1f, -1f);
    [SerializeField] private Vector2 rpmNeedlePivot = new Vector2(-1f, -1f);
    [SerializeField] private Vector2 fuelNeedlePivot = new Vector2(-1f, -1f);

    [Header("Tuning (Play mode)")]
    [Tooltip("Show the dashboard all the time and drive the needles with the sliders below")]
    [SerializeField] private bool tuningMode = false;
    [Range(0f, 1f)] [SerializeField] private float testSpeed = 0f;
    [Range(0f, 1f)] [SerializeField] private float testRpm = 0f;
    [Range(0f, 1f)] [SerializeField] private float testFuel = 0f;

    [Header("Speed needle")]
    [SerializeField] private bool useMph = true;
    [Tooltip("Highest number printed on the speedometer")]
    [SerializeField] private float dialMaxSpeed = 260f;
    [Tooltip("Needle rotation (degrees) at 0 - tune until it points at '0'")]
    [SerializeField] private float speedAngleAtZero = 200f;
    [Tooltip("Needle rotation at max - tune until it points at the top number")]
    [SerializeField] private float speedAngleAtMax = -40f;

    [Header("RPM needle (simulated)")]
    [SerializeField] private float rpmAngleAtZero = 200f;
    [SerializeField] private float rpmAngleAtMax = -40f;
    [Tooltip("Number of gears - RPM drops at each shift")]
    [SerializeField] private int gears = 5;
    [Range(0f, 1f)] [SerializeField] private float idleRpm = 0.12f;

    [Header("Fuel needle")]
    [Tooltip("Rotation when EMPTY - tune until it points at 'E'")]
    [SerializeField] private float fuelAngleEmpty = 140f;
    [Tooltip("Rotation when FULL - tune until it points at 'F'")]
    [SerializeField] private float fuelAngleFull = 40f;
    [Range(0f, 1f)] [SerializeField] private float lowFuelBelow = 0.15f;

    [Header("Extras")]
    [SerializeField] private bool showDigitalSpeed = false;
    [Tooltip("Where the digital speed number sits (0-1 across the dashboard image)")]
    [SerializeField] private Vector2 digitalSpeedPosition = new Vector2(0.52f, 0.33f);
    [SerializeField] private float digitalSpeedSize = 30f;
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float smoothing = 6f;

    private GameObject root;
    private RectTransform dash;
    private RectTransform speedNeedleRect, rpmNeedleRect, fuelNeedleRect, lowFuelRect;
    private Image lowFuelImage, lowFuelGlow;
    private TMP_Text speedText;
    private GameObject healthGroup;
    private RectTransform healthFill;
    private Image healthFillImage;

    private VehicleInteraction trackedCar;
    private Rigidbody carBody;
    private VehicleFuel carFuel;
    private VehicleHealth carHealth;
    private float shownSpeed, shownRpm, shownFuel = 1f;

    private void Awake()
    {
        BuildUI();
        root.SetActive(false);
    }

    private void Update()
    {
        if (tuningMode)
        {
            if (!root.activeSelf) root.SetActive(true);
            Layout();
            SetAngle(speedNeedleRect, Mathf.Lerp(speedAngleAtZero, speedAngleAtMax, testSpeed));
            SetAngle(rpmNeedleRect, Mathf.Lerp(rpmAngleAtZero, rpmAngleAtMax, testRpm));
            SetAngle(fuelNeedleRect, Mathf.Lerp(fuelAngleEmpty, fuelAngleFull, testFuel));
            if (fuelNeedleRect != null) fuelNeedleRect.gameObject.SetActive(true);
            // Low-fuel light lit while Test Fuel is in the low zone, so it's easy to place
            bool testLow = testFuel <= lowFuelBelow;
            if (lowFuelImage != null) lowFuelImage.color = testLow ? lowFuelColor : new Color(0.3f, 0.09f, 0.05f, 0.9f);
            if (lowFuelGlow != null) lowFuelGlow.color = testLow ? new Color(lowFuelColor.r, lowFuelColor.g, lowFuelColor.b, 0.6f) : Color.clear;
            if (speedText != null) speedText.text = Mathf.RoundToInt(testSpeed * dialMaxSpeed).ToString();
            trackedCar = null;
            return;
        }

        VehicleInteraction car = VehicleInteraction.Current;
        bool show = car != null && !CameraMode.IsActive;
        if (root.activeSelf != show) root.SetActive(show);
        if (!show) { trackedCar = null; return; }

        if (car != trackedCar)
        {
            trackedCar = car;
            carBody = car.GetComponent<Rigidbody>();
            carFuel = car.GetComponent<VehicleFuel>();
            carHealth = car.GetComponent<VehicleHealth>();
            if (fuelNeedleRect != null) fuelNeedleRect.gameObject.SetActive(carFuel != null);
            if (lowFuelRect != null) lowFuelRect.gameObject.SetActive(carFuel != null);
            if (lowFuelGlow != null) lowFuelGlow.gameObject.SetActive(carFuel != null);
            if (healthGroup != null) healthGroup.SetActive(carHealth != null);
            shownFuel = carFuel != null ? carFuel.Fuel01 : 1f;
        }

        Layout(); // lets you tune positions/sizes live in Play mode
        float dt = Time.unscaledDeltaTime;

        // Speed
        float mps = carBody != null ? carBody.linearVelocity.magnitude : 0f;
        float speed = mps * (useMph ? 2.237f : 3.6f);
        shownSpeed = Mathf.Lerp(shownSpeed, speed, smoothing * dt);
        float speed01 = Mathf.Clamp01(shownSpeed / Mathf.Max(1f, dialMaxSpeed));
        SetAngle(speedNeedleRect, Mathf.Lerp(speedAngleAtZero, speedAngleAtMax, speed01));
        if (speedText != null) speedText.text = Mathf.RoundToInt(shownSpeed).ToString();

        // RPM: climbs through each "gear", drops at the shift
        float rpm01 = idleRpm;
        if (speed01 > 0.01f)
        {
            float gearSpan = 1f / Mathf.Max(1, gears);
            float inGear = (speed01 % gearSpan) / gearSpan;
            if (speed01 >= 0.999f) inGear = 1f;
            rpm01 = Mathf.Lerp(0.35f, 0.92f, inGear);
        }
        rpm01 += Mathf.Sin(Time.unscaledTime * 40f) * 0.005f; // a tiny engine shimmer
        shownRpm = Mathf.Lerp(shownRpm, rpm01, 8f * dt);
        SetAngle(rpmNeedleRect, Mathf.Lerp(rpmAngleAtZero, rpmAngleAtMax, shownRpm));

        // Fuel
        if (carFuel != null)
        {
            shownFuel = Mathf.Lerp(shownFuel, carFuel.Fuel01, 3f * dt);
            SetAngle(fuelNeedleRect, Mathf.Lerp(fuelAngleEmpty, fuelAngleFull, shownFuel));

            if (lowFuelImage != null)
            {
                // Low: blinks. Empty: stays lit. Otherwise a dark, unlit lamp.
                bool low = carFuel.Fuel01 <= lowFuelBelow;
                bool lit = low && (carFuel.IsEmpty || (Time.unscaledTime * 2.5f) % 1f < 0.55f);
                Color off = lowFuelIcon != null ? new Color(1f, 1f, 1f, 0f) : new Color(0.3f, 0.09f, 0.05f, 0.9f);
                lowFuelImage.color = lit ? lowFuelColor : off;
                if (lowFuelGlow != null) lowFuelGlow.color = lit ? new Color(lowFuelColor.r, lowFuelColor.g, lowFuelColor.b, 0.6f) : Color.clear;
            }
        }

        // Health
        if (carHealth != null && healthFill != null)
        {
            float h = carHealth.Health01;
            healthFill.anchorMax = new Vector2(Mathf.Clamp01(h), 1f);
            healthFillImage.color = h > 0.5f
                ? Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.9f, 0.35f), (h - 0.5f) * 2f)
                : Color.Lerp(new Color(1f, 0.2f, 0.2f), new Color(1f, 0.85f, 0.2f), h * 2f);
        }
    }

    private static void SetAngle(RectTransform needle, float angle)
    {
        if (needle != null) needle.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    // ---------------- layout ----------------

    private float ArtScale => dashboard != null ? dashboardWidth / dashboard.rect.width : 1f;
    private Vector2 DashSize => dashboard != null
        ? dashboard.rect.size * ArtScale
        : new Vector2(dashboardWidth, dashboardWidth * 0.4f);

    private void Layout()
    {
        dash.sizeDelta = DashSize;
        dash.anchoredPosition = new Vector2(-screenMargin.x, screenMargin.y);

        PlaceNeedle(speedNeedleRect, speedNeedle, speedCenter, speedNeedleScale, speedNeedlePivot);
        PlaceNeedle(rpmNeedleRect, rpmNeedle, rpmCenter, rpmNeedleScale, rpmNeedlePivot);
        PlaceNeedle(fuelNeedleRect, fuelNeedle, fuelCenter, fuelNeedleScale, fuelNeedlePivot);

        if (lowFuelRect != null)
        {
            lowFuelRect.anchoredPosition = Vector2.Scale(lowFuelIconPosition, DashSize);
            lowFuelRect.sizeDelta = Vector2.one * lowFuelIconSize;
            if (lowFuelGlow != null)
            {
                lowFuelGlow.rectTransform.anchoredPosition = lowFuelRect.anchoredPosition;
                lowFuelGlow.rectTransform.sizeDelta = Vector2.one * lowFuelIconSize * 2.6f;
            }
        }

        if (healthGroup != null)
        {
            RectTransform track = (RectTransform)healthGroup.transform;
            track.sizeDelta = new Vector2(DashSize.x * 0.5f, 8f);
            track.anchoredPosition = new Vector2(-screenMargin.x - DashSize.x * 0.25f, Mathf.Max(4f, screenMargin.y - 14f));
        }

        if (speedText != null)
        {
            ((RectTransform)speedText.transform).anchoredPosition = Vector2.Scale(digitalSpeedPosition, DashSize);
            speedText.fontSize = digitalSpeedSize;
        }
    }

    private void PlaceNeedle(RectTransform rt, Sprite sprite, Vector2 center01, float scale, Vector2 pivotOverride)
    {
        if (rt == null) return;
        rt.pivot = NeedlePivot(sprite, pivotOverride);
        rt.anchoredPosition = Vector2.Scale(center01, DashSize);
        rt.sizeDelta = sprite != null ? sprite.rect.size * ArtScale * scale : new Vector2(8f, 80f) * scale;
    }

    // ---------------- building ----------------

    private void BuildUI()
    {
        root = new GameObject("DashboardHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -5; // menus, notes and results draw on top

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        dash = MakeImage("Dashboard", root.transform, dashboard,
                         dashboard != null ? Color.white : new Color(0f, 0f, 0f, 0.55f));
        dash.anchorMin = dash.anchorMax = dash.pivot = new Vector2(1f, 0f);

        // Children of the dashboard are positioned from its bottom-left corner
        rpmNeedleRect = MakeNeedle("RpmNeedle", rpmNeedle);
        speedNeedleRect = MakeNeedle("SpeedNeedle", speedNeedle);
        fuelNeedleRect = MakeNeedle("FuelNeedle", fuelNeedle);

        // Low-fuel warning light: your icon, or a small glowing lamp (a shaded
        // sphere with a soft halo behind it - made first so it draws underneath)
        if (lowFuelIcon == null)
        {
            RectTransform glow = MakeImage("LowFuelGlow", dash, GlowSprite(), Color.clear);
            glow.anchorMin = glow.anchorMax = Vector2.zero;
            lowFuelGlow = glow.GetComponent<Image>();
        }
        lowFuelRect = MakeImage("LowFuelLight", dash, lowFuelIcon != null ? lowFuelIcon : LampSprite(), new Color(1f, 1f, 1f, 0f));
        lowFuelRect.anchorMin = lowFuelRect.anchorMax = Vector2.zero;
        lowFuelImage = lowFuelRect.GetComponent<Image>();

        if (showDigitalSpeed)
        {
            RectTransform t = MakeRect("Speed", dash);
            t.anchorMin = t.anchorMax = Vector2.zero;
            t.sizeDelta = new Vector2(120f, 40f);
            speedText = t.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) speedText.font = font;
            speedText.fontSize = 30;
            speedText.alignment = TextAlignmentOptions.Center;
            speedText.raycastTarget = false;
        }

        if (showHealthBar)
        {
            RectTransform track = MakeImage("HealthBar", root.transform, null, new Color(1f, 1f, 1f, 0.15f));
            track.anchorMin = track.anchorMax = track.pivot = new Vector2(1f, 0f);
            healthGroup = track.gameObject;

            RectTransform fill = MakeImage("Fill", track, null, new Color(0.3f, 0.9f, 0.35f));
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            fill.pivot = new Vector2(0f, 0.5f);
            healthFill = fill;
            healthFillImage = fill.GetComponent<Image>();
        }

        Layout();
    }

    // A little shaded sphere: white highlight top-left, darker rim. Drawn in
    // greys so the Image colour tints it.
    private static Sprite LampSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Vector2 c = new Vector2(size - 1, size - 1) * 0.5f;
        Vector2 highlight = c + new Vector2(-size * 0.16f, size * 0.18f);
        float r = size * 0.5f - 1f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(x, y);
            float d = Vector2.Distance(p, c) / r;                    // 0 centre .. 1 edge
            float alpha = Mathf.Clamp01((1f - d) * r);               // smooth edge
            float shade = Mathf.Lerp(1f, 0.45f, d * d);              // darker towards the rim
            float spot = Mathf.Clamp01(1f - Vector2.Distance(p, highlight) / (size * 0.22f));
            float v = Mathf.Clamp01(shade + spot * spot * 0.9f);
            tex.SetPixel(x, y, new Color(v, Mathf.Lerp(v, 1f, spot * spot), Mathf.Lerp(v, 1f, spot * spot), alpha));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // Soft round halo, fully transparent at the edge
    private static Sprite GlowSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Vector2 c = new Vector2(size - 1, size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), c) / (size * 0.5f));
            float a = (1f - d) * (1f - d);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private RectTransform MakeNeedle(string name, Sprite sprite)
    {
        RectTransform rt = MakeImage(name, dash, sprite, sprite != null ? Color.white : new Color(1f, 0.3f, 0.3f));
        rt.anchorMin = rt.anchorMax = Vector2.zero; // positioned from the dashboard's bottom-left

        return rt; // pivot is set in Layout
    }

    // Rotation point: the override if set, else the sprite's own pivot (Sprite Editor)
    private static Vector2 NeedlePivot(Sprite sprite, Vector2 pivotOverride)
    {
        if (pivotOverride.x >= 0f && pivotOverride.y >= 0f) return pivotOverride;
        return sprite != null
            ? new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height)
            : new Vector2(0.5f, 0.1f);
    }

    private RectTransform MakeImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = MakeRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    private RectTransform MakeRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}