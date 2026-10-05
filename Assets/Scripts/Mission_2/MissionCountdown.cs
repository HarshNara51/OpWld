using UnityEngine;
using UnityEngine.Events;
using TMPro;

// A reusable on-screen countdown for any mission. Something calls
// StartCountdown(); at zero it fires "On Finished" (wire it in the
// Inspector). E.g. the Hotel escape: "Cops arrive in 00:45".
public class MissionCountdown : MonoBehaviour
{
    [SerializeField] private float durationSeconds = 45f;

    [Tooltip("On-screen text (TextMeshPro on the mission's Canvas)")]
    [SerializeField] private TMP_Text timerText;

    [Tooltip("Shown before the time, e.g. 'Cops arrive in'")]
    [SerializeField] private string label = "Cops arrive in";

    [Tooltip("When 'On Midpoint' fires: 0.5 = halfway through the countdown")]
    [Range(0f, 1f)][SerializeField] private float midpointFraction = 0.5f;

    [Tooltip("Fires once, partway through (e.g. dispatch the cops)")]
    public UnityEvent onMidpoint;

    public UnityEvent onFinished;

    private float remaining;
    private bool running;
    private bool midpointFired;

    public bool IsRunning => running;

    private void Awake()
    {
        if (timerText != null) timerText.text = "";
    }

    public void StartCountdown()
    {
        remaining = durationSeconds;
        running = true;
        midpointFired = false;
        UpdateText();
    }

    // Stops and hides it (escaped, failed, ...)
    public void StopCountdown()
    {
        running = false;
        if (timerText != null) timerText.text = "";
    }

    private void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;

        if (!midpointFired && durationSeconds - remaining >= durationSeconds * midpointFraction)
        {
            midpointFired = true;
            onMidpoint.Invoke();
        }

        if (remaining <= 0f)
        {
            StopCountdown();
            onFinished.Invoke();
            return;
        }

        UpdateText();
    }

    private void UpdateText()
    {
        if (timerText == null) return;
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        timerText.text = $"{label} {minutes:00}:{seconds:00}";
    }
}