using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Spider-Man style "Return to the mission area". Put this on an empty
// GameObject at the middle of the fight and set the Radius (drawn as a
// circle in the Scene view). Only active during the shootout: from the
// first shot until enough enemies are down for the truck to flee. Leave
// the circle and a countdown starts; come back and it resets; let it run
// out and the mission fails. A glowing red ring on the ground shows the
// edge during the shootout. Builds its own UI and ring - nothing to wire.
public class MissionAreaBoundary : MonoBehaviour
{
    [SerializeField] private float radius = 75f;
    [SerializeField] private float warningSeconds = 10f;
    [SerializeField] private string warningText = "RETURN TO THE MISSION AREA";
    [SerializeField] private string failReason = "You abandoned the shootout";

    [Header("Ground ring")]
    [SerializeField] private bool showRing = true;
    [SerializeField] private Color ringColor = new Color(1f, 0.15f, 0.1f, 0.85f);
    [SerializeField] private float ringWidth = 0.35f;
    [Tooltip("Layers the ring sits on (the ground). Defaults to everything.")]
    [SerializeField] private LayerMask groundMask = ~0;

    private Transform player;
    private float timeLeft;
    private GameObject warningUI;
    private TMP_Text label;
    private Image tint;
    private LineRenderer ring;

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        timeLeft = warningSeconds;
        BuildWarningUI();
        if (showRing) BuildRing();
    }

    private void Update()
    {
        Transform target = Target();
        bool on = IsShootoutOn();
        if (ring != null)
        {
            if (ring.enabled != on) ring.enabled = on;
            if (on) // gentle pulse so it reads as a boundary, not scenery
            {
                Color c = ringColor;
                c.a *= 0.65f + 0.35f * Mathf.Sin(Time.time * 3f);
                ring.startColor = ring.endColor = c;
            }
        }
        bool outside = target != null && on && FlatDistance(target.position) > radius;

        if (!outside)
        {
            timeLeft = warningSeconds;
            warningUI.SetActive(false);
            return;
        }

        timeLeft -= Time.deltaTime;
        warningUI.SetActive(true);
        label.text = $"{warningText}\n<size=140%>{Mathf.CeilToInt(Mathf.Max(0f, timeLeft))}</size>";
        tint.color = new Color(0.8f, 0f, 0f, 0.12f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f)); // soft pulse

        if (timeLeft <= 0f)
        {
            warningUI.SetActive(false);
            enabled = false;
            Mission4Manager.Instance.FailMission(failReason);
        }
    }

    private bool IsShootoutOn()
    {
        Mission4Manager m = Mission4Manager.Instance;
        return m != null && m.CurrentState == Mission4Manager.MissionState.InProgress &&
               EnemyCover.CombatActive && !m.ShootoutOver;
    }

    // The player on foot, or whatever they're driving
    private Transform Target()
    {
        if (player != null && player.gameObject.activeInHierarchy) return player;
        return VehicleInteraction.Current != null ? VehicleInteraction.Current.transform : null;
    }

    private float FlatDistance(Vector3 pos)
    {
        Vector3 d = pos - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    private void BuildWarningUI()
    {
        warningUI = new GameObject("MissionAreaWarning");
        warningUI.transform.SetParent(transform, false);
        Canvas canvas = warningUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = warningUI.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Full-screen red tint
        GameObject tintGo = new GameObject("Tint", typeof(RectTransform));
        tintGo.transform.SetParent(warningUI.transform, false);
        tint = tintGo.AddComponent<Image>();
        tint.raycastTarget = false;
        RectTransform tr = tintGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;

        // Big centred message + countdown
        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(warningUI.transform, false);
        label = textGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 56f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.raycastTarget = false;
        RectTransform lr = textGo.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 0.55f); lr.anchorMax = new Vector2(1f, 0.85f);
        lr.offsetMin = lr.offsetMax = Vector2.zero;

        warningUI.SetActive(false);
    }

    // A circle of points laid on the ground (rays down, so it follows slopes)
    private void BuildRing()
    {
        int segments = Mathf.Clamp(Mathf.RoundToInt(radius * 2f), 48, 256);
        var go = new GameObject("MissionAreaRing");
        go.transform.SetParent(transform, false);
        ring = go.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = segments;
        ring.widthMultiplier = ringWidth;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.startColor = ring.endColor = ringColor;

        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            // Lowest hit = the ground, not a roof, canopy or tree above it
            float lowest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(p + Vector3.up * 50f, Vector3.down, 200f, groundMask, QueryTriggerInteraction.Ignore))
                lowest = Mathf.Min(lowest, hit.point.y);
            if (lowest < float.MaxValue) p.y = lowest;
            ring.SetPosition(i, p + Vector3.up * 0.15f);
        }
        ring.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        const int segments = 64;
        Vector3 prev = transform.position + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 next = transform.position + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
