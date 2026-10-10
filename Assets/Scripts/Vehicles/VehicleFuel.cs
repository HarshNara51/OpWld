using System;
using UnityEngine;

// Put this on the ROOT of every drivable car (next to VehicleInteraction).
// Fuel burns while the car is being driven, based on distance travelled
// (flooring it burns a bit more). At 0 the engine cuts out: the car
// coasts to a stop and won't drive until it's refuelled at a FuelStation.
public class VehicleFuel : MonoBehaviour
{
    [SerializeField] private float tankSize = 100f;

    [Tooltip("Fuel used per kilometre at normal speed")]
    [SerializeField] private float fuelPerKm = 5f;

    [Tooltip("Extra burn when driving fast (1 = none, 1.5 = 50% more at top speed)")]
    [SerializeField] private float highSpeedMultiplier = 1.5f;

    [Tooltip("Speed (km/h) considered 'top speed' for the multiplier above")]
    [SerializeField] private float topSpeedKmh = 120f;

    [Header("Mission cars")]
    [Tooltip("Tick for cars a mission depends on (taxi, bomb car) - they never run dry")]
    [SerializeField] private bool infiniteFuel = false;

    [Header("References (auto-found if empty)")]
    [SerializeField] private MonoBehaviour carController; // Prometeo's script

    public float Current { get; private set; }
    public float TankSize => tankSize;
    public float Fuel01 => tankSize > 0f ? Current / tankSize : 0f;
    public bool IsEmpty => Current <= 0f;

    // For the speedometer HUD later
    public event Action<float> FuelChanged;

    private VehicleInteraction interaction;
    private Rigidbody rb;
    private bool warnedLow;
    private bool warnedCritical;

    private void Awake()
    {
        interaction = GetComponent<VehicleInteraction>();
        rb = GetComponent<Rigidbody>();
        if (carController == null) carController = GetComponent("PrometeoCarController") as MonoBehaviour;
        Current = tankSize;
    }

    private void Update()
    {
        bool beingDriven = interaction != null && VehicleInteraction.Current == interaction;
        if (!beingDriven) return;

        // Empty tank: keep the engine off even after getting back in
        if (IsEmpty)
        {
            if (carController != null && carController.enabled) carController.enabled = false;
            return;
        }

        if (infiniteFuel || rb == null) return;

        float speed = rb.linearVelocity.magnitude;               // m/s
        float metres = speed * Time.deltaTime;
        float speedFactor = Mathf.Lerp(1f, highSpeedMultiplier, Mathf.Clamp01(speed * 3.6f / topSpeedKmh));
        float used = metres / 1000f * fuelPerKm * speedFactor;

        if (used > 0f) SetFuel(Current - used);
    }

    // Called by FuelStation (or a jerry can, etc.)
    public void AddFuel(float amount)
    {
        if (amount <= 0f) return;
        bool wasEmpty = IsEmpty;
        SetFuel(Current + amount);

        if (wasEmpty && !IsEmpty)
        {
            Debug.Log($"{name}: engine restarted.");
            // Back in business if someone's driving it
            if (interaction != null && VehicleInteraction.Current == interaction && carController != null)
                carController.enabled = true;
        }
    }

    public void FillUp() => AddFuel(tankSize - Current);

    private void SetFuel(float value)
    {
        float before = Current;
        Current = Mathf.Clamp(value, 0f, tankSize);
        if (Mathf.Approximately(before, Current)) return;

        FuelChanged?.Invoke(Fuel01);
        LogWarnings(before);
    }

    private void LogWarnings(float before)
    {
        float f = Fuel01;

        // Re-arm the warnings after refuelling
        if (f > 0.25f) { warnedLow = false; warnedCritical = false; }

        if (f <= 0.25f && !warnedLow)
        {
            warnedLow = true;
            Debug.Log($"{name}: fuel low ({Mathf.RoundToInt(f * 100)}%) - find a gas station.");
        }

        if (f <= 0.1f && !warnedCritical)
        {
            warnedCritical = true;
            Debug.Log($"{name}: fuel almost empty ({Mathf.RoundToInt(f * 100)}%)!");
        }

        if (before > 0f && Current <= 0f)
        {
            Debug.Log($"{name}: out of fuel! The engine cut out.");
            if (carController != null) carController.enabled = false; // coasts to a stop
        }
    }
}
