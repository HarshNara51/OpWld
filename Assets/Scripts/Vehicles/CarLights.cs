using System.Collections.Generic;
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
//   Brake lights           : tail lamps are DIM at night and flare up BRIGHT
//                             (bloom) while braking - day or night.
//                             Player: Space, or S while still rolling forward.
//                             Traffic: slowing down, or standing in a queue.
//
// Cars whose model has its own lamp material (name contains "light" or
// "emission", e.g. PCC_Light_Mat, Lights, 7-Light, or the *_HeadLight /
// *_TailLight ones made by Tools > Vehicles > Build Model Lamps) light THAT
// up instead of the glowing dots, and those dots stay hidden. "Head"/"Tail"
// in the name says which end it is; other names count as both.
// Cars without one keep the dots. Turn signals are always dots.
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

    [Header("Brake lights")]
    [Tooltip("Tail lamps at night when not braking (fraction of full glow)")]
    [Range(0.05f, 1f)][SerializeField] private float tailDim = 0.35f;
    [Tooltip("Tail lamps while braking (times full glow - above 1 blooms harder)")]
    [SerializeField] private float brakeBright = 2f;
    [Tooltip("Traffic: slowing faster than this (m/s per second) counts as braking")]
    [SerializeField] private float trafficBrakeDecel = 1f;

    [Header("The model's own lamps")]
    [Tooltip("Light up the car model's own lamp material(s) instead of the dots, if it has any")]
    [SerializeField] private bool useModelLamps = true;
    [Tooltip("How bright the model's lamps glow when on (HDR - above 1 blooms)")]
    [SerializeField] private float modelLampGlow = 4f;

    private struct ModelLamp
    {
        public Material material;   // this car's own copy
        public bool wasEmissive;
        public Color offColor, onColor;
        public Texture offMap, onMap;
        public bool head, tail;     // which end(s) of the car it lights
    }
    private readonly List<ModelLamp> modelLamps = new List<ModelLamp>();
    private bool modelHasHead, modelHasTail;
    private bool modelHasTailOnly;   // a lamp material just for the back (can brake-flash on its own)

    private VehicleInteraction vehicle;
    private TrafficCar trafficCar;   // only set on cars the traffic system is driving
    private float nextTrafficCheck;
    private int signal;          // -1 left, 0 off, +1 right
    private bool? lampsShown;    // last state, so renderers are only touched on change
    private bool? leftShown, rightShown;
    private bool? beamsShown;
    private float nextDistanceCheck;
    private bool nearPlayer;
    private static Transform player;
    private Rigidbody body;
    private bool? brakeShown;
    private float lastSpeed, brakeHold;
    private MaterialPropertyBlock block;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        vehicle = GetComponentInParent<VehicleInteraction>();
        body = GetComponentInParent<Rigidbody>();
        block = new MaterialPropertyBlock();
        SetAll(headlamps, false); SetAll(tailLamps, false);
        SetAll(leftSignals, false); SetAll(rightSignals, false);
        if (beams != null) foreach (Light l in beams) if (l != null) l.enabled = false;
        if (useModelLamps) FindModelLamps();
    }

    private void OnDestroy()
    {
        foreach (ModelLamp l in modelLamps) if (l.material != null) Destroy(l.material);
    }

    // Gives this car its own copy of each lamp material, so lighting it up
    // doesn't light every other car that shares the material
    private void FindModelLamps()
    {
        // The car itself: its VehicleInteraction, or (traffic) the object above the "CarLights" holder
        Transform root = vehicle != null ? vehicle.transform
                       : name == "CarLights" && transform.parent != null ? transform.parent : transform;
        var dots = new HashSet<Renderer>();
        foreach (Renderer[] list in new[] { headlamps, tailLamps, leftSignals, rightSignals })
            if (list != null) foreach (Renderer r in list) if (r != null) dots.Add(r);

        var copies = new Dictionary<Material, Material>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (dots.Contains(r)) continue;
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || !IsLampMaterial(m.name)) continue;
                if (!copies.TryGetValue(m, out Material copy))
                {
                    copy = new Material(m) { name = m.name + " (lamp)" };
                    copies[m] = copy;

                    string n = m.name.ToLowerInvariant();
                    bool tail = n.Contains("tail") || n.Contains("rear") || n.Contains("brake") || n.Contains("stop");
                    bool head = n.Contains("head") || n.Contains("front");
                    ModelLamp lamp = MakeLamp(copy);
                    lamp.head = head || !tail;
                    lamp.tail = tail || !head;
                    modelLamps.Add(lamp);
                    if (lamp.head) modelHasHead = true;
                    if (lamp.tail) modelHasTail = true;
                    if (lamp.tail && !lamp.head) modelHasTailOnly = true;
                }
                mats[i] = copy;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    private static bool IsLampMaterial(string name)
    {
        string n = name.ToLowerInvariant();
        // "stop" = brake lamp lenses named like StopSignal_Mat (High Matters sedans, taxi)
        return n.Contains("light") || n.Contains("emission") || n.Contains("stop");
    }

    private ModelLamp MakeLamp(Material m)
    {
        var lamp = new ModelLamp { material = m, wasEmissive = m.IsKeywordEnabled("_EMISSION") };
        lamp.offColor = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
        lamp.offMap = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
        Texture baseMap = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;

        // The emission colour is the glow tint (lamps that already glow keep their
        // colours, just brighter; Build Model Lamps stores white / warm white / red).
        // Plain ones glow in their base colour (or texture, so red tail lamps stay red).
        Color tint;
        if (lamp.offColor.maxColorComponent > 0.01f)
            tint = lamp.offColor / lamp.offColor.maxColorComponent;
        else
            tint = baseMap != null ? Color.white : (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white);
        lamp.onColor = new Color(tint.r, tint.g, tint.b) * modelLampGlow;
        lamp.onMap = lamp.offMap != null ? lamp.offMap : baseMap;
        return lamp;
    }

    // Head / both-ends lamps: on or off. Back-only lamps: off, dim (night) or bright (braking).
    private void SetModelLamps(bool on, bool braking)
    {
        foreach (ModelLamp l in modelLamps)
        {
            if (l.material == null) continue;
            bool backOnly = l.tail && !l.head;
            float level = backOnly ? (braking ? brakeBright : on ? tailDim : 0f) : (on ? 1f : 0f);
            bool lit = level > 0f;
            if (lit || l.wasEmissive) l.material.EnableKeyword("_EMISSION");
            else l.material.DisableKeyword("_EMISSION");
            if (l.material.HasProperty("_EmissionColor")) l.material.SetColor("_EmissionColor", lit ? l.onColor * level : l.offColor);
            if (l.material.HasProperty("_EmissionMap")) l.material.SetTexture("_EmissionMap", lit ? l.onMap : l.offMap);
        }
    }

    // Player: handbrake, or "reverse" while still rolling forward (= braking).
    // Traffic: slowing down, or standing still (holding the brakes in a queue).
    private bool IsBraking(bool driven)
    {
        if (kind == Kind.Player)
        {
            if (!driven) return false;
            float forward = body != null ? Vector3.Dot(body.linearVelocity, body.transform.forward) : 0f;
            return GameKeys.Held(GameAction.Handbrake) || (GameKeys.Held(GameAction.Reverse) && forward > 0.5f);
        }
        if (trafficCar == null || Time.deltaTime <= 0f) return false;
        float speed = trafficCar.Speed;
        float decel = (lastSpeed - speed) / Time.deltaTime;
        lastSpeed = speed;
        if (decel > trafficBrakeDecel || speed < 0.3f) brakeHold = 0.35f; // short hold so they don't flicker
        else brakeHold -= Time.deltaTime;
        return brakeHold > 0f;
    }

    // Tail dots: dim normally, bright while braking (per-car colour, the shared material stays as is)
    private void SetTailDots(bool visible, float level)
    {
        if (tailLamps == null) return;
        foreach (Renderer r in tailLamps)
        {
            if (r == null) continue;
            r.enabled = visible;
            if (!visible) continue;
            Color c = r.sharedMaterial != null ? r.sharedMaterial.GetColor(BaseColorId) : Color.red;
            block.SetColor(BaseColorId, c * level);
            r.SetPropertyBlock(block);
        }
    }

    // -1 = left, 0 = off, +1 = right (TrafficCar calls this)
    public void SetSignal(int direction) => signal = direction;

    private void Update()
    {
        bool night = SceneTimeOfDay.LightsOn;
        bool driven = vehicle != null && vehicle.IsDriving;
        // Traffic models also stand around as parked props - those stay dark.
        // TrafficCar is added right after spawning, so look for it a few times
        if (kind == Kind.Traffic && trafficCar == null && Time.time >= nextTrafficCheck)
        {
            nextTrafficCheck = Time.time + 1f;
            trafficCar = GetComponentInParent<TrafficCar>();
        }
        bool moving = kind == Kind.Traffic ? trafficCar != null : driven;
        bool lamps = night && moving;

        bool braking = IsBraking(driven);
        if (lampsShown != lamps || brakeShown != braking)
        {
            lampsShown = lamps;
            brakeShown = braking;
            // The model's own lamps replace the dots at that end of the car
            SetAll(headlamps, lamps && !modelHasHead);
            SetModelLamps(lamps, braking);
            // Tail dots: shown when the model has no back lamps of its own - and, while
            // braking, also when its one lamp material covers both ends (so the
            // headlights stay as they are while the brakes flare up)
            bool dotsAtNight = lamps && !modelHasTail;
            bool dotsBraking = braking && !modelHasTailOnly;
            SetTailDots(dotsAtNight || dotsBraking, braking ? brakeBright : tailDim);
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
