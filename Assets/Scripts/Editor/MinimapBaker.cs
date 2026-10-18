using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Splines;

// Tools > Minimap > Bake Map Image (open scene)
// Tools > Minimap > Bake Map Images (all 6 scenes)
//
// Takes one straight-down picture of the whole world for the Minimap and
// saves it to Assets/Resources/Minimap/<scene name>.png - the Minimap loads
// the one for the scene you're in (Hub's if a scene has none). All scenes
// share the terrain and roads, but the mission buildings (hotel, stores...)
// only exist in their own scene, hence one picture per scene.
//
// The picture is toned down (darker, less colour) so blips and the player
// arrow stand out, and the roads are painted on top from the road splines,
// clean and light, the way GTA's radar draws them. Players, cars, traffic,
// planes, mission markers and terrain trees/grass are left out.
//
// Re-run after moving roads or big buildings.
public static class MinimapBaker
{
    private const string OutFolder = "Assets/Resources/Minimap";
    private static readonly string[] Scenes =
    {
        "Assets/Scenes/Hub.unity", "Assets/Scenes/Mission_Farm.unity", "Assets/Scenes/Mission_GasStation.unity",
        "Assets/Scenes/Mission_Hotel.unity", "Assets/Scenes/Mission_Railway.unity", "Assets/Scenes/Mission_ShoppingComplex.unity",
    };
    private const string ManagersPath = "Assets/Resources/Managers.prefab";

    // Area covered (world X/Z). The terrain is 0..1000; a margin shows the
    // water and the bridge ends around it.
    private static readonly Vector2 Min = new Vector2(-100f, -100f);
    private const float Size = 1200f;
    private const int Resolution = 4096;

    private const float RoadHalfWidth = 5f;   // real asphalt half-width (not SplineExtrude Radius)
    private const float RoadOutline = 1.2f;   // dark kerb line around the road (m)
    private static readonly Color RoadColor = new Color(0.74f, 0.75f, 0.78f);
    private static readonly Color KerbColor = new Color(0.10f, 0.10f, 0.11f);

    // Root objects not to photograph
    private static readonly string[] Hide =
    {
        "Player", "Plr_Crs", "Prometheus", "Free Racing Car", "BonusTaxi", "Traffic", "AirTraffic",
        "Dummies", "Missions", "TowTruck_Repair", "TowTruck_Gas", "ShoppingLot_Ambience", "Bounds",
    };

    [MenuItem("Tools/Minimap/Bake Map Image (open scene)")]
    private static void BakeOpenScene()
    {
        if (!Bake()) EditorUtility.DisplayDialog("Bake Map Image", "No 'Roads' object in the open scene - open one of the game scenes and try again.", "OK");
    }

    [MenuItem("Tools/Minimap/Bake Map Images (all 6 scenes)")]
    private static void BakeAllScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string back = SceneManager.GetActiveScene().path;
        foreach (string scene in Scenes)
        {
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            Bake();
        }
        if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
    }

    private static bool Bake()
    {
        GameObject roads = GameObject.Find("Roads");
        if (roads == null) return false;
        string outPath = OutFolder + "/" + SceneManager.GetActiveScene().name + ".png";

        Texture2D photo = TakePhoto();
        if (photo == null) return false;

        try
        {
            EditorUtility.DisplayProgressBar("Bake Map Image", "Painting roads...", 0.6f);
            Color[] px = photo.GetPixels();
            Stylize(px);
            PaintRoads(px, roads);

            var result = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);
            result.SetPixels(px);
            result.Apply();

            Directory.CreateDirectory(OutFolder);
            File.WriteAllBytes(outPath, result.EncodeToPNG());
            Object.DestroyImmediate(result);
        }
        finally
        {
            Object.DestroyImmediate(photo);
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true; // smooth roads when zoomed out (driving, the big map)
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = Resolution;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();

        HookUp();
        Debug.Log($"Minimap: baked {outPath} ({Resolution}px, {Size / Resolution:0.00} m per pixel).");
        return true;
    }

    // ---------- the photo ----------

    private static Texture2D TakePhoto()
    {
        var hidden = new List<GameObject>();
        foreach (GameObject g in SceneManager.GetActiveScene().GetRootGameObjects())
            if (g.activeSelf && System.Array.IndexOf(Hide, g.name) >= 0) { g.SetActive(false); hidden.Add(g); }

        var terrains = new List<(Terrain t, bool trees)>();
        foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            terrains.Add((t, t.drawTreesAndFoliage));
            t.drawTreesAndFoliage = false;
        }

        // From 400 m up everything is "tiny": keep buildings on their good LODs
        float lodBias = QualitySettings.lodBias;
        QualitySettings.lodBias = 20f;
        LODGroup[] lods = Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
        foreach (LODGroup l in lods) l.ForceLOD(0);

        // Same plain daylight in every scene (the Hotel is at night, Railway in the rain),
        // sun nearly straight down so buildings don't throw long shadows over the roads
        Light sun = RenderSettings.sun;
        Quaternion sunRot = sun != null ? sun.transform.rotation : Quaternion.identity;
        float sunIntensity = sun != null ? sun.intensity : 0f;
        Color sunColor = sun != null ? sun.color : Color.white;
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(80f, -30f, 0f);
            sun.intensity = 1.6f;
            sun.color = new Color(1f, 0.97f, 0.92f);
        }
        var lamps = new List<Light>();
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l != sun && l.enabled) { l.enabled = false; lamps.Add(l); }
        bool fog = RenderSettings.fog;
        AmbientMode ambientMode = RenderSettings.ambientMode;
        Color ambient = RenderSettings.ambientLight;
        float ambientIntensity = RenderSettings.ambientIntensity;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.6f);

        var camGo = new GameObject("MinimapBakeCamera") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        Texture2D photo = null;
        try
        {
            EditorUtility.DisplayProgressBar("Bake Map Image", "Taking the picture...", 0.2f);
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Size * 0.5f;
            cam.aspect = 1f;
            cam.useOcclusionCulling = false; // the baked occlusion data hides half the town from up here
            cam.transform.SetPositionAndRotation(new Vector3(Min.x + Size * 0.5f, 400f, Min.y + Size * 0.5f), Quaternion.Euler(90f, 0f, 0f));
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 800f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.17f, 0.24f);
            cam.targetTexture = rt;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = true;

            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            photo = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, false);
            photo.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
            photo.Apply();
            RenderTexture.active = prev;
        }
        finally
        {
            Object.DestroyImmediate(camGo);
            rt.Release();
            Object.DestroyImmediate(rt);
            foreach (GameObject g in hidden) g.SetActive(true);
            foreach (var (t, trees) in terrains) t.drawTreesAndFoliage = trees;
            QualitySettings.lodBias = lodBias;
            foreach (LODGroup l in lods) if (l != null) l.ForceLOD(-1);
            if (sun != null) { sun.transform.rotation = sunRot; sun.intensity = sunIntensity; sun.color = sunColor; }
            foreach (Light l in lamps) if (l != null) l.enabled = true;
            RenderSettings.fog = fog;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientIntensity = ambientIntensity;
        }
        return photo;
    }

    // Darker, calmer colours so the HUD on top reads well
    private static void Stylize(Color[] px)
    {
        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            c = Color.Lerp(new Color(lum, lum, lum), c, 0.5f);       // half the saturation
            c = new Color(Mathf.Pow(c.r, 1.1f), Mathf.Pow(c.g, 1.1f), Mathf.Pow(c.b, 1.1f)) * 0.62f;
            c += new Color(0.015f, 0.025f, 0.04f);                    // a cool lift in the shadows
            c.a = 1f;
            px[i] = c;
        }
    }

    // ---------- roads from the splines ----------

    private static void PaintRoads(Color[] px, GameObject roads)
    {
        float ppm = Resolution / Size; // pixels per metre
        var kerb = new float[px.Length];
        var road = new float[px.Length];

        foreach (SplineContainer c in roads.GetComponentsInChildren<SplineContainer>())
            for (int s = 0; s < c.Splines.Count; s++)
            {
                float len = c.CalculateLength(s);
                int n = Mathf.Max(2, Mathf.CeilToInt(len / 0.75f));
                for (int i = 0; i <= n; i++)
                {
                    Vector3 p = c.EvaluatePosition(s, i / (float)n);
                    float cx = (p.x - Min.x) * ppm, cy = (p.z - Min.y) * ppm;
                    Stamp(kerb, cx, cy, (RoadHalfWidth + RoadOutline) * ppm);
                    Stamp(road, cx, cy, RoadHalfWidth * ppm);
                }
            }

        for (int i = 0; i < px.Length; i++)
        {
            if (kerb[i] <= 0f) continue;
            Color c = Color.Lerp(px[i], KerbColor, kerb[i] * 0.85f);
            px[i] = Color.Lerp(c, RoadColor, road[i]);
        }
    }

    // Anti-aliased filled disc, max-blended into the mask
    private static void Stamp(float[] mask, float cx, float cy, float r)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1)), x1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt(cx + r + 1));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1)), y1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt(cy + r + 1));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float v = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                int i = y * Resolution + x;
                if (v > mask[i]) mask[i] = v;
            }
    }

    // ---------- Managers prefab ----------

    // Makes sure the Managers prefab has a Minimap with the right area + font
    private static void HookUp()
    {
        GameObject prefab = PrefabUtility.LoadPrefabContents(ManagersPath);
        try
        {
            Minimap map = prefab.GetComponent<Minimap>();
            if (map == null) map = prefab.AddComponent<Minimap>();
            map.SetArea(Min, new Vector2(Size, Size));
            var so = new SerializedObject(map);
            SerializedProperty font = so.FindProperty("font");
            if (font.objectReferenceValue == null)
            {
                font.objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Imports/Fonts/SDF/Sniglet SDF.asset");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(prefab, ManagersPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }
}
