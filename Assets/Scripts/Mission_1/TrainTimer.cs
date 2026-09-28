using UnityEngine;
using TMPro;

// Put this on an empty GameObject in Mission1_Railway.
public class TrainTimer : MonoBehaviour
{
    public static TrainTimer Instance { get; private set; }

    [Tooltip("Seconds until the train departs")]
    [SerializeField] private float durationSeconds = 180f;

    [Tooltip("Optional - leave empty until the real HUD exists")]
    [SerializeField] private TMP_Text timerText;

    // TEMP DIAGNOSTIC — remove once the timer is confirmed working
    [SerializeField] private float debugLogInterval = 5f;
    private float debugLogTimer;

    private float remaining;
    private bool running;

    private void Awake()
    {
        Instance = this;
        remaining = durationSeconds;
    }

    public void StartTimer()
    {
        running = true;
        Debug.Log($"Train timer started: {durationSeconds:F0}s");
    }

    private void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;

        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(Mathf.Max(0f, remaining) / 60f);
            int seconds = Mathf.FloorToInt(Mathf.Max(0f, remaining) % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        // TEMP DIAGNOSTIC — remove once the timer is confirmed working
        debugLogTimer += Time.deltaTime;
        if (debugLogTimer >= debugLogInterval)
        {
            debugLogTimer = 0f;
            Debug.Log($"[DIAGNOSTIC] Train departs in: {Mathf.Max(0f, remaining):F0}s");
        }

        if (remaining <= 0f)
        {
            running = false;
            Mission1Manager.Instance.FailMission("Train departed");
        }
    }
}
