using System.Collections.Generic;
using UnityEngine;

// Put this on a payphone (no collider needed - it checks distance).
// Walk up, press the key, and a tow truck brings your car:
//   Repair phone  -> your most damaged / wrecked car comes back repaired
//   Refuel phone  -> your emptiest car comes back with a full tank
// The truck drives in on its spline, the screen fades to black, and your
// real car is waiting at the Delivery Point when it fades back in.
public class TowPhone : MonoBehaviour
{
    public enum Service { Repair, Refuel }

    [SerializeField] private Service service = Service.Repair;
    [SerializeField] private KeyCode callKey = KeyCode.I;
    [SerializeField] private float radius = 2.5f;

    [Tooltip("This phone's tow truck (with its own spline)")]
    [SerializeField] private TowTruckDriver towTruck;

    [Tooltip("Where the fixed car is parked afterwards (an empty in front of the shop)")]
    [SerializeField] private Transform deliveryPoint;

    [Header("Repair phone only")]
    [Tooltip("Also fill the tank when repairing")]
    [SerializeField] private bool alsoRefuel = true;

    // Cars currently on a tow truck - shared by every phone, so two
    // phones can never send for the same car at once
    private static readonly HashSet<VehicleInteraction> carsInService = new HashSet<VehicleInteraction>();

    private Transform player;
    private bool wasNear;
    private bool busy;

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (busy || Time.timeScale == 0f || player == null) return;

        bool near = player.gameObject.activeInHierarchy &&
                    Vector3.Distance(player.position, transform.position) <= radius;

        if (near && !wasNear)
        {
            Debug.Log(service == Service.Repair
                ? $"Press {callKey} to call the tow truck (repair)"
                : $"Press {callKey} to call the tow truck (fuel)");
        }
        wasNear = near;

        if (near && Input.GetKeyDown(callKey)) Call();
    }

    private void Call()
    {
        // The truck is already out (e.g. called from another pump's phone)
        if (towTruck != null && towTruck.IsBusy)
        {
            NotePopup.Show("<b>Tow service:</b> \"Relax, the truck's already on its way!\"", 3f);
            return;
        }

        VehicleInteraction car = FindCarNeedingService();
        if (car == null)
        {
            NotePopup.Show(service == Service.Repair
                ? "<b>Tow service:</b> \"Your car's already in perfect shape!\""
                : "<b>Tow service:</b> \"Your tank's already full - nothing to deliver!\"", 3f);
            return;
        }

        busy = true;
        carsInService.Add(car);
        NotePopup.Show(service == Service.Repair
            ? "<b>Tow service:</b> \"Yo! We'll haul it to the shop and fix her up. Hang tight!\""
            : "<b>Tow service:</b> \"On our way with your car - we'll fill her up!\"", 4f);

        // Quick blink: the car gets "picked up" (hidden), so it's never in
        // two places at once while the truck brings its look-alike over
        ScreenFader.Instance.FadeOutIn(
            onBlack: () => car.gameObject.SetActive(false),
            onDone: () =>
            {
                if (towTruck != null) towTruck.Drive(car, () => FadeAndDeliver(car));
                else FadeAndDeliver(car); // no truck assigned: just fade and swap
            },
            fadeTime: 0.35f,
            holdTime: 0.3f);
    }

    private void FadeAndDeliver(VehicleInteraction car)
    {
        ScreenFader.Instance.FadeOutIn(
            onBlack: () =>
            {
                Deliver(car);
                if (towTruck != null) towTruck.Hide();
            },
            onDone: () =>
            {
                busy = false;
                carsInService.Remove(car);

                if (service == Service.Repair)
                {
                    NotePopup.Show(alsoRefuel
                        ? "Good as new - and the tank's full! Your car's waiting out front."
                        : "Good as new! Your car's waiting out front.", 3.5f);
                }
                else
                {
                    VehicleHealth h = car.GetComponent<VehicleHealth>();
                    bool stillHurt = h != null && (h.IsWrecked || h.Health01 < 0.999f);
                    NotePopup.Show(stillHurt
                        ? "Tank's full... but she's still banged up. The repair shop can fix that."
                        : "Tank's full. Drive safe!", 3.5f);
                }
            });
    }

    private void Deliver(VehicleInteraction car)
    {
        car.gameObject.SetActive(true); // back from the tow truck
        if (deliveryPoint != null)
        {
            car.transform.SetPositionAndRotation(deliveryPoint.position, deliveryPoint.rotation);
            Rigidbody rb = car.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = deliveryPoint.position;
                rb.rotation = deliveryPoint.rotation;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        VehicleHealth health = car.GetComponent<VehicleHealth>();
        VehicleFuel fuel = car.GetComponent<VehicleFuel>();

        if (service == Service.Repair)
        {
            if (health != null) health.Repair();
            if (alsoRefuel && fuel != null) fuel.FillUp();
        }
        else if (fuel != null)
        {
            fuel.FillUp();
        }
    }

    // Only cars this phone's truck can carry. Repair: most damaged.
    // Refuel: emptiest. Cars that are fine are ignored.
    private VehicleInteraction FindCarNeedingService()
    {
        VehicleInteraction best = null;
        float lowest = 0.999f;

        foreach (VehicleInteraction car in FindObjectsByType<VehicleInteraction>(FindObjectsSortMode.None))
        {
            if (car == VehicleInteraction.Current) continue;
            if (carsInService.Contains(car)) continue;
            if (towTruck != null && !towTruck.CanTow(car)) continue;

            float level;
            if (service == Service.Repair)
            {
                VehicleHealth h = car.GetComponent<VehicleHealth>();
                if (h == null) continue;
                level = h.IsWrecked ? 0f : h.Health01;
            }
            else
            {
                VehicleFuel f = car.GetComponent<VehicleFuel>();
                if (f == null) continue;
                level = f.Fuel01;
            }

            if (level < lowest) { lowest = level; best = car; }
        }
        return best;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}