using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Tools > Vehicles > Build Model Lamps (traffic + Blaze)
//
// Most car models paint their lamps into one big texture, so there's no
// separate "light" material for CarLights to switch on. This tool finds the
// lamp triangles of each car's body (the ones under its head/tail lamp dots,
// facing out of the front/back), moves them into two new sub-meshes and gives
// those a "<material>_HeadLight" / "<material>_TailLight" copy of the original
// material. CarLights then lights those up at night instead of the dots.
//
// Nothing imported is changed: the new meshes and materials are saved in
// Assets/Prefabs/ModelLamps and only the prefabs' Mesh Filters / materials
// are pointed at them. Running it again rebuilds from the original meshes.
public static class ModelLampBuilder
{
    private const string Folder = "Assets/Prefabs/ModelLamps";
    private const string BlazePath = "Assets/Prefabs/PlayerCars/Blaze.prefab";

    // Untextured lamp parts glow in these colours (textured ones glow in their texture's colours)
    private static readonly Color HeadGlow = new Color(1f, 0.93f, 0.8f);
    private static readonly Color TailGlow = new Color(1f, 0.04f, 0.02f);

    [MenuItem("Tools/Vehicles/Build Model Lamps (traffic + Blaze)")]
    private static void BuildMenu()
    {
        if (!EditorUtility.DisplayDialog("Build Model Lamps",
                "Makes the lamps of every traffic car and Blaze part of the car model, so they glow instead of the lamp dots.\n\n" +
                "New meshes and materials go into " + Folder + ". The prefabs are re-pointed at them; imported files aren't changed.",
                "Build", "Cancel")) return;
        Debug.Log(Build());
    }

    // Which original mesh each lamp mesh was made from, so a re-run starts
    // from the original again (Assets/Prefabs/ModelLamps/sources.json)
    [System.Serializable] private class SourceEntry { public string lampMesh, sourceGuid, sourceName; }
    [System.Serializable] private class SourceList { public List<SourceEntry> entries = new List<SourceEntry>(); }
    private static SourceList sources;
    private static string SourcesFile => Folder + "/sources.json";

    private class MeshResult
    {
        public Mesh mesh;
        public int headSource = -1, tailSource = -1;   // sub-mesh the lamp triangles came from (for the material)
        public int headSub = -1, tailSub = -1;         // their new sub-mesh index
        public int headTris, tailTris;
    }

    public static string Build()
    {
        Directory.CreateDirectory(Folder + "/Meshes");
        Directory.CreateDirectory(Folder + "/Materials");
        AssetDatabase.Refresh();

        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CarLights cl = go != null ? go.GetComponentInChildren<CarLights>(true) : null;
            if (cl == null) continue;
            bool traffic = new SerializedObject(cl).FindProperty("kind").enumValueIndex == (int)CarLights.Kind.Traffic;
            if (traffic || path == BlazePath) paths.Add(path);
        }

        sources = File.Exists(SourcesFile) ? JsonUtility.FromJson<SourceList>(File.ReadAllText(SourcesFile)) : new SourceList();

        // Model files that aren't Read/Write get it switched on first (needed to
        // read their triangles) - before any prefab is opened for editing
        foreach (string path in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            MeshFilter body = BodyOf(go);
            if (body != null) MakeReadable(OriginalMesh(body.sharedMesh));
        }

        var meshCache = new Dictionary<Mesh, MeshResult>();
        var matCache = new Dictionary<(Material, bool), Material>();
        var report = new StringBuilder("Model lamps:\n");

        foreach (string path in paths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                report.Append(Path.GetFileNameWithoutExtension(path)).Append(": ")
                      .Append(BuildOne(root, path == BlazePath, meshCache, matCache)).Append('\n');
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        sources.entries.RemoveAll(e => !File.Exists(e.lampMesh));
        File.WriteAllText(SourcesFile, JsonUtility.ToJson(sources, true));
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(SourcesFile);
        return report.ToString();
    }

    private static string BuildOne(GameObject root, bool blaze, Dictionary<Mesh, MeshResult> meshCache, Dictionary<(Material, bool), Material> matCache)
    {
        CarLights cl = root.GetComponentInChildren<CarLights>(true);
        var so = new SerializedObject(cl);
        List<Renderer> heads = Renderers(so, "headlamps"), tails = Renderers(so, "tailLamps");

        MeshFilter body = BodyOf(root);
        if (body == null) return "no body mesh";
        MeshRenderer bodyRenderer = body.GetComponent<MeshRenderer>();

        // Strip anything a previous run added, so it always starts from the original
        Mesh source = OriginalMesh(body.sharedMesh);
        Material[] mats = bodyRenderer.sharedMaterials
            .Where(m => m == null || !m.name.EndsWith("_HeadLight") && !m.name.EndsWith("_TailLight"))
            .Take(source.subMeshCount).ToArray(); // one per original sub-mesh

        // Head lamps the model already has its own material for (Jeep, MicroBus, Sedan: "Headlights")
        bool hasHeadMaterial = mats.Any(m => m != null && m.name.ToLowerInvariant().Contains("light"));

        if (!meshCache.TryGetValue(source, out MeshResult result))
        {
            result = SplitLamps(source, body.transform, root.transform, hasHeadMaterial ? null : heads, tails, blaze, mats);
            meshCache[source] = result;
        }
        if (result.headTris == 0 && result.tailTris == 0) return "no lamp triangles found - keeps its dots";

        var newMats = new List<Material>(mats);
        while (newMats.Count < source.subMeshCount) newMats.Add(mats.Length > 0 ? mats[mats.Length - 1] : null);
        if (result.headSub >= 0) newMats.Add(LampMaterial(mats[Mathf.Min(result.headSource, mats.Length - 1)], true, matCache));
        if (result.tailSub >= 0) newMats.Add(LampMaterial(mats[Mathf.Min(result.tailSource, mats.Length - 1)], false, matCache));

        body.sharedMesh = result.mesh;
        bodyRenderer.sharedMaterials = newMats.ToArray();
        return $"{result.headTris} head + {result.tailTris} tail triangles" + (hasHeadMaterial ? " (own Headlights material kept)" : "");
    }

    // The body: the biggest mesh that isn't one of the lamp dots
    private static MeshFilter BodyOf(GameObject root)
    {
        MeshFilter body = null;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.transform.parent != null && mf.transform.parent.name == "CarLights") continue;
            if (body == null || mf.sharedMesh.vertexCount > body.sharedMesh.vertexCount) body = mf;
        }
        return body;
    }

    // A previous run's lamp mesh -> the model's original mesh (followed back
    // through sources.json; lamp meshes all live in the ModelLamps folder)
    private static Mesh OriginalMesh(Mesh mesh)
    {
        for (int guard = 0; mesh != null && sources != null && guard < 10; guard++)
        {
            string path = AssetDatabase.GetAssetPath(mesh);
            if (!path.StartsWith(Folder + "/")) return mesh;
            SourceEntry e = sources.entries.Find(x => x.lampMesh == path);
            if (e == null) return mesh;
            Mesh next = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(e.sourceGuid)))
                if (o is Mesh m && m.name == e.sourceName) next = m;
            if (next == null) return mesh;
            mesh = next;
        }
        return mesh;
    }

    private static List<Renderer> Renderers(SerializedObject so, string field)
    {
        var list = new List<Renderer>();
        SerializedProperty p = so.FindProperty(field);
        for (int i = 0; p != null && i < p.arraySize; i++)
            if (p.GetArrayElementAtIndex(i).objectReferenceValue is Renderer r) list.Add(r);
        return list;
    }

    private static MeshResult SplitLamps(Mesh source, Transform bodyT, Transform root, List<Renderer> heads, List<Renderer> tails, bool blaze, Material[] mats)
    {
        Vector3[] verts = source.vertices;

        // Lamp dots -> boxes in the body mesh's own space, a little bigger than the dot
        List<Bounds> headBoxes = Boxes(heads, bodyT), tailBoxes = Boxes(tails, bodyT);
        Vector3 forward = bodyT.InverseTransformDirection(root.forward).normalized;

        var keep = new List<int>[source.subMeshCount];
        var head = new List<int>();
        var tail = new List<int>();
        var headFrom = new Dictionary<int, int>();
        var tailFrom = new Dictionary<int, int>();

        for (int s = 0; s < source.subMeshCount; s++)
        {
            keep[s] = new List<int>();
            int[] tris = source.GetTriangles(s);
            string matName = s < mats.Length && mats[s] != null ? mats[s].name.ToLowerInvariant() : "";
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                Vector3 centre = (a + b + c) / 3f;
                // Normalised by hand: some models are tiny (imported at 1/100) and
                // Vector3.normalized gives zero for vectors that short
                Vector3 normal = Vector3.Cross(b - a, c - a);
                float length = normal.magnitude;
                if (length > 0f) normal /= length;
                float facing = Vector3.Dot(normal, forward);

                bool isHead, isTail;
                if (blaze)
                {
                    // Blaze's headlamps are the dark glass pockets just above its dots
                    Vector3 rootPos = root.InverseTransformPoint(bodyT.TransformPoint(centre));
                    // (the windshield is the same glass, but higher up and further back)
                    isHead = heads != null && matName.Contains("glass") && facing > -0.2f && rootPos.z > 1.5f && rootPos.y < 1.0f && Mathf.Abs(rootPos.x) > 0.3f;
                    isTail = InAny(tailBoxes, centre) && facing < -0.25f && Small(a, b, c, tailBoxes);
                }
                else
                {
                    isHead = heads != null && InAny(headBoxes, centre) && facing > 0.25f && Small(a, b, c, headBoxes);
                    isTail = InAny(tailBoxes, centre) && facing < -0.25f && Small(a, b, c, tailBoxes);
                }

                List<int> to = isHead ? head : isTail ? tail : keep[s];
                to.Add(tris[t]); to.Add(tris[t + 1]); to.Add(tris[t + 2]);
                if (isHead) headFrom[s] = headFrom.TryGetValue(s, out int n) ? n + 1 : 1;
                else if (isTail) tailFrom[s] = tailFrom.TryGetValue(s, out int n2) ? n2 + 1 : 1;
            }
        }

        var result = new MeshResult { headTris = head.Count / 3, tailTris = tail.Count / 3 };
        if (result.headTris == 0 && result.tailTris == 0) { result.mesh = source; return result; }

        Mesh mesh = Object.Instantiate(source);
        mesh.name = source.name + "_lamps";
        int subs = source.subMeshCount;
        if (head.Count > 0) result.headSub = subs++;
        if (tail.Count > 0) result.tailSub = subs++;
        mesh.subMeshCount = subs;
        for (int s = 0; s < source.subMeshCount; s++) mesh.SetTriangles(keep[s], s);
        if (result.headSub >= 0) mesh.SetTriangles(head, result.headSub);
        if (result.tailSub >= 0) mesh.SetTriangles(tail, result.tailSub);
        result.headSource = headFrom.Count > 0 ? headFrom.OrderByDescending(kv => kv.Value).First().Key : 0;
        result.tailSource = tailFrom.Count > 0 ? tailFrom.OrderByDescending(kv => kv.Value).First().Key : 0;

        string file = $"{Folder}/Meshes/{Safe(source.name)}_lamps_{Short(source)}.asset";
        // Re-runs overwrite the old lamp mesh in place, so its ID (and every
        // prefab's link to it) stays the same
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(file);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = Path.GetFileNameWithoutExtension(file);
            Object.DestroyImmediate(mesh);
            mesh = existing;
            EditorUtility.SetDirty(mesh);
        }
        else AssetDatabase.CreateAsset(mesh, file);
        sources.entries.RemoveAll(x => x.lampMesh == file);
        sources.entries.Add(new SourceEntry { lampMesh = file, sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)), sourceName = source.name });
        result.mesh = mesh;
        return result;
    }

    private static List<Bounds> Boxes(List<Renderer> dots, Transform bodyT)
    {
        var boxes = new List<Bounds>();
        if (dots == null) return boxes;
        foreach (Renderer r in dots)
        {
            Bounds w = r.bounds;
            var local = new Bounds(bodyT.InverseTransformPoint(w.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = w.center + Vector3.Scale(w.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                local.Encapsulate(bodyT.InverseTransformPoint(corner));
            }
            // 50% bigger plus a few cm, so the whole lamp is caught even if the dot isn't spot on
            Vector3 size = local.size;
            float pad = 0.04f / Mathf.Max(0.0001f, bodyT.lossyScale.x);
            local.Expand(size * 0.5f + Vector3.one * pad * 2f);
            boxes.Add(local);
        }
        return boxes;
    }

    // A lamp triangle is no bigger than the lamp - this keeps big body panels
    // that merely reach into the box (e.g. the Stylized pack's rear corners) out
    private static bool Small(Vector3 a, Vector3 b, Vector3 c, List<Bounds> boxes)
    {
        float edge = Mathf.Max((a - b).magnitude, (b - c).magnitude, (c - a).magnitude);
        foreach (Bounds box in boxes)
            if (box.Contains((a + b + c) / 3f)) return edge <= box.size.magnitude * 0.6f;
        return false;
    }

    private static bool InAny(List<Bounds> boxes, Vector3 p)
    {
        foreach (Bounds b in boxes) if (b.Contains(p)) return true;
        return false;
    }

    // One lamp material per (original material, head/tail) - shared by every car using that material
    private static Material LampMaterial(Material original, bool headLamp, Dictionary<(Material, bool), Material> cache)
    {
        if (cache.TryGetValue((original, headLamp), out Material m)) return m;
        string name = $"{original.name}_{(headLamp ? "HeadLight" : "TailLight")}";
        string file = $"{Folder}/Materials/{Safe(name)}.mat";

        m = AssetDatabase.LoadAssetAtPath<Material>(file);
        if (m == null) { m = new Material(original); AssetDatabase.CreateAsset(m, file); }
        else m.CopyPropertiesFromMaterial(original);
        m.name = name;

        // Off by day (CarLights switches the glow on at night). The emission
        // colour is the glow tint: head lamps white (= the texture's own lamp
        // colour), tail lamps always red, whatever the texture has there.
        bool textured = original.mainTexture != null;
        m.DisableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", !headLamp ? TailGlow : textured ? Color.white : HeadGlow);
        if (!textured && !headLamp && m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.55f, 0.04f, 0.03f)); // reads as a tail lamp by day too
        EditorUtility.SetDirty(m);
        cache[(original, headLamp)] = m;
        return m;
    }

    private static void MakeReadable(Mesh mesh)
    {
        if (mesh.isReadable) return;
        if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) is ModelImporter importer && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    private static string Safe(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_').Replace(',', '_').Replace('.', '_');
    }

    private static string Short(Mesh m) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m)).Substring(0, 6);
}
