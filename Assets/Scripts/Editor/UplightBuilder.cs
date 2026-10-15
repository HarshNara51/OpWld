using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Tools > Lighting > Build Aimable Uplight
//
// The imported uplight (Assets/Imports/uplight/model.dae) has its base,
// yoke and lamp head baked into one mesh, so the head can't tilt. This
// cuts the mesh into its separate pieces, then builds a prefab:
//
//   Uplight (Aimable)        <- place / move this; UplightAim on it
//     Swivel                 <- turns left/right: base plate, yoke, axle
//       Head                 <- tilts on the axle; +X = where it points
//         Spot Light
//
// Run it again any time (e.g. after changing Scale) - it rebuilds in place.
public static class UplightBuilder
{
    private const string SourcePath = "Assets/Imports/uplight/model.dae";
    private const string MaterialPath = "Assets/Imports/uplight/uplight.mat";
    private const string OutFolder = "Assets/Imports/uplight/Aimable";
    private const string PrefabPath = "Assets/Imports/uplight/Uplight (Aimable).prefab";
    private const float Scale = 0.3f; // same size as the copies already placed in the Hub

    [MenuItem("Tools/Lighting/Build Aimable Uplight")]
    public static string Build()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (source == null) return "Uplight: model.dae not found.";

        // Every separate piece of the model, in model space
        var pieces = new List<Piece>();
        foreach (MeshFilter mf in source.GetComponentsInChildren<MeshFilter>())
            pieces.AddRange(SplitIntoPieces(mf.sharedMesh, mf.transform.localToWorldMatrix));

        // The head is by far the biggest piece; the axle is the long thin one it turns on
        Piece headPiece = pieces[0], axle = null;
        foreach (Piece p in pieces) if (p.vertexCount > headPiece.vertexCount) headPiece = p;
        foreach (Piece p in pieces)
        {
            if (p == headPiece) continue;
            Vector3 s = p.bounds.size;
            if (s.z > 4f * Mathf.Max(s.x, s.y) && (axle == null || s.z > axle.bounds.size.z)) axle = p;
        }
        Vector3 pivot = axle != null ? axle.bounds.center : headPiece.bounds.center; // on the tilt axis (Z)

        // Ground the lamp: root at the middle of the base, on the floor
        Bounds all = pieces[0].bounds;
        foreach (Piece p in pieces) all.Encapsulate(p.bounds);
        Vector3 floor = new Vector3(pivot.x, all.min.y, pivot.z);

        // Which way the lens faces (largest flat face of the head, on the side the head bulges to)
        Vector3 lens = LensDirection(headPiece, pivot);
        float lensAngle = Mathf.Atan2(lens.y, lens.x) * Mathf.Rad2Deg; // around Z
        // Turn the head so it points straight along +X at tilt 0
        Quaternion level = Quaternion.Euler(0f, 0f, -lensAngle);

        Directory.CreateDirectory(OutFolder);
        var baseParts = new List<Piece>();
        foreach (Piece p in pieces) if (p != headPiece) baseParts.Add(p);
        Mesh baseMesh = BuildMesh(baseParts, v => (v - floor) * Scale, Quaternion.identity, "Uplight_Base");
        Mesh headMesh = BuildMesh(new List<Piece> { headPiece }, v => level * (v - pivot) * Scale, level, "Uplight_Head");
        baseMesh = SaveMesh(baseMesh, OutFolder + "/Uplight_Base.asset");
        headMesh = SaveMesh(headMesh, OutFolder + "/Uplight_Head.asset");

        // How far in front of the axle the lens is (for the light)
        float lensReach = 0f;
        foreach (Vector3 v in headMesh.vertices) lensReach = Mathf.Max(lensReach, v.x);

        // ---- the prefab ----
        var root = new GameObject("Uplight (Aimable)");
        var swivel = new GameObject("Swivel");
        swivel.transform.SetParent(root.transform, false);
        AddMesh(swivel, baseMesh, material);

        var head = new GameObject("Head");
        head.transform.SetParent(swivel.transform, false);
        head.transform.localPosition = (pivot - floor) * Scale;
        AddMesh(head, headMesh, material);

        var lightGo = new GameObject("Spot Light");
        lightGo.transform.SetParent(head.transform, false);
        lightGo.transform.localPosition = new Vector3(lensReach - 0.02f, 0f, 0f);
        lightGo.transform.localRotation = Quaternion.LookRotation(Vector3.right); // light shines along its +Z
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(1f, 0.93f, 0.82f); // warm white
        light.intensity = 50f; // right for ~3 m away; the dinosaur placer sets each lamp's own
        light.range = 18f;
        light.spotAngle = 50f;
        light.innerSpotAngle = 28f;
        light.shadows = LightShadows.None; // many of these around the map - shadows would be expensive

        var aim = root.AddComponent<UplightAim>();
        var so = new SerializedObject(aim);
        so.FindProperty("swivel").objectReferenceValue = swivel.transform;
        so.FindProperty("head").objectReferenceValue = head.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Light only in Night scenes (the lamp itself stays visible)
        var night = root.AddComponent<NightOnly>();
        var nso = new SerializedObject(night);
        SerializedProperty comps = nso.FindProperty("components");
        comps.arraySize = 1;
        comps.GetArrayElementAtIndex(0).objectReferenceValue = light;
        nso.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        string summary = $"Uplight: built '{PrefabPath}' ({pieces.Count} pieces, head = {headPiece.vertexCount} verts, lens was at {lensAngle:F0} deg).";
        Debug.Log(summary);
        return summary;
    }

    // ---------------- placing them ----------------

    private const string GroupName = "Dino Uplights";

    // Tools > Lighting > Place Uplights Around Dinosaurs (open scene)
    // Two lamps per dinosaur (every PhotoCollectible whose ID starts with
    // "dino"), one on each flank, aimed at the body. Replaces the scene's
    // previous "Dino Uplights" group, so it's safe to run again. Undoable.
    [MenuItem("Tools/Lighting/Place Uplights Around Dinosaurs (open scene)")]
    public static string PlaceAroundDinosaurs()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return "Uplight: build the aimable uplight first (Tools > Lighting > Build Aimable Uplight).";

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == GroupName) Undo.DestroyObjectImmediate(r);

        var group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Place uplights");
        var log = new List<string>();

        foreach (PhotoCollectible dino in Object.FindObjectsByType<PhotoCollectible>(FindObjectsSortMode.None))
        {
            if (dino.gameObject.scene != scene || !dino.collectibleId.StartsWith("dino")) continue;
            if (!BodyShape(dino.transform, out Vector3 center, out Vector3 along, out float halfLength, out float halfWidth, out Bounds bounds)) continue;

            var holder = new GameObject("Uplights_" + dino.name);
            holder.transform.SetParent(group.transform, false);
            Vector3 across = Vector3.Cross(Vector3.up, along).normalized;
            float height = bounds.size.y;

            for (int side = -1; side <= 1; side += 2)
            {
                // Out past the flank, a little toward one end so the two beams cross the body
                float gap = Mathf.Clamp(height * 0.35f, 1.5f, 6f);
                Vector3 spot = center + across * side * (halfWidth + gap) + along * side * halfLength * 0.25f;
                spot.y = GroundBelow(spot, bounds.max.y + 10f, dino.transform, bounds.min.y);

                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                lamp.name = side < 0 ? "Uplight L" : "Uplight R";
                lamp.transform.SetParent(holder.transform, true);
                lamp.transform.position = spot;
                // Base squared up with the dino (looks placed by hand, not random)
                lamp.transform.rotation = Quaternion.LookRotation(along, Vector3.up);

                // Beam wide/long enough for this dino
                float distance = Vector3.Distance(spot, center);
                Light light = lamp.GetComponentInChildren<Light>();
                light.spotAngle = Mathf.Clamp(2f * Mathf.Atan2(Mathf.Max(height, halfLength) * 0.6f, distance) * Mathf.Rad2Deg, 45f, 90f);
                light.innerSpotAngle = light.spotAngle * 0.55f;
                light.range = Mathf.Max(18f, distance * 2.5f + height);
                // Light fades with distance squared, so farther lamps need more (about 60 at 3 m looked right)
                light.intensity = Mathf.Clamp(6.5f * distance * distance, 30f, 450f);

                var aim = lamp.GetComponent<UplightAim>();
                aim.target = dino.transform;
                aim.aimHeight = height > 10f ? 0.4f : 0.55f; // tall ones: light the body, not the sky
                aim.Aim();
                PrefabUtility.RecordPrefabInstancePropertyModifications(aim);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            }
            log.Add(dino.name);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        string summary = $"Uplight: placed 2 lamps each for {log.Count} dinosaurs in {scene.name} ({string.Join(", ", log)}). Save the scene to keep them.";
        Debug.Log(summary);
        return summary;
    }

    // The dino's body from its actual mesh: middle, long horizontal axis (head-tail), half length/width
    private static bool BodyShape(Transform dino, out Vector3 center, out Vector3 along, out float halfLength, out float halfWidth, out Bounds bounds)
    {
        var points = new List<Vector3>();
        bounds = default;
        bool any = false;
        foreach (Renderer r in dino.GetComponentsInChildren<Renderer>())
        {
            Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))) continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            Vector3[] v = mesh.vertices;
            int step = Mathf.Max(1, v.Length / 3000);
            for (int i = 0; i < v.Length; i += step) points.Add(r.transform.TransformPoint(v[i]));
        }
        center = bounds.center; along = Vector3.forward; halfLength = halfWidth = 1f;
        if (!any || points.Count < 3) return false;

        // Main horizontal direction of the body (2D principal axis)
        float mx = 0f, mz = 0f;
        foreach (Vector3 p in points) { mx += p.x; mz += p.z; }
        mx /= points.Count; mz /= points.Count;
        float xx = 0f, zz = 0f, xz = 0f;
        foreach (Vector3 p in points) { float dx = p.x - mx, dz = p.z - mz; xx += dx * dx; zz += dz * dz; xz += dx * dz; }
        float angle = 0.5f * Mathf.Atan2(2f * xz, xx - zz);
        along = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        Vector3 across = new Vector3(-along.z, 0f, along.x);

        float minA = float.MaxValue, maxA = float.MinValue, minC = float.MaxValue, maxC = float.MinValue;
        foreach (Vector3 p in points)
        {
            Vector3 d = new Vector3(p.x - mx, 0f, p.z - mz);
            float a = Vector3.Dot(d, along), c = Vector3.Dot(d, across);
            minA = Mathf.Min(minA, a); maxA = Mathf.Max(maxA, a); minC = Mathf.Min(minC, c); maxC = Mathf.Max(maxC, c);
        }
        center = new Vector3(mx, bounds.center.y, mz) + along * (minA + maxA) * 0.5f + across * (minC + maxC) * 0.5f;
        halfLength = (maxA - minA) * 0.5f;
        halfWidth = (maxC - minC) * 0.5f;
        return true;
    }

    // Ground height under a point: the first surface below that isn't the dino itself
    private static float GroundBelow(Vector3 at, float fromHeight, Transform ignore, float fallback)
    {
        var hits = Physics.RaycastAll(new Vector3(at.x, fromHeight, at.z), Vector3.down, fromHeight + 100f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        foreach (RaycastHit h in hits)
            if (!h.transform.IsChildOf(ignore) && h.point.y > best) best = h.point.y;
        if (best > float.MinValue) return best;
        Terrain t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(at) + t.transform.position.y : fallback;
    }

    // ---------------- mesh surgery ----------------

    private class Piece
    {
        public Vector3[] positions, normals;
        public Vector4[] tangents;
        public Vector2[] uvs;
        public int[] triangles;
        public Bounds bounds;
        public int vertexCount => positions.Length;
    }

    // Splits a mesh into its separately-modelled pieces (connected triangles, welding equal positions)
    private static List<Piece> SplitIntoPieces(Mesh mesh, Matrix4x4 toModel)
    {
        Vector3[] v = mesh.vertices, n = mesh.normals;
        Vector4[] t = mesh.tangents;
        Vector2[] uv = mesh.uv;
        int[] tri = mesh.triangles;

        int count = v.Length;
        var parent = new int[count];
        for (int i = 0; i < count; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        var byPos = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < count; i++)
        {
            var key = Vector3Int.RoundToInt(v[i] * 10000f);
            if (byPos.TryGetValue(key, out int j)) Union(i, j); else byPos[key] = i;
        }
        for (int i = 0; i < tri.Length; i += 3) { Union(tri[i], tri[i + 1]); Union(tri[i + 1], tri[i + 2]); }

        var groups = new Dictionary<int, List<int>>(); // root -> triangle starts
        for (int i = 0; i < tri.Length; i += 3)
        {
            int r = Find(tri[i]);
            if (!groups.TryGetValue(r, out var list)) groups[r] = list = new List<int>();
            list.Add(i);
        }

        var pieces = new List<Piece>();
        foreach (List<int> triStarts in groups.Values)
        {
            var remap = new Dictionary<int, int>();
            var p = new List<Vector3>(); var pn = new List<Vector3>(); var pt = new List<Vector4>(); var puv = new List<Vector2>();
            var ptri = new List<int>();
            foreach (int s in triStarts)
                for (int k = 0; k < 3; k++)
                {
                    int old = tri[s + k];
                    if (!remap.TryGetValue(old, out int nw))
                    {
                        nw = p.Count;
                        remap[old] = nw;
                        p.Add(toModel.MultiplyPoint3x4(v[old]));
                        pn.Add(n.Length > 0 ? toModel.MultiplyVector(n[old]).normalized : Vector3.up);
                        if (t.Length > 0)
                        {
                            Vector3 tv = toModel.MultiplyVector(t[old]).normalized;
                            pt.Add(new Vector4(tv.x, tv.y, tv.z, t[old].w));
                        }
                        puv.Add(uv.Length > 0 ? uv[old] : Vector2.zero);
                    }
                    ptri.Add(nw);
                }
            var piece = new Piece
            {
                positions = p.ToArray(), normals = pn.ToArray(), tangents = pt.ToArray(),
                uvs = puv.ToArray(), triangles = ptri.ToArray(),
            };
            piece.bounds = new Bounds(piece.positions[0], Vector3.zero);
            foreach (Vector3 x in piece.positions) piece.bounds.Encapsulate(x);
            pieces.Add(piece);
        }
        return pieces;
    }

    // The lens = the biggest flat face of the head that faces away from the axle
    private static Vector3 LensDirection(Piece head, Vector3 pivot)
    {
        Vector3 bulge = head.bounds.center - pivot;
        bulge.z = 0f;
        var areaByDir = new Dictionary<Vector3Int, (float area, Vector3 sum)>();
        for (int i = 0; i < head.triangles.Length; i += 3)
        {
            Vector3 a = head.positions[head.triangles[i]], b = head.positions[head.triangles[i + 1]], c = head.positions[head.triangles[i + 2]];
            Vector3 cross = Vector3.Cross(b - a, c - a);
            float area = cross.magnitude * 0.5f;
            if (area < 1e-8f) continue;
            Vector3 nrm = cross.normalized;
            if (Mathf.Abs(nrm.z) > 0.3f) continue; // the lamp tilts around Z, so the lens faces somewhere in the X/Y plane
            var key = Vector3Int.RoundToInt(nrm * 12f);
            areaByDir.TryGetValue(key, out var acc);
            areaByDir[key] = (acc.area + area, acc.sum + nrm * area);
        }
        Vector3 best = bulge.sqrMagnitude > 1e-6f ? bulge.normalized : Vector3.right;
        float bestArea = 0f;
        foreach (var kv in areaByDir)
        {
            Vector3 dir = kv.Value.sum.normalized;
            if (Vector3.Dot(dir, bulge) <= 0f) continue;
            if (kv.Value.area > bestArea) { bestArea = kv.Value.area; best = dir; }
        }
        best.z = 0f;
        return best.normalized;
    }

    private static Mesh BuildMesh(List<Piece> parts, System.Func<Vector3, Vector3> move, Quaternion rotate, string name)
    {
        var p = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<Vector4>(); var uv = new List<Vector2>(); var tri = new List<int>();
        bool hasTangents = true;
        foreach (Piece part in parts) if (part.tangents.Length != part.positions.Length) hasTangents = false;
        foreach (Piece part in parts)
        {
            int offset = p.Count;
            foreach (Vector3 x in part.positions) p.Add(move(x));
            foreach (Vector3 x in part.normals) n.Add(rotate * x);
            if (hasTangents) foreach (Vector4 x in part.tangents) { Vector3 r = rotate * (Vector3)x; t.Add(new Vector4(r.x, r.y, r.z, x.w)); }
            uv.AddRange(part.uvs);
            foreach (int i in part.triangles) tri.Add(i + offset);
        }
        var mesh = new Mesh { name = name };
        if (p.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(p); mesh.SetNormals(n); mesh.SetUVs(0, uv);
        if (hasTangents) mesh.SetTangents(t);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        if (!hasTangents) mesh.RecalculateTangents();
        return mesh;
    }

    // Overwrites the existing asset in place, so prefabs/scenes keep their reference on a rebuild
    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        existing.Clear();
        EditorUtility.CopySerialized(mesh, existing);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    private static void AddMesh(GameObject go, Mesh mesh, Material material)
    {
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }
}
