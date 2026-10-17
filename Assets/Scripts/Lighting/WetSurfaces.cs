using System.Collections.Generic;
using UnityEngine;

// Makes everything in the scene look rain-soaked (Railway). Only at runtime, and only in the
// scene that has this component: each URP Lit material is swapped for a wet COPY (darker, much
// shinier), so the real material assets - shared with every other scene - are never changed.
//
// The terrain is shared by all scenes too, so its layers are swapped for wet copies while this
// scene is loaded and the originals are put back when it unloads (or Play mode stops).
// Objects that appear later (traffic cars) are picked up by a slow re-scan.
public class WetSurfaces : MonoBehaviour
{
    [Header("How wet")]
    [Tooltip("Smoothness for most surfaces (walls, props, vehicles)")]
    [Range(0f, 1f)] [SerializeField] private float smoothness = 0.7f;
    [Tooltip("Smoothness for ground-like surfaces: roads, concrete, rails, floors")]
    [Range(0f, 1f)] [SerializeField] private float groundSmoothness = 0.88f;
    [Tooltip("Smoothness for leaves, grass and other alpha-clipped materials")]
    [Range(0f, 1f)] [SerializeField] private float foliageSmoothness = 0.45f;
    [Tooltip("Smoothness for characters (skinned meshes) - clothes and skin only get a damp sheen")]
    [Range(0f, 1f)] [SerializeField] private float characterSmoothness = 0.4f;
    [Tooltip("Colour multiplier for rough surfaces - wet things look darker")]
    [Range(0.3f, 1f)] [SerializeField] private float darken = 0.7f;
    [Tooltip("Material names containing any of these get the ground smoothness")]
    [SerializeField] private string[] groundWords = { "road", "asphalt", "street", "pavement", "sidewalk", "concrete", "floor", "ground", "rail", "track", "tarmac" };

    [Header("Terrain")]
    [SerializeField] private bool wetTerrain = true;
    [Range(0f, 1f)] [SerializeField] private float terrainSmoothness = 0.75f;

    [Tooltip("Seconds between checks for newly spawned objects (traffic). 0 = never")]
    [SerializeField] private float rescanInterval = 3f;

    private readonly Dictionary<Material, Material> wetCopies = new Dictionary<Material, Material>();
    private readonly Dictionary<Material, Material> characterCopies = new Dictionary<Material, Material>();
    private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
    private readonly HashSet<int> seen = new HashSet<int>();
    private TerrainData terrainData;
    private TerrainLayer[] originalLayers;

    // The shared terrain asset while it holds wet layers (the editor save guard below skips it).
    public static TerrainData SwappedTerrain { get; private set; }
    private float rescanTimer;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    private void Start()
    {
        Scan();
        if (wetTerrain) WetTerrain();
        rescanTimer = rescanInterval;
    }

    private void Update()
    {
        if (rescanInterval <= 0f) return;
        rescanTimer -= Time.deltaTime;
        if (rescanTimer > 0f) return;
        rescanTimer = rescanInterval;
        Scan();
    }

    private void OnDestroy()
    {
        // Put back anything that outlives this scene (persistent player, managers...).
        foreach (var pair in originals)
            if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        originals.Clear();

        if (terrainData != null && originalLayers != null)
        {
            TerrainLayer[] wetLayers = terrainData.terrainLayers;
            terrainData.terrainLayers = originalLayers;
            foreach (TerrainLayer l in wetLayers) if (l != null) Destroy(l);
            SwappedTerrain = null;
#if UNITY_EDITOR
            // Back to exactly what's on disk - don't leave the shared terrain flagged as changed.
            UnityEditor.EditorUtility.ClearDirty(terrainData);
#endif
        }

        foreach (Material m in wetCopies.Values) if (m != null) Destroy(m);
        foreach (Material m in characterCopies.Values) if (m != null) Destroy(m);
        wetCopies.Clear();
        characterCopies.Clear();
    }

    private void Scan()
    {
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!seen.Add(r.GetInstanceID())) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is SpriteRenderer) continue;

            Material[] mats = r.sharedMaterials;
            bool character = r is SkinnedMeshRenderer;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material wet = GetWet(mats[i], character);
                if (wet == null) continue;
                if (!changed) originals[r] = (Material[])mats.Clone();
                mats[i] = wet;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    private Material GetWet(Material src, bool character)
    {
        if (src == null) return null;
        Dictionary<Material, Material> copies = character ? characterCopies : wetCopies;
        if (copies.TryGetValue(src, out Material cached)) return cached;
        if (wetCopies.ContainsValue(src) || characterCopies.ContainsValue(src)) return null; // already a wet copy

        Material wet = null;
        string shader = src.shader.name;
        bool lit = shader == "Universal Render Pipeline/Lit" || shader == "Universal Render Pipeline/Complex Lit" || shader == "Universal Render Pipeline/Simple Lit";
        bool transparent = src.HasProperty("_Surface") && src.GetFloat("_Surface") > 0.5f;

        if (lit && !transparent && src.HasProperty(SmoothnessId))
        {
            wet = new Material(src) { name = src.name + " (Wet)" };
            bool foliage = src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f;
            float target = character ? characterSmoothness
                : foliage ? foliageSmoothness
                : IsGround(src.name) ? groundSmoothness : smoothness;

            float oldSmooth = src.GetFloat(SmoothnessId);
            wet.SetFloat(SmoothnessId, Mathf.Max(oldSmooth, target));

            // Rough things darken a lot when wet, shiny things (car paint, metal) barely change.
            if (src.HasProperty(BaseColorId))
            {
                float k = Mathf.Lerp(darken, 1f, oldSmooth);
                if (foliage || character) k = Mathf.Lerp(k, 1f, 0.5f);
                Color c = src.GetColor(BaseColorId);
                wet.SetColor(BaseColorId, new Color(c.r * k, c.g * k, c.b * k, c.a));
            }
            if (wet.HasProperty("_EnvironmentReflections"))
            {
                wet.SetFloat("_EnvironmentReflections", 1f);
                wet.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            if (wet.HasProperty("_SpecularHighlights"))
            {
                wet.SetFloat("_SpecularHighlights", 1f);
                wet.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
        }

        copies[src] = wet; // null = leave this material alone
        return wet;
    }

    private bool IsGround(string materialName)
    {
        string n = materialName.ToLowerInvariant();
        foreach (string w in groundWords)
            if (!string.IsNullOrEmpty(w) && n.Contains(w)) return true;
        return false;
    }

    private void WetTerrain()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null) return;

        terrainData = terrain.terrainData;
        originalLayers = terrainData.terrainLayers;
        var wetLayers = new TerrainLayer[originalLayers.Length];
        for (int i = 0; i < originalLayers.Length; i++)
        {
            TerrainLayer src = originalLayers[i];
            if (src == null) continue;
            TerrainLayer l = Instantiate(src);
            l.name = src.name + " (Wet)";
            if (src.maskMapTexture != null)
            {
                // Smoothness comes from the mask map's alpha, remapped by min/max .w
                Vector4 min = src.maskMapRemapMin, max = src.maskMapRemapMax;
                min.w = Mathf.Max(min.w, terrainSmoothness * 0.75f);
                max.w = Mathf.Max(max.w, terrainSmoothness);
                l.maskMapRemapMin = min;
                l.maskMapRemapMax = max;
            }
            else
            {
                l.smoothness = Mathf.Max(src.smoothness, terrainSmoothness);
            }
            Vector4 d = src.diffuseRemapMax;
            l.diffuseRemapMax = new Vector4(d.x * darken, d.y * darken, d.z * darken, d.w);
            wetLayers[i] = l;
        }
        terrainData.terrainLayers = wetLayers;
        SwappedTerrain = terrainData;
    }
}

#if UNITY_EDITOR
// If "Save Project" is pressed during Play mode in Railway, don't write the shared terrain
// asset while it points at the temporary wet layers (that would break every scene's terrain).
class WetSurfacesSaveGuard : UnityEditor.AssetModificationProcessor
{
    static string[] OnWillSaveAssets(string[] paths)
    {
        if (WetSurfaces.SwappedTerrain == null) return paths;
        string terrainPath = UnityEditor.AssetDatabase.GetAssetPath(WetSurfaces.SwappedTerrain);
        return System.Array.FindAll(paths, p => p != terrainPath);
    }
}
#endif
