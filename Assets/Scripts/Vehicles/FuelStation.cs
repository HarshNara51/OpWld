using UnityEngine;

// Put this on an empty GameObject at a gas station pump. Drive in (or
// stand next to your car), stop, and HOLD the key to refuel - the tank
// fills up gradually. Works for any car with VehicleFuel. No collider
// needed; it checks distance.
public class FuelStation : MonoBehaviour
{
    [SerializeField] private float radius = 5f;

    [Tooltip("Fuel added per second while holding the key")]
    [SerializeField] private float refuelRate = 25f;

    [Tooltip("Car must be slower than this (m/s) to refuel")]
    [SerializeField] private float maxCarSpeed = 1f;

    private VehicleFuel carInRange;
    private bool wasRefuelling;
    private VehicleFuel[] allCars = new VehicleFuel[0];
    private float nextCarSearch;

    private void Update()
    {
        VehicleFuel car = FindCarInRange();
        carInRange = car;
        if (car == null)
        {
            NotePopup.ClearPrompt(this);
            wasRefuelling = false;
            return;
        }

        bool stopped = IsStopped(car);
        bool refuelling = GameKeys.Held(GameAction.Refuel) && car.Fuel01 < 1f && stopped;
        if (refuelling) car.AddFuel(refuelRate * Time.deltaTime);
        wasRefuelling = refuelling;

        // One prompt that always says what's going on
        int percent = Mathf.RoundToInt(car.Fuel01 * 100);
        string text = car.Fuel01 >= 0.999f ? "Tank full!"
                    : !stopped ? "Stop the car to refuel."
                    : refuelling ? $"Refuelling... {percent}%"
                    : $"Hold {NotePopup.Key(GameAction.Refuel)} to refuel ({percent}% in the tank)";
        NotePopup.Prompt(this, "<b>Gas station:</b> " + text);
    }

    private void OnDisable() => NotePopup.ClearPrompt(this);

    private VehicleFuel FindCarInRange()
    {
        if (VehicleInteraction.Current != null)
        {
            VehicleFuel driven = VehicleInteraction.Current.GetComponent<VehicleFuel>();
            return driven != null && InRange(driven.transform) ? driven : null;
        }

        // Refresh the car list now and then, not every frame
        if (Time.time >= nextCarSearch)
        {
            nextCarSearch = Time.time + 2f;
            allCars = FindObjectsByType<VehicleFuel>(FindObjectsSortMode.None);
        }

        VehicleFuel nearest = null;
        float best = float.MaxValue;
        foreach (VehicleFuel f in allCars)
        {
            if (f == null || !f.isActiveAndEnabled) continue;
            float d = FlatDistance(f.transform);
            if (d <= radius && d < best) { best = d; nearest = f; }
        }
        return nearest;
    }

    private bool InRange(Transform t) => FlatDistance(t) <= radius;

    private float FlatDistance(Transform t)
    {
        Vector3 o = t.position - transform.position;
        o.y = 0f;
        return o.magnitude;
    }

    private bool IsStopped(VehicleFuel car)
    {
        Rigidbody rb = car.GetComponent<Rigidbody>();
        return rb == null || rb.linearVelocity.magnitude <= maxCarSpeed;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
