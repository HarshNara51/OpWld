using System.Collections;
using UnityEngine;
using TMPro;

// The Farm mission's "trucks arrive in..." countdown, synced to the real lead truck.
//
// StartCountdown() (called the moment the body is photographed) gives the player
// Duration Seconds to photograph the clues and hide. It works out how long the truck
// needs to drive to the farm entrance (Arrival Knot on its spline) and holds the truck
// back, so it rolls in exactly as the timer reaches 00:00. The "trucks have arrived"
// moment (Mission3Manager.OnCountdownFinished) is triggered by the truck actually
// passing the entrance - never by the clock alone.
public class TruckArrivalCountdown : MonoBehaviour
{
    [Tooltip("Seconds the player gets (after photographing the body) before the trucks reach the farm entrance")]
    [SerializeField] private float durationSeconds = 120f;

    [Tooltip("The lead truck (SplineVehicle, Auto Start OFF) - this script starts it at the right moment")]
    [SerializeField] private SplineVehicle truck;
    [Tooltip("Knot on the truck's spline where it enters the farm")]
    [SerializeField] private int arrivalKnotIndex = 5;

    [Tooltip("The styled timer plate (shared mission-timer look). Takes priority over Timer Text.")]
    [SerializeField] private MissionTimerDisplay display;
    [SerializeField] private string caption = "TRUCKS ARRIVE IN";
    [Tooltip("Fallback: plain on-screen countdown (TextMeshPro on the mission's Canvas)")]
    [SerializeField] private TMP_Text timerText;

    private float remaining;
    private bool running;
    private bool truckStarted;
    private float arrivalDistance;
    private Coroutine startRoutine;

    public bool Running => running;

    private void Awake()
    {
        remaining = durationSeconds;
        if (timerText != null) timerText.text = "";
        if (display != null) display.Hide();
    }

    public void StartCountdown()
    {
        running = true;
        truckStarted = false;
        remaining = durationSeconds;

        if (truck != null)
        {
            arrivalDistance = truck.DistanceAtKnot(arrivalKnotIndex);
            float drive = truck.EstimateSecondsTo(arrivalDistance);
            // If the drive is longer than the countdown, show the real time instead of ending early
            remaining = Mathf.Max(durationSeconds, drive);
            startRoutine = StartCoroutine(StartTruckAfter(remaining - drive));
        }
        if (display != null) display.Show(caption);
        UpdateText();
    }

    private IEnumerator StartTruckAfter(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        truckStarted = true;
        truck.BeginMoving();
        startRoutine = null;
    }

    // Stops and hides the countdown (e.g. mission failed)
    public void StopCountdown()
    {
        running = false;
        if (startRoutine != null) { StopCoroutine(startRoutine); startRoutine = null; }
        if (timerText != null) timerText.text = "";
        if (display != null) display.Hide();
    }

    private void Update()
    {
        if (!running) return;

        remaining = Mathf.Max(0f, remaining - Time.deltaTime);

        bool arrived = truck != null
            ? truckStarted && truck.DistanceTravelled >= arrivalDistance
            : remaining <= 0f;

        if (arrived)
        {
            StopCountdown();
            Mission3Manager.Instance.OnCountdownFinished();
            return;
        }

        UpdateText();
    }

    private void UpdateText()
    {
        if (display != null) { display.SetSeconds(remaining); return; }
        if (timerText == null) return;
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.CeilToInt(remaining % 60f);
        if (seconds == 60) { minutes++; seconds = 0; }
        timerText.text = $"Trucks arrive in {minutes:00}:{seconds:00}";
    }
}
