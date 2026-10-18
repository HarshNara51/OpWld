using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Gas Station EMP charge meter: a dark "device" panel with a pulsing cyan border,
// a row of segments that light up electric cyan -> violet -> white-hot as the charge
// builds, an animated electric crackle over the lit part, and a status read-out
// (STANDBY / CHARGING / SIGNAL LOST / PULSE READY). Reads everything from TruckEMP.
// Built by the editor setup; segments are the children of Segments Root.
public class EMPMeterUI : MonoBehaviour
{
    [SerializeField] private TruckEMP emp;
    [SerializeField] private RectTransform segmentsRoot;
    [SerializeField] private Image glowBorder;
    [SerializeField] private RawImage crackle;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text percentText;
    [SerializeField] private RectTransform shakeRoot;

    [Header("Colours")]
    [SerializeField] private Color lowColor = new Color(0.1f, 0.85f, 1f);
    [SerializeField] private Color midColor = new Color(0.55f, 0.35f, 1f);
    [SerializeField] private Color highColor = new Color(0.92f, 0.95f, 1f);
    [SerializeField] private Color offColor = new Color(0.06f, 0.12f, 0.2f, 1f);
    [SerializeField] private Color lostColor = new Color(1f, 0.22f, 0.25f);

    private Image[] segments;
    private Vector2 shakeHome;
    private float shown01;

    private void Awake()
    {
        if (segmentsRoot != null)
        {
            segments = new Image[segmentsRoot.childCount];
            for (int i = 0; i < segments.Length; i++) segments[i] = segmentsRoot.GetChild(i).GetComponent<Image>();
        }
        if (shakeRoot != null) shakeHome = shakeRoot.anchoredPosition;
    }

    private void Update()
    {
        if (emp == null || segments == null) return;

        float t = Time.unscaledTime;
        bool lost = emp.IsDisrupted;
        bool charging = emp.IsCharging;
        bool ready = emp.Percent01 >= 1f;
        shown01 = Mathf.MoveTowards(shown01, emp.Percent01, Time.unscaledDeltaTime * 1.5f); // smooth

        // Segments: lit part ramps cyan -> violet -> white-hot; the leading cell flickers
        int count = segments.Length;
        float litF = shown01 * count;
        int lit = Mathf.FloorToInt(litF);
        for (int i = 0; i < count; i++)
        {
            float pos = (i + 0.5f) / count;
            Color on = pos < 0.6f ? Color.Lerp(lowColor, midColor, pos / 0.6f) : Color.Lerp(midColor, highColor, (pos - 0.6f) / 0.4f);
            Color c;
            if (lost) c = (i < lit) ? Color.Lerp(lostColor, offColor, (Mathf.Sin(t * 40f + i * 1.7f) > 0.2f) ? 0f : 0.7f) : offColor;
            else if (i < lit) c = on * (charging ? 0.9f + 0.1f * Mathf.Sin(t * 12f - i * 0.6f) : 0.75f);
            else if (i == lit && charging) c = Color.Lerp(offColor, highColor, (litF - lit) * (0.6f + 0.4f * Mathf.PerlinNoise(t * 25f, i)));
            else c = offColor;
            c.a = 1f;
            segments[i].color = c;
        }

        // Border glow: slow breathe on standby, fast pulse while charging, red flicker when lost
        if (glowBorder != null)
        {
            Color g = lost ? lostColor : ready ? highColor : lowColor;
            float a = lost ? (Mathf.PerlinNoise(t * 18f, 0f) > 0.5f ? 0.9f : 0.2f)
                    : charging ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 6f))
                    : ready ? 1f
                    : 0.25f + 0.15f * Mathf.Sin(t * 1.5f);
            g.a = a;
            glowBorder.color = g;
        }

        // Electric crackle sliding across the lit cells while charging
        if (crackle != null)
        {
            var rt = crackle.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Max(0.001f, shown01), rt.anchorMax.y);
            Rect uv = crackle.uvRect;
            uv.x = -t * 1.6f;
            uv.width = Mathf.Max(0.05f, shown01 * 3f);
            uv.y = Mathf.Floor(t * 15f) * 0.37f; // jumps between bolt shapes
            crackle.uvRect = uv;
            Color cc = lost ? lostColor : highColor;
            cc.a = charging || lost ? 0.85f : ready ? 0.5f : 0f;
            crackle.color = cc;
        }

        if (statusText != null)
        {
            statusText.text = ready ? "PULSE READY" : lost ? "SIGNAL LOST" : charging ? "CHARGING" : "STANDBY";
            statusText.color = lost ? lostColor : ready ? highColor : charging ? lowColor : new Color(0.45f, 0.6f, 0.75f);
        }
        if (percentText != null)
        {
            percentText.text = Mathf.FloorToInt(shown01 * 100f) + "%";
            percentText.color = lost ? lostColor : Color.Lerp(lowColor, highColor, shown01);
        }

        // Glitch shake while the signal is lost
        if (shakeRoot != null)
            shakeRoot.anchoredPosition = shakeHome + (lost ? new Vector2(Random.Range(-3f, 3f), Random.Range(-2f, 2f)) : Vector2.zero);
    }
}
