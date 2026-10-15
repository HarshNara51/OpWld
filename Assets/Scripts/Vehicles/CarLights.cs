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
    }
    private readonly List<ModelLamp> modelLamps = new List<ModelLamp>();
    private bool modelHasHead, modelHasTail;

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
                    modelLamps.Add(MakeLamp(copy));

                    string n = m.name.ToLowerInvariant();
                    bool tail = n.Contains("tail") || n.Contains("rear") || n.Contains("brake");
                    bool head = n.Contains("head") || n.Contains("front");
                    if (head || !tail) modelHasHead = true;
                    if (tail || !head) modelHasTail = true;
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
        return n.Contains("light") || n.Contains("emission");
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

    private void SetModelLamps(bool on)
    {
        foreach (ModelLamp l in modelLamps)
        {
            if (l.material == null) continue;
            if (on || l.wasEmissive) l.material.EnableKeyword("_EMISSION");
            else l.material.DisableKeyword("_EMISSION");
            if (l.material.HasProperty("_EmissionColor")) l.material.SetColor("_EmissionColor", on ? l.onColor : l.offColor);
            if (l.material.HasProperty("_EmissionMap")) l.material.SetTexture("_EmissionMap", on ? l.onMap : l.offMap);
        }
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
            // The model's own lamps replace the dots at that end of the car
            SetAll(headlamps, lamps && !modelHasHead);
            SetAll(tailLamps, lamps && !modelHasTail);
            SetModelLamps(lamps);
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
