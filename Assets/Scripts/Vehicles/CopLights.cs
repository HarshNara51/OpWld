using System.Collections;
using UnityEngine;

// Put this on each cop car. Lights stay off until the player is
// busted, then strobe red / blue / white (plus an optional siren).
//
// Groups: GameObjects switched on/off together (e.g. point lights).
// Light bar: if the car's light bar is its own mesh, its red/blue
// material slots are swapped to glowing versions in sync.
public class CopLights : MonoBehaviour
{
    [Header("Point lights")]
    [SerializeField] private GameObject[] redGroup;
    [SerializeField] private GameObject[] blueGroup;
    [Tooltip("Optional white strobe")]
    [SerializeField] private GameObject[] whiteGroup;

    [Header("Light bar glow (optional)")]
    [Tooltip("The light bar mesh's renderer (e.g. PoliceS_light)")]
    [SerializeField] private Renderer lightBar;
    [Tooltip("Element number of the red part in the light bar's Materials list (-1 = none)")]
    [SerializeField] private int redSlot = -1;
    [Tooltip("Element number of the blue part in the light bar's Materials list (-1 = none)")]
    [SerializeField] private int blueSlot = -1;
    [SerializeField] private Material redGlowMaterial;
    [SerializeField] private Material blueGlowMaterial;

    [Header("Timing / sound")]
    [Tooltip("Seconds per flash step - lower = faster strobe")]
    [SerializeField] private float flashInterval = 0.12f;
    [Tooltip("Optional looping siren - set its Output to the SFX mixer group")]
    [SerializeField] private AudioSource siren;

    private Material[] offMaterials;
    private bool on;

    private void Awake()
    {
        if (lightBar != null) offMaterials = lightBar.sharedMaterials;

        SetGroup(redGroup, false);
        SetGroup(blueGroup, false);
        SetGroup(whiteGroup, false);
    }

    private void OnEnable() => SuspicionManager.Busted += TurnOn;
    private void OnDisable() => SuspicionManager.Busted -= TurnOn;

    // Also callable directly (e.g. from a UnityEvent) for testing
    public void TurnOn()
    {
        if (on) return;
        on = true;
        StartCoroutine(FlashRoutine());
        if (siren != null) siren.Play();
    }

    private IEnumerator FlashRoutine()
    {
        var wait = new WaitForSeconds(flashInterval);
        int step = 0;

        // Pattern: red, white, red, blue, white, blue - repeating
        while (true)
        {
            int phase = step % 6;
            bool red = phase == 0 || phase == 2;
            bool blue = phase == 3 || phase == 5;
            bool white = phase == 1 || phase == 4;

            SetGroup(redGroup, red);
            SetGroup(blueGroup, blue);
            SetGroup(whiteGroup, white);
            SetLightBar(red, blue);

            step++;
            yield return wait;
        }
    }

    private void SetLightBar(bool redOn, bool blueOn)
    {
        if (lightBar == null || offMaterials == null) return;

        Material[] mats = (Material[])offMaterials.Clone();
        if (redOn && redGlowMaterial != null && redSlot >= 0 && redSlot < mats.Length) mats[redSlot] = redGlowMaterial;
        if (blueOn && blueGlowMaterial != null && blueSlot >= 0 && blueSlot < mats.Length) mats[blueSlot] = blueGlowMaterial;
        lightBar.sharedMaterials = mats; // only affects this car's renderer
    }

    private static void SetGroup(GameObject[] group, bool active)
    {
        if (group == null) return;
        foreach (GameObject g in group)
        {
            if (g != null) g.SetActive(active);
        }
    }
}