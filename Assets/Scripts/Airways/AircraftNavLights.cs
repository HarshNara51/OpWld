using UnityEngine;

// Blinking red (left) / green (right) wingtip lights on an aircraft.
// Added by Tools > Aircraft > Add Navigation Lights. The "lights" are
// small glowing unlit balls, not Light components, so they cost almost
// nothing: no lighting, no shadows - this script just switches the two
// renderers on and off.
public class AircraftNavLights : MonoBehaviour
{
    [SerializeField] private Renderer[] lamps;

    [Tooltip("Seconds for one full blink cycle")]
    [SerializeField] private float cycle = 1.4f;
    [Tooltip("How long each flash stays on")]
    [SerializeField] private float flash = 0.09f;
    [Tooltip("Two quick flashes per cycle (like real anti-collision lights), or one")]
    [SerializeField] private bool doubleFlash = true;

    private float offset;
    private bool lit = true;

    private void OnEnable()
    {
        offset = Random.value * cycle; // aircraft don't all blink in sync
        Set(false);
    }

    private void Update()
    {
        float t = Mathf.Repeat(Time.time + offset, cycle);
        bool on = t < flash || (doubleFlash && t > flash * 2.2f && t < flash * 3.2f);
        if (on != lit) Set(on);
    }

    private void Set(bool on)
    {
        lit = on;
        if (lamps == null) return;
        foreach (Renderer r in lamps) if (r != null) r.enabled = on;
    }
}
