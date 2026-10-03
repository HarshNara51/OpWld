using UnityEngine;
using TMPro;

// Starts counting down once Mission3Manager calls StartCountdown()
// (the moment the body is photographed). Calls
// Mission3Manager.OnCountdownFinished() at zero.
public class TruckArrivalCountdown : MonoBehaviour
{
    [SerializeField] private float durationSeconds = 60f;

    [Tooltip("Optional on-screen countdown (TextMeshPro on the mission's Canvas)")]
    [SerializeField] private TMP_Text timerText;

    private float remaining;
    private bool running;

    private void Awake()
    {
        remaining = durationSeconds;
        if (timerText != null) timerText.text = "";
    }

    public void StartCountdown()
    {
        remaining = durationSeconds;
        running = true;
        UpdateText();
    }

    // Stops and hides the countdown (e.g. mission failed)
    public void StopCountdown()
    {
        running = false;
        if (timerText != null) timerText.text = "";
    }

    private void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            remaining = 0f;
            StopCountdown();
            Mission3Manager.Instance.OnCountdownFinished();
            return;
        }

        UpdateText();
    }

    private void UpdateText()
    {
        if (timerText == null) return;
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        timerText.text = $"Trucks arrive in {minutes:00}:{seconds:00}";
    }
}