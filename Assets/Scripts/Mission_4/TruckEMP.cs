using UnityEngine;

// Hold Hold Key while near the truck (in the car) to charge the EMP.
// Charge drains if you let go, drive out of range, or a random
// "connection lost" disruption hits - which also forces you to
// release and re-press the key before it'll resume charging.
public class TruckEMP : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The LEAD truck's MissionTruckFollower - stopping it also stops any follower/trailer automatically")]
    [SerializeField] private MissionTruckFollower truck;
    [SerializeField] private Transform player;
    [SerializeField] private Transform car;

    [Header("Charging")]
    [SerializeField] private KeyCode holdKey = KeyCode.F;
    [SerializeField] private float range = 15f;
    [Tooltip("Percent per second while charging cleanly - keep this low for a long, tense chase")]
    [SerializeField] private float fillRatePercentPerSecond = 3f;
    [Tooltip("Percent per second lost while not charging (key up, out of range, or disrupted)")]
    [SerializeField] private float decayRatePercentPerSecond = 8f;

    [Header("Disruption (\"lost connection\")")]
    [SerializeField] private float disruptionIntervalMin = 4f;
    [SerializeField] private float disruptionIntervalMax = 10f;

    [Tooltip("Revealed once the truck is actually stopped - starts inactive in the scene")]
    [SerializeField] private GameObject callCopsPrompt;

    // 0-1, for a future UI bar
    public float Percent01 { get; private set; }
    public bool IsCharging { get; private set; }
    public bool IsDisrupted { get; private set; }

    private float nextDisruptionTime;
    private bool complete;
    private bool wasCharging;
    private int lastLoggedMilestone = -1; // TEMP DIAGNOSTIC - remove once a real UI bar exists

    private Transform GetActiveTarget()
    {
        if (player != null && player.gameObject.activeInHierarchy) return player;
        return car;
    }

    private void Update()
    {
        if (truck == null || complete) return;

        bool keyHeld = Input.GetKey(holdKey);

        // A disruption sticks until the key is actually released -
        // just continuing to hold it does nothing to clear it.
        if (IsDisrupted && !keyHeld)
        {
            IsDisrupted = false;
        }

        Transform target = GetActiveTarget();
        bool inRange = target != null && Vector3.Distance(target.position, truck.transform.position) <= range;

        IsCharging = keyHeld && inRange && !IsDisrupted;

        // Reschedule fresh every time a charging session actually
        // begins, so the countdown is never already "overdue" from
        // time spent driving over before you ever pressed the key.
        if (IsCharging && !wasCharging)
        {
            ScheduleNextDisruption();
        }
        wasCharging = IsCharging;

        if (IsCharging)
        {
            Percent01 = Mathf.Min(1f, Percent01 + (fillRatePercentPerSecond / 100f) * Time.deltaTime);

            // TEMP DIAGNOSTIC - remove once a real UI bar exists
            int milestone = Mathf.FloorToInt(Percent01 * 10f);
            if (milestone > lastLoggedMilestone)
            {
                lastLoggedMilestone = milestone;
                Debug.Log($"EMP charge: {milestone * 10}%");
            }

            if (Time.time >= nextDisruptionTime)
            {
                IsDisrupted = true;
                IsCharging = false;
                Debug.Log("EMP connection lost - release and hold again.");
                ScheduleNextDisruption();
            }
        }
        else
        {
            Percent01 = Mathf.Max(0f, Percent01 - (decayRatePercentPerSecond / 100f) * Time.deltaTime);
        }

        if (Percent01 >= 1f)
        {
            complete = true;
            truck.ForceStop();
            if (callCopsPrompt != null) callCopsPrompt.SetActive(true);
        }
    }

    private void ScheduleNextDisruption()
    {
        nextDisruptionTime = Time.time + Random.Range(disruptionIntervalMin, disruptionIntervalMax);
    }
}