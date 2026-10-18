using UnityEngine;
using TMPro;

// Countdown for Mission1_Railway. Put this on an empty GameObject.
// Started by the locomotive's "On Arrived At Stop" event, so it only
// runs while the train is actually waiting at the platform. When it
// hits zero the train departs (the mission fails once it's gone).
public class TrainTimer : MonoBehaviour
{
    public static TrainTimer Instance { get; private set; }

    [Tooltip("Seconds the train waits at the platform before departing")]
    [SerializeField] private float durationSeconds = 180f;

    [Tooltip("The styled timer plate (shared mission-timer look). Takes priority over Timer Text.")]
    [SerializeField] private MissionTimerDisplay display;
    [SerializeField] private string caption = "TRAIN DEPARTS IN";
    [Tooltip("Fallback: plain on-screen countdown (a TextMeshPro text on the Canvas)")]
    [SerializeField] private TMP_Text timerText;

    private float remaining;
    private bool running;

    public bool IsRunning => running;

    private void Awake()
    {
        Instance = this;
        remaining = durationSeconds;
        if (timerText != null) timerText.text = ""; // hidden until the train arrives
        if (display != null) display.Hide();
    }

    public void StartTimer()
    {
        if (running) return;

        remaining = durationSeconds;
        running = true;
        if (display != null) display.Show(caption);
        UpdateText();
    }

    // Stops and hides the countdown (mission completed or failed early)
    public void StopTimer()
    {
        running = false;
        if (timerText != null) timerText.text = "";
        if (display != null) display.Hide();
    }

    private void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;

        if (remaining <= 0f)
        {
            remaining = 0f;
            running = false;
            UpdateText(); // leaves 00:00 on screen as the train pulls out
            Mission1Manager.Instance.OnTrainTimerExpired();
            return;
        }

        UpdateText();
    }

    private void UpdateText()
    {
        if (display != null) { display.SetSeconds(remaining); return; }
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }
}