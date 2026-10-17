using UnityEngine;

/// <summary>
/// FPS counter (on the Main Camera prefab). Players switch it on/off with the
/// "Show FPS" button in Settings (saved, off by default) - see ShowFpsButton.
///
/// - Builds: a small colour-coded "118 FPS" in the top-right corner, clear of
///   the objective line (top-left).
/// - Editor: the detailed developer view - frame time and the MIN/MAX seen
///   since the last reset, so you can walk near the trees and check the worst
///   dip afterwards. Press F10 to reset Min/Max.
/// - Updates every 'updateInterval' seconds so the number is readable.
/// - Color: green = smooth, yellow = getting rough, red = lag.
/// </summary>
public class FPSMonitor : MonoBehaviour
{
    [Header("Update Settings")]
    public float updateInterval = 0.5f;

    [Header("Color Thresholds")]
    public float goodFps = 50f;
    public float okFps = 30f;

    const string PrefsKey = "ShowFPS";

    public static event System.Action<bool> ShowChanged;

    // The player's setting (Settings > Show FPS)
    public static bool Show
    {
        get => PlayerPrefs.GetInt(PrefsKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ShowChanged?.Invoke(value);
        }
    }

    private float accumFrames = 0f;
    private float accumTime = 0f;
    private float currentFps = 0f;
    private float minFps = float.MaxValue;
    private float maxFps = 0f;

    private GUIStyle style;
    private GUIStyle shadow;

    void Update()
    {
        accumTime += Time.unscaledDeltaTime;
        accumFrames++;

        if (accumTime >= updateInterval)
        {
            currentFps = accumFrames / accumTime;
            accumFrames = 0f;
            accumTime = 0f;

            if (currentFps < minFps) minFps = currentFps;
            if (currentFps > maxFps) maxFps = currentFps;
        }

        if (Application.isEditor && Input.GetKeyDown(KeyCode.F10))
        {
            minFps = float.MaxValue;
            maxFps = 0f;
        }
    }

    void OnGUI()
    {
        if (!Show) return;

        float scale = Screen.height / 1080f;
        if (style == null)
        {
            style = new GUIStyle { fontStyle = FontStyle.Bold };
            shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        }
        Color color = currentFps >= goodFps ? new Color(0.45f, 1f, 0.45f) :
                      currentFps >= okFps ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.35f, 0.3f);
        style.normal.textColor = color;

        if (Application.isEditor)
        {
            // Detailed developer view, top-right
            float frameMs = currentFps > 0f ? 1000f / currentFps : 0f;
            float displayMin = minFps == float.MaxValue ? 0f : minFps;
            string text =
                $"FPS: {currentFps:F1}\n" +
                $"Frame: {frameMs:F1} ms\n" +
                $"Min: {displayMin:F1}   Max: {maxFps:F1}\n" +
                $"(F10 resets Min/Max)";
            style.fontSize = shadow.fontSize = Mathf.RoundToInt(26 * scale);
            style.alignment = shadow.alignment = TextAnchor.UpperRight;
            Draw(new Rect(Screen.width - 420 * scale - 20 * scale, 20 * scale, 420 * scale, 140 * scale), text);
        }
        else
        {
            // Compact player view, top-right
            style.fontSize = shadow.fontSize = Mathf.RoundToInt(28 * scale);
            style.alignment = shadow.alignment = TextAnchor.UpperRight;
            Draw(new Rect(Screen.width - 220 * scale - 24 * scale, 18 * scale, 220 * scale, 40 * scale), $"{currentFps:F0} FPS");
        }
    }

    void Draw(Rect r, string text)
    {
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, shadow);
        GUI.Label(r, text, style);
    }
}
