using System.Collections.Generic;
using UnityEngine;

// Put on the parent of many lamps (e.g. 'St_Lts'). Switches each lamp's
// Light off when it's far from the camera - nobody notices a street light
// going dark 80 m away, but every lit light costs GPU time. The glowing
// bulb material stays on, so lamps still look lit in the distance.
// In daytime scenes (see SceneTimeOfDay) all lamps stay off.
public class StreetLightCuller : MonoBehaviour
{
    [Tooltip("Lights further than this from the camera are switched off")]
    [SerializeField] private float maxDistance = 80f;
    [Tooltip("Seconds between checks (no need to check every frame)")]
    [SerializeField] private float checkInterval = 0.25f;

    private readonly List<Light> lights = new List<Light>();
    private float nextCheck;

    // Lets the graphics presets change it (e.g. shorter on Low)
    public float MaxDistance
    {
        get => maxDistance;
        set { maxDistance = value; nextCheck = 0f; }
    }

    private void OnEnable()
    {
        lights.Clear();
        GetComponentsInChildren(true, lights);
        nextCheck = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + checkInterval;

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 camPos = cam.transform.position;
        float maxSqr = maxDistance * maxDistance;
        // Daytime scenes (SceneTimeOfDay Morning/Day) keep every lamp off.
        bool dark = SceneTimeOfDay.LightsOn;

        foreach (Light l in lights)
        {
            if (l == null) continue;
            bool on = dark && (l.transform.position - camPos).sqrMagnitude <= maxSqr;
            if (l.enabled != on) l.enabled = on;
        }
    }
}
