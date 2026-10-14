using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Tools > Photo Hunt
//
// Make Wall Art From Selected Images
//   Select images in the Project window, run it. For each image you get a
//   ready-to-place prefab in Assets/Prefabs/PhotoHunt: the picture on a thin
//   see-through board (looks painted on - transparent parts stay clear, it's
//   lit and shaded like the wall), with a PhotoCollectible already set up.
//   Its ID is also added to the hunt list on the Managers prefab.
//
// Stick Selected To Wall
//   Look at a wall in the Scene view so the spot you want is in the MIDDLE
//   of the view, select the art in the Hierarchy, run it. The art jumps onto
//   the wall there, flat against it and upright. It aims at the wall you SEE
//   (its mesh), not its collider - a building's box collider can bulge out.
public static class PhotoHuntArtTools
{
    private const string Folder = "Assets/Prefabs/PhotoHunt";
    private const string MaterialFolder = Folder + "/Materials";
    private const string ManagersPath = "Assets/Resources/Managers.prefab";
    private const float LongestSide = 2.5f;   // metres - scale it in the scene as you like
    private const float WallGap = 0.01f;      // 1 cm off the wall, so it never flickers into it

    [MenuItem("Tools/Photo Hunt/Make Wall Art From Selected Images")]
    public static string MakeWallArt()
    {
        var made = new List<string>();
        Directory.CreateDirectory(MaterialFolder);

        foreach (Object o in Selection.objects)
        {
            if (!(o is Texture2D)) continue;
            string texPath = AssetDatabase.GetAssetPath(o);
            string file = Path.GetFileNameWithoutExtension(texPath);
            string id = "art_" + Slug(file);
            string displayName = Pretty(file);

            FixImport(texPath);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            Material mat = MakeMaterial(tex, $"{MaterialFolder}/{Slug(file)}.mat");
            string prefabPath = $"{Folder}/WallArt_{Slug(file)}.prefab";
            MakePrefab(tex, mat, id, prefabPath);
            AddToHuntList(id, displayName);
            made.Add(id);
        }

        AssetDatabase.SaveAssets();
        string summary = made.Count == 0
            ? "Photo Hunt: select one or more images in the Project window first."
            : $"Photo Hunt: made {made.Count} wall art prefab(s) in {Folder} and added them to the hunt list: {string.Join(", ", made)}";
        Debug.Log(summary);
        return summary;
    }

    [MenuItem("Tools/Photo Hunt/Make Wall Art From Selected Images", true)]
    private static bool MakeWallArtValidate()
    {
        foreach (Object o in Selection.objects) if (o is Texture2D) return true;
        return false;
    }

    [MenuItem("Tools/Photo Hunt/Stick Selected To Wall")]
    public static string StickToWall()
    {
        GameObject art = Selection.activeGameObject;
        SceneView view = SceneView.lastActiveSceneView;
        if (art == null || view == null || view.camera == null) return "Select the art in the Hierarchy and have a Scene view open.";

        Ray ray = view.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool found = RaycastVisibleMeshes(ray, art.transform, out RaycastHit best, out string wallName);
        if (!found) // e.g. terrain - fall back to colliders
        {
            foreach (RaycastHit hit in Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(art.transform)) continue;
                if (!found || hit.distance < best.distance) { best = hit; found = true; wallName = hit.transform.name; }
            }
        }
        if (!found)
        {
            string msg = "Photo Hunt: nothing to stick to in the middle of the Scene view. Aim at the wall.";
            Debug.LogWarning(msg);
            return msg;
        }

        Undo.RecordObject(art.transform, "Stick to wall");
        // Faces out of the wall; upright on walls, and lined up with the view on floors/ceilings
        Vector3 up = Mathf.Abs(Vector3.Dot(best.normal, Vector3.up)) > 0.9f ? view.camera.transform.up : Vector3.up;
        art.transform.rotation = Quaternion.LookRotation(-best.normal, up);
        art.transform.position = best.point + best.normal * WallGap;
        string done = $"Photo Hunt: stuck {art.name} on {wallName}.";
        Debug.Log(done);
        return done;
    }

    [MenuItem("Tools/Photo Hunt/Stick Selected To Wall", true)]
    private static bool StickToWallValidate() => Selection.activeGameObject != null;

    // ---------------- building blocks ----------------

    // Unity's own mesh picking (what clicking in the Scene view uses) - internal, so reached by name
    private static readonly MethodInfo IntersectRayMesh = typeof(HandleUtility).GetMethod(
        "IntersectRayMesh", BindingFlags.Static | BindingFlags.NonPublic);

    // Nearest visible mesh surface along the ray, ignoring 'skip' and its children
    private static bool RaycastVisibleMeshes(Ray ray, Transform skip, out RaycastHit best, out string hitName)
    {
        best = default;
        hitName = null;
        if (IntersectRayMesh == null) return false;
        bool found = false;
        var args = new object[4];
        foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (mf.sharedMesh == null || mf.transform.IsChildOf(skip)) continue;
            var r = mf.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!r.bounds.IntersectRay(ray, out float boundsDist) || (found && boundsDist > best.distance)) continue;

            args[0] = ray; args[1] = mf.sharedMesh; args[2] = mf.transform.localToWorldMatrix; args[3] = null;
            if (!(bool)IntersectRayMesh.Invoke(null, args)) continue;
            var hit = (RaycastHit)args[3];
            if (!found || hit.distance < best.distance) { best = hit; found = true; hitName = mf.name; } // mesh hits have no collider/transform
        }
        // Comes back unnormalised on scaled objects (x500 on some buildings), and must face us
        if (found)
        {
            best.normal = best.normal.normalized;
            if (Vector3.Dot(best.normal, ray.direction) > 0f) best.normal = -best.normal;
        }
        return found;
    }

    // Plain texture with mipmaps (no shimmering from far away), clamped edges
    private static void FixImport(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    // URP Lit, transparent: lit and shadowed like paint, see-through where the image is
    private static Material MakeMaterial(Texture2D tex, string path)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = mat == null;
        if (isNew) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        mat.SetTexture("_BaseMap", tex);
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.12f); // matte paint, not glossy
        mat.SetFloat("_Metallic", 0f);

        mat.SetFloat("_Surface", 1f); // transparent
        mat.SetFloat("_Blend", 0f);   // alpha
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        // Cut fully-empty pixels out completely (no faint box around the art)
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.08f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cull", (float)CullMode.Back);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetShaderPassEnabled("DepthOnly", false);
        mat.SetShaderPassEnabled("ShadowCaster", false);
        mat.enableInstancing = true;

        if (isNew) AssetDatabase.CreateAsset(mat, path);
        else EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void MakePrefab(Texture2D tex, Material mat, string id, string path)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        var art = new GameObject("Art");
        art.transform.SetParent(root.transform, false);
        art.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var mr = art.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;

        // Right shape, longest side = LongestSide metres. The quad's picture side
        // faces -Z, i.e. back along the root's forward - the root's forward points
        // INTO the wall (that's how Stick To Wall turns it).
        float aspect = (float)tex.width / tex.height;
        art.transform.localScale = aspect >= 1f
            ? new Vector3(LongestSide, LongestSide / aspect, 1f)
            : new Vector3(LongestSide * aspect, LongestSide, 1f);

        var collectible = root.AddComponent<PhotoCollectible>();
        collectible.collectibleId = id;
        collectible.maxDistance = 40f;
        collectible.proximityHint = "";
        collectible.note = "";

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    // Fills an empty slot on the list (or adds one). Skips IDs already there.
    private static void AddToHuntList(string id, string displayName)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ManagersPath);
        try
        {
            var hunt = root.GetComponentInChildren<PhotoCollectionManager>(true);
            if (hunt == null) { Debug.LogWarning("Photo Hunt: no PhotoCollectionManager on the Managers prefab."); return; }

            var so = new SerializedObject(hunt);
            SerializedProperty items = so.FindProperty("items");
            int slot = -1;
            for (int i = 0; i < items.arraySize; i++)
            {
                string existing = items.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
                if (existing == id) return;
                if (slot < 0 && string.IsNullOrEmpty(existing)) slot = i;
            }
            if (slot < 0) { items.arraySize++; slot = items.arraySize - 1; }
            items.GetArrayElementAtIndex(slot).FindPropertyRelative("id").stringValue = id;
            items.GetArrayElementAtIndex(slot).FindPropertyRelative("displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ManagersPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // "Neon Graffiti Speed Racing Logo" -> "neon_graffiti_speed_racing_logo"
    private static string Slug(string s) => Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');

    // "Four-Panel Neon Graffiti_Sticker sheet" -> "Four-Panel Neon Graffiti Sticker Sheet"
    private static string Pretty(string s) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Regex.Replace(s, "[_]+", " ").Trim());
}
