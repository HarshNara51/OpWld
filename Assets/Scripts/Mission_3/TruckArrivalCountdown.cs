using UnityEngine;

// Starts counting down once Mission3Manager calls StartCountdown()
// (after all 5 clues are found). Calls Mission3Manager.OnCountdownFinished()
// at zero.
public class TruckArrivalCountdown : MonoBehaviour
{
    [SerializeField] private float durationSeconds = 60f;

    private float remaining;
    private bool running;

    private void Awake()
    {
        remaining = durationSeconds;
    }

    public void StartCountdown()
    {
        running = true;
    }

    private void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            running = false;
            Mission3Manager.Instance.OnCountdownFinished();
        }
    }
}
