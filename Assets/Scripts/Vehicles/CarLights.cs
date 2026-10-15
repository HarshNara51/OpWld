using UnityEngine;

// Headlamps, tail lamps and (traffic only) turn signals on a car. Added by
// Tools > Vehicles > Add Car Lights. The lamps are small glowing unlit
// shapes, not Light components. The only real lights are the two road
// beams per car, and they're only on when it matters (see below).
//
//   Night (SceneTimeOfDay) : headlamps + tail lamps on
//                             (player cars: only while being driven)
//   Road beams (real lights): player car while driven; traffic only when
//                             near the player, so far-away traffic costs nothing
//   Turn signals           : traffic only, day or night, blink while the
//                             car is about to turn / turning (TrafficCar sets them)
public class CarLights : MonoBehaviour
{
    public enum Kind { Player, Traffic }

    [SerializeField] private Kind kind = Kind.Traffic;
    [SerializeField] private Renderer[] headlamps;
    [SerializeField] private Renderer[] tailLamps;
    [SerializeField] private Renderer[] leftSignals;
    [SerializeField] private Renderer[] rightSignals;
    [Tooltip("Real spot lights that light the road")]
    [SerializeField] private Light[] beams;
    [Tooltip("Traffic only: the beams switch on within this distance of the player (or the car they drive)")]
    [SerializeField] private float beamDistance = 70f;

    [Tooltip("Seconds per signal blink (on + off)")]
    [SerializeField] private float signalCycle = 0.7f;

    private VehicleInteraction vehicle;
    private int signal;          // -1 left, 0 off, +1 right
    private bool? lampsShown;    // last state, so renderers are only touched on change
    private bool? leftShown, rightShown;
    private bool? beamsShown;
    private float nextDistanceCheck;
    private bool nearPlayer;
    private static Transform player;

    private void Awake()
    {
        vehicle = GetComponentInParent<VehicleInteraction>();
        SetAll(headlamps, false); SetAll(tailLamps, false);
        SetAll(leftSignals, false); SetAll(rightSignals, false);
        if (beams != null) foreach (Light l in beams) if (l != null) l.enabled = false;
    }

    // -1 = left, 0 = off, +1 = right (TrafficCar calls this)
    public void SetSignal(int direction) => signal = direction;

    private void Update()
    {
        bool night = SceneTimeOfDay.LightsOn;
        bool driven = vehicle != null && vehicle.IsDriving;
        bool lamps = night && (kind == Kind.Traffic || driven);

        if (lampsShown != lamps)
        {
            lampsShown = lamps;
            SetAll(headlamps, lamps);
            SetAll(tailLamps, lamps);
        }

        // Real beams: traffic only near the player (checked a few times a second, not every frame)
        if (kind == Kind.Traffic && lamps && Time.time >= nextDistanceCheck)
        {
            nextDistanceCheck = Time.time + 0.4f + Random.value * 0.2f;
            Transform target = PlayerTarget();
            nearPlayer = target != null && (target.position - transform.position).sqrMagnitude < beamDistance * beamDistance;
        }
        bool beamsOn = lamps && (kind == Kind.Player || nearPlayer);
        if (beamsShown != beamsOn)
        {
            beamsShown = beamsOn;
            if (beams != null) foreach (Light l in beams) if (l != null) l.enabled = beamsOn;
        }

        // Signals: blink on the turning side
        bool blinkOn = Mathf.Repeat(Time.time, signalCycle) < signalCycle * 0.5f;
        bool left = signal < 0 && blinkOn, right = signal > 0 && blinkOn;
        if (leftShown != left) { leftShown = left; SetAll(leftSignals, left); }
        if (rightShown != right) { rightShown = right; SetAll(rightSignals, right); }
    }

    // The player on foot, or the car they're driving
    private static Transform PlayerTarget()
    {
        if (VehicleInteraction.Current != null) return VehicleInteraction.Current.transform;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        return player != null && player.gameObject.activeInHierarchy ? player : null;
    }

    private static void SetAll(Renderer[] list, bool on)
    {
        if (list == null) return;
        foreach (Renderer r in list) if (r != null) r.enabled = on;
    }
}
