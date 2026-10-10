using UnityEngine;

// Put this on an empty GameObject at a gas station pump. Drive in (or
// stand next to your car), stop, and HOLD the key to refuel - the tank
// fills up gradually. Works for any car with VehicleFuel. No collider
// needed; it checks distance.
public class FuelStation : MonoBehaviour
{
    [SerializeField] private KeyCode refuelKey = KeyCode.F;
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

        if (car != carInRange)
        {
            carInRange = car;
            if (car != null)
                Debug.Log($"Gas station: hold {refuelKey} to refuel ({Mathf.RoundToInt(car.Fuel01 * 100)}% in the tank).");
        }

        bool refuelling = car != null && Input.GetKey(refuelKey) && car.Fuel01 < 1f && IsStopped(car);

        if (car != null && Input.GetKeyDown(refuelKey) && !IsStopped(car))
            Debug.Log("Gas station: stop the car first.");

        if (refuelling)
        {
            car.AddFuel(refuelRate * Time.deltaTime);
            if (car.Fuel01 >= 1f) Debug.Log("Gas station: tank full!");
        }
        else if (wasRefuelling && car != null && car.Fuel01 < 1f)
        {
            Debug.Log($"Gas station: stopped at {Mathf.RoundToInt(car.Fuel01 * 100)}%.");
        }

        wasRefuelling = refuelling;
    }

    // The car being driven, or - if on foot - the nearest car with fuel in range
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
