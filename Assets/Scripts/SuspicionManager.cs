using System.Collections.Generic;
using UnityEngine;

// Put this on an empty GameObject in any mission scene (scene-local,
// resets fresh each time the mission is attempted).
public class SuspicionManager : MonoBehaviour
{
    public static SuspicionManager Instance { get; private set; }

    [SerializeField] private float maxSuspicion = 100f;
    [Tooltip("Fill speed per second at point-blank range from a source")]
    [SerializeField] private float fillRate = 40f;
    [Tooltip("Decay speed per second once clear of every source")]
    [SerializeField] private float decayRate = 15f;

    [Tooltip("Whether detection runs at all - leave true for missions like Mission 1 where cops are active from the start. Set false here and call SetActive(true) at the right moment for missions like Mission 3, where it shouldn't matter until a specific phase (e.g. the chase, not while hiding near parked trucks).")]
    [SerializeField] private bool startActive = true;

    [Tooltip("Whichever mission manager is in this scene - must implement IFailableMission")]
    [SerializeField] private MonoBehaviour missionManagerSource;

    private readonly List<SuspicionSource> sources = new List<SuspicionSource>();
    private IFailableMission missionManager;
    private Transform player;
    private Transform car;
    private float currentSuspicion;
    private bool busted;
    private bool isActive;

    public float Percent01 => currentSuspicion / maxSuspicion;

    private void Awake()
    {
        Instance = this;
        isActive = startActive;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        GameObject carObj = GameObject.FindGameObjectWithTag("PlayerCar");
        if (carObj != null) car = carObj.transform;

        missionManager = missionManagerSource as IFailableMission;
        if (missionManager == null)
        {
            Debug.LogWarning("SuspicionManager: Mission Manager Source isn't assigned, or doesn't implement IFailableMission.");
        }
    }

    public void SetActive(bool active)
    {
        isActive = active;
    }

    private void Start()
    {
        // Start() runs after every object's Awake() has completed, so this
        // reliably finds every cop/thief/truck already placed in the
        // scene — sidesteps the OnEnable ordering issue entirely.
        sources.AddRange(FindObjectsByType<SuspicionSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
    }

    // Player is disabled while driving (see VehicleInteraction), so this
    // picks whichever one is actually relevant right now: the player on
    // foot, or the car while driving.
    private Transform GetDetectionTarget()
    {
        if (player != null && player.gameObject.activeInHierarchy) return player;
        return car;
    }

    private void Update()
    {
        if (busted || !isActive) return;

        Transform target = GetDetectionTarget();
        if (target == null) return;

        float closestRatio = 0f; // 0 = fully clear, 1 = right on top of a source
        float closestDist = float.MaxValue;

        foreach (var source in sources)
        {
            if (!source.gameObject.activeInHierarchy) continue;

            float dist = Vector3.Distance(target.position, source.transform.position);
            if (dist < closestDist) closestDist = dist;

            if (dist < source.detectRadius)
            {
                float ratio = 1f - (dist / source.detectRadius);
                if (ratio > closestRatio) closestRatio = ratio;
            }
        }

        if (closestRatio > 0f)
        {
            currentSuspicion = Mathf.Min(maxSuspicion, currentSuspicion + fillRate * closestRatio * Time.deltaTime);
        }
        else
        {
            currentSuspicion = Mathf.Max(0f, currentSuspicion - decayRate * Time.deltaTime);
        }

        if (currentSuspicion >= maxSuspicion)
        {
            busted = true;
            missionManager?.FailMission("Spotted");
        }
    }
}