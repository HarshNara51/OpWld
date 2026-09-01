using UnityEngine;

/// <summary>
/// Drop this on any active GameObject (e.g. Main Camera or Player) to get a
/// live, color-coded FPS overlay in Play Mode and in builds.
///
/// - Updates every 'updateInterval' seconds so the number is readable
///   instead of jittering every frame.
/// - Tracks the MIN fps seen since the last reset, so you can walk near
///   the trees, then check the worst dip that happened along the way
///   instead of having to catch it in the moment.
/// - Press R at any time to reset Min/Max tracking (e.g. right before you
///   start walking toward the forest, so the Min reading only reflects
///   that specific test run).
/// - Color: green = smooth, yellow = getting rough, red = lag.
///   Adjust goodFps / okFps below to whatever thresholds matter to you.
/// </summary>
public class FPSMonitor : MonoBehaviour
{
    [Header("Update Settings")]
    public float updateInterval = 0.5f;

    [Header("Color Thresholds")]
    public float goodFps = 50f;
    public float okFps = 30f;

    private float accumFrames = 0f;
    private float accumTime = 0f;
    private float currentFps = 0f;
    private float minFps = float.MaxValue;
    private float maxFps = 0f;

    private GUIStyle style;

    void Start()
    {
        style = new GUIStyle();
        style.fontSize = 32;
        style.fontStyle = FontStyle.Bold;
    }

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

        if (Input.GetKeyDown(KeyCode.R))
        {
            minFps = float.MaxValue;
            maxFps = 0f;
        }
    }

    void OnGUI()
    {
        style.normal.textColor = currentFps >= goodFps ? Color.green :
                                  currentFps >= okFps ? Color.yellow : Color.red;

        float frameMs = currentFps > 0f ? 1000f / currentFps : 0f;
        float displayMin = minFps == float.MaxValue ? 0f : minFps;

        string text =
            $"FPS: {currentFps:F1}\n" +
            $"Frame: {frameMs:F1} ms\n" +
            $"Min: {displayMin:F1}   Max: {maxFps:F1}\n" +
            $"(Press R to reset Min/Max)";

        GUI.Label(new Rect(20, 20, 320, 120), text, style);
    }
}