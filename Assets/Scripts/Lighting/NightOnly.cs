using System.Collections.Generic;
using UnityEngine;

// Switches its lights off unless the scene is dark (SceneTimeOfDay.LightsOn).
// Already on the aimable uplight, street light, tube/ceiling light and aircraft
// prefabs. Add it to any other lamp too and drag in what to switch off.
// The lamp's model stays visible; only its light/glow goes.
public class NightOnly : MonoBehaviour
{
    [Tooltip("Light components, scripts (e.g. the blinking) to switch off in daytime")]
    [SerializeField] private Behaviour[] components;
    [Tooltip("Glowing parts (e.g. the aircraft light dots) to hide in daytime")]
    [SerializeField] private Renderer[] renderers;
    [Tooltip("Renderers that stay visible but whose glowing (emissive) materials go dark in daytime, e.g. a street light's bulb")]
    [SerializeField] private Renderer[] emissionOff;

    // One non-glowing copy per emissive material, shared by every lamp (keeps batching intact).
    private static readonly Dictionary<Material, Material> darkCopies = new Dictionary<Material, Material>();

    // Start, not Awake: runs after the scene's SceneTimeOfDay is known, and after
    // the blink script has set itself up, so nothing switches back on afterwards
    private void Start()
    {
        if (SceneTimeOfDay.LightsOn) return;
        if (components != null) foreach (Behaviour b in components) if (b != null) b.enabled = false;
        if (renderers != null) foreach (Renderer r in renderers) if (r != null) r.enabled = false;
        if (emissionOff != null) foreach (Renderer r in emissionOff) if (r != null) DarkenEmission(r);
    }

    private static void DarkenEmission(Renderer r)
    {
        Material[] mats = r.sharedMaterials;
        bool changed = false;
        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null || !m.IsKeywordEnabled("_EMISSION")) continue;
            if (!darkCopies.TryGetValue(m, out Material dark))
            {
                dark = new Material(m) { name = m.name + " (Off)" };
                dark.DisableKeyword("_EMISSION");
                if (dark.HasProperty("_EmissionColor")) dark.SetColor("_EmissionColor", Color.black);
                darkCopies[m] = dark;
            }
            mats[i] = dark;
            changed = true;
        }
        if (changed) r.sharedMaterials = mats;
    }
}
