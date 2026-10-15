using UnityEngine;

// Switches its lights off unless the scene's SceneTimeOfDay is Night.
// Already on the aimable uplight and the aircraft prefabs. Add it to any
// other lamp too (street lights, signs...) and drag in what to switch off.
// The lamp's model stays visible; only its light/glow goes.
public class NightOnly : MonoBehaviour
{
    [Tooltip("Light components, scripts (e.g. the blinking) to switch off in daytime")]
    [SerializeField] private Behaviour[] components;
    [Tooltip("Glowing parts (e.g. the aircraft light dots) to hide in daytime")]
    [SerializeField] private Renderer[] renderers;

    // Start, not Awake: runs after the scene's SceneTimeOfDay is known, and after
    // the blink script has set itself up, so nothing switches back on afterwards
    private void Start()
    {
        if (SceneTimeOfDay.LightsOn) return;
        if (components != null) foreach (Behaviour b in components) if (b != null) b.enabled = false;
        if (renderers != null) foreach (Renderer r in renderers) if (r != null) r.enabled = false;
    }
}
