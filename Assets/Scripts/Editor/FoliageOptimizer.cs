using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

// Editor-only tool. Fixes the foliage performance problems in this project:
//
//  1. Tree prefabs exported from Blender were made of hundreds of separate objects
//     (every leaf cluster was its own MeshRenderer = its own draw call, x4 for shadows).
//     "Optimize Tree Prefabs" merges each tree into ONE mesh per material and builds
//     real LOD levels (LOD1/LOD2 keep fewer, slightly bigger leaf clusters).
//
//  2. Some foliage materials were Transparent (huge overdraw, no depth pre-pass).
//     "Fix Foliage Materials" turns them into Opaque + Alpha Clipping and enables GPU instancing.
//
// The prefab files are edited in place (same GUID), so every scene instance and every
// tree painted on a terrain picks up the optimized version automatically.
public static class FoliageOptimizer
{
    // Tree prefabs to optimize (paths relative to the project).
    static readonly string[] TreePrefabs =
    {
        "Assets/Imports/Trees/one/All/tree1_LODParent.prefab",
        "Assets/Imports/Trees/one/All/tree2_LODParent.prefab",
        "Assets/Imports/Trees/one/All/tree3_LODParent.prefab",
        "Assets/Imports/Trees/one/All/tree4_LODParent.prefab",
        "Assets/Imports/Trees/one/All/tree5_LODParent.prefab",
        "Assets/Imports/Trees/one/All/tree6_LODParent.prefab",
        "Assets/Imports/Trees/one/All/tree7_LODParent.prefab",
        "Assets/Imports/Trees/one/All/tree8_LODParent.prefab",
        "Assets/Imports/Trees/two/instance_0_LODParent.prefab",
        "Assets/Imports/Trees/Cypress_V2_test_LODParent.prefab",
        "Assets/Imports/Trees/beech_LODParent.prefab",
        "Assets/Imports/Trees/five/Pine.prefab",
    };

    // Folders whose materials are foliage (leaves, grass, bark).
    static readonly string[] FoliageMaterialFolders =
    {
        "Assets/Imports/Trees",
        "Assets/Imports/Grass_Dry",
        "Assets/Imports/grassgreen",
    };

    const string MeshFolder = "Assets/Imports/Trees/_OptimizedMeshes";

    // LOD switch points, as a fraction of screen height (for a ~14 m tree at 60° FOV:
    // 0.30 ≈ 40 m, 0.12 ≈ 100 m, 0.015 ≈ 800 m).
    // LOD0 = full mesh, LOD1 = thinned mesh, LOD2 = impostor (2 crossed quads with a baked picture).
    const float Lod0Height = 0.30f;
    const float Lod1Height = 0.12f;
    const float CullHeight = 0.015f;

    // Fraction of loose parts (leaf clusters / twigs) kept in LOD1,
    // and how much the kept ones are enlarged to cover the gaps.
    const float Lod1Keep = 0.5f, Lod1Scale = 1.25f;

    // Impostor texture: two views side by side, each ImpostorRes x ImpostorRes.
    const int ImpostorRes = 512;
    const int ImpostorLayer = 31;

    // Parts at least this big (relative to the whole tree) are "structural" (trunk, main limbs)
    // and are kept in every LOD.
    const float StructuralSizeRatio = 0.35f;

    struct Part
    {
        public Mesh Mesh;
        public int SubMesh;
        public Material Material;
        public Matrix4x4 ToRoot;
        public int Group;        // index of the source renderer; parts of one renderer are thinned together
        public bool Structural;
        public Bounds RootBounds;
    }

    // ------------------------------------------------------------------
    // Tools > Foliage > Optimize Tree Prefabs
    // ------------------------------------------------------------------
    [MenuItem("Tools/Foliage/Optimize Tree Prefabs")]
    public static void OptimizeTreePrefabs()
    {
        if (!AssetDatabase.IsValidFolder(MeshFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(MeshFolder).Replace('\\', '/'), Path.GetFileName(MeshFolder));

        int done = 0;
        try
        {
            for (int i = 0; i < TreePrefabs.Length; i++)
            {
                string path = TreePrefabs[i];
                EditorUtility.DisplayProgressBar("Optimizing trees", path, (float)i / TreePrefabs.Length);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"Foliage: prefab not found, skipped: {path}");
                    continue;
                }
                if (OptimizePrefab(path))
                    done++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Foliage: optimized {done} tree prefab(s).");
    }

    public static bool OptimizePrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (root.transform.Find("LOD0") != null && root.transform.childCount == 3)
            {
                Debug.Log($"Foliage: already optimized, skipped: {prefabPath}");
                return false;
            }

            List<Part> parts = CollectParts(root, out int rendererCount);
            if (parts.Count == 0)
                return false;

            // Tree bounds in root space, used to classify structural parts.
            Bounds treeBounds = parts[0].RootBounds;
            foreach (Part p in parts)
                treeBounds.Encapsulate(p.RootBounds);
            float treeSize = treeBounds.size.magnitude;
            for (int i = 0; i < parts.Count; i++)
            {
                Part p = parts[i];
                p.Structural = rendererCount <= 3 || p.RootBounds.size.magnitude >= treeSize * StructuralSizeRatio;
                parts[i] = p;
            }

            // Only thin the tree if it is actually made of many loose parts.
            bool canThin = rendererCount > 20;

            string baseName = Path.GetFileNameWithoutExtension(prefabPath);
            Mesh lod0 = BuildLodMesh(parts, 1f, 1f, out Material[] mats0, baseName + "_LOD0");
            Mesh lod1;
            Material[] mats1 = mats0;
            if (canThin)
                lod1 = BuildLodMesh(parts, Lod1Keep, Lod1Scale, out mats1, baseName + "_LOD1");
            else // Single-mesh tree: thin the individual leaf cards instead of whole branches.
                lod1 = ThinLeafIslands(lod0, mats0, Lod1Keep, Lod1Scale, baseName + "_LOD1");

            Mesh lod2 = BakeImpostor(lod0, mats0, baseName, out Material impostorMat);

            // Save meshes as one asset file (LOD0 main, the others as sub-assets).
            string meshPath = $"{MeshFolder}/{baseName}_Meshes.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(lod0, meshPath);
            AssetDatabase.AddObjectToAsset(lod1, meshPath);
            AssetDatabase.AddObjectToAsset(lod2, meshPath);
            Material[] mats2 = { impostorMat };

            // Strip every old renderer / child from the prefab.
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            foreach (var smr in root.GetComponents<SkinnedMeshRenderer>()) Object.DestroyImmediate(smr);
            foreach (var mr in root.GetComponents<MeshRenderer>()) Object.DestroyImmediate(mr);
            foreach (var mf in root.GetComponents<MeshFilter>()) Object.DestroyImmediate(mf);

            Renderer r0 = CreateLodChild(root, "LOD0", lod0, mats0, ShadowCastingMode.On);
            Renderer r1 = CreateLodChild(root, "LOD1", lod1, mats1, ShadowCastingMode.On);
            Renderer r2 = CreateLodChild(root, "LOD2", lod2, mats2, ShadowCastingMode.Off);

            LODGroup group = root.GetComponent<LODGroup>();
            if (group == null) group = root.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.SetLODs(new[]
            {
                new LOD(Lod0Height, new[] { r0 }),
                new LOD(Lod1Height, new[] { r1 }),
                new LOD(CullHeight, new[] { r2 }),
            });
            group.RecalculateBounds();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            Debug.Log($"Foliage: {baseName}: {rendererCount} renderers -> 1 per LOD " +
                      $"({mats0.Length} materials). Triangles LOD0/1/impostor: " +
                      $"{Tris(lod0)}/{Tris(lod1)}/{Tris(lod2)}");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static List<Part> CollectParts(GameObject root, out int rendererCount)
    {
        var parts = new List<Part>();
        Matrix4x4 worldToRoot = root.transform.worldToLocalMatrix;
        rendererCount = 0;

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh;
            Matrix4x4 toRoot;

            if (r is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) continue;
                // Trunks were exported as skinned meshes but are never animated: bake them to a static mesh.
                mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                smr.BakeMesh(mesh, true);
                Transform t = smr.transform;
                toRoot = worldToRoot * Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
            }
            else if (r is MeshRenderer)
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                mesh = mf.sharedMesh;
                toRoot = worldToRoot * r.transform.localToWorldMatrix;
            }
            else continue;

            Bounds rootBounds = TransformBounds(mesh.bounds, toRoot);
            Material[] mats = r.sharedMaterials;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                Material mat = mats.Length > 0 ? mats[Mathf.Min(s, mats.Length - 1)] : null;
                if (mat == null) continue;
                parts.Add(new Part
                {
                    Mesh = mesh, SubMesh = s, Material = mat, ToRoot = toRoot,
                    Group = rendererCount, RootBounds = rootBounds,
                });
            }
            rendererCount++;
        }
        return parts;
    }

    // Combines the parts into one mesh with one sub-mesh per material.
    // keep < 1 drops a deterministic share of the non-structural parts and enlarges the rest.
    static Mesh BuildLodMesh(List<Part> parts, float keep, float scale, out Material[] materials, string name)
    {
        var byMaterial = new Dictionary<Material, List<CombineInstance>>();
        var order = new List<Material>();

        foreach (Part p in parts)
        {
            Matrix4x4 m = p.ToRoot;
            if (!p.Structural && keep < 1f)
            {
                if (Hash01(p.Group) >= keep) continue;
                // Scale the cluster around its own centre so the thinner canopy still looks full.
                Vector3 c = p.RootBounds.center;
                m = Matrix4x4.Translate(c) * Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.Translate(-c) * m;
            }

            if (!byMaterial.TryGetValue(p.Material, out var list))
            {
                list = new List<CombineInstance>();
                byMaterial[p.Material] = list;
                order.Add(p.Material);
            }
            list.Add(new CombineInstance { mesh = p.Mesh, subMeshIndex = p.SubMesh, transform = m });
        }

        var perMaterial = new CombineInstance[order.Count];
        for (int i = 0; i < order.Count; i++)
        {
            var merged = new Mesh { indexFormat = IndexFormat.UInt32 };
            merged.CombineMeshes(byMaterial[order[i]].ToArray(), true, true);
            perMaterial[i] = new CombineInstance { mesh = merged, transform = Matrix4x4.identity };
        }

        var result = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        result.CombineMeshes(perMaterial, false, false);
        result.RecalculateBounds();
        result.Optimize();
        foreach (var ci in perMaterial) Object.DestroyImmediate(ci.mesh);

        materials = order.ToArray();
        return result;
    }

    // For trees that are a single mesh: split each leaf sub-mesh into its connected pieces
    // (leaf cards / clusters), keep a deterministic share of them and enlarge the kept ones.
    // Bark sub-meshes are kept untouched.
    static Mesh ThinLeafIslands(Mesh src, Material[] mats, float keep, float scale, string name, bool allSubMeshes = false)
    {
        Mesh result = Object.Instantiate(src);
        result.name = name;
        var verts = new List<Vector3>();
        src.GetVertices(verts);
        var parent = new int[verts.Count];

        for (int s = 0; s < src.subMeshCount; s++)
        {
            if (!allSubMeshes && (s >= mats.Length || !IsLeafMaterial(mats[s]))) continue;
            int[] tris = src.GetTriangles(s);
            foreach (int v in tris) parent[v] = v;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Union(parent, tris[t], tris[t + 1]);
                Union(parent, tris[t], tris[t + 2]);
            }

            // Triangle count, vertex set and centre of every island.
            var islandTris = new Dictionary<int, int>();
            var islandCenter = new Dictionary<int, Vector3>();
            var islandVerts = new Dictionary<int, List<int>>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int root = Find(parent, tris[t]);
                islandTris[root] = islandTris.TryGetValue(root, out int c) ? c + 1 : 1;
            }
            var seen = new HashSet<int>();
            foreach (int v in tris)
            {
                if (!seen.Add(v)) continue;
                int root = Find(parent, v);
                if (!islandVerts.TryGetValue(root, out var list)) islandVerts[root] = list = new List<int>();
                list.Add(v);
            }
            foreach (var kv in islandVerts)
            {
                Vector3 sum = Vector3.zero;
                foreach (int v in kv.Value) sum += verts[v];
                islandCenter[kv.Key] = sum / kv.Value.Count;
            }

            // Islands that are a big part of the sub-mesh (welded canopy) are always kept.
            int bigIsland = Mathf.Max(1, tris.Length / 3 / 5);
            var kept = new List<int>(tris.Length);
            var keepRoot = new Dictionary<int, bool>();
            foreach (var kv in islandTris)
                keepRoot[kv.Key] = kv.Value >= bigIsland || Hash01(kv.Key) < keep;
            for (int t = 0; t < tris.Length; t += 3)
            {
                if (!keepRoot[Find(parent, tris[t])]) continue;
                kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]);
            }
            foreach (var kv in islandVerts)
            {
                if (!keepRoot[kv.Key] || islandTris[kv.Key] >= bigIsland) continue;
                Vector3 c = islandCenter[kv.Key];
                foreach (int v in kv.Value) verts[v] = c + (verts[v] - c) * scale;
            }
            result.SetTriangles(kept, s, false);
        }

        result.SetVertices(verts);
        result.RecalculateBounds();
        return result;
    }

    // Renders the tree from the front and from the side into one texture, and returns
    // two crossed quads that show those pictures (4 triangles in total).
    static Mesh BakeImpostor(Mesh mesh, Material[] mats, string baseName, out Material material)
    {
        Bounds b = mesh.bounds;
        float half = Mathf.Max(b.extents.y, Mathf.Max(b.extents.x, b.extents.z));
        Vector3 bakePos = new Vector3(0f, -5000f, 0f);   // far from the level; only the bake layer is rendered

        var treeGo = new GameObject("__ImpostorBakeTree") { hideFlags = HideFlags.HideAndDontSave, layer = ImpostorLayer };
        treeGo.transform.position = bakePos;
        treeGo.AddComponent<MeshFilter>().sharedMesh = mesh;
        var treeRenderer = treeGo.AddComponent<MeshRenderer>();
        treeRenderer.sharedMaterials = mats;
        treeRenderer.shadowCastingMode = ShadowCastingMode.Off;

        var camGo = new GameObject("__ImpostorBakeCamera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = half;
        cam.aspect = 1f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = half * 4f + 1f;
        cam.cullingMask = 1 << ImpostorLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.allowHDR = false;
        cam.allowMSAA = false;
        var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        camData.renderPostProcessing = false;
        camData.renderShadows = false;
        camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;

        var rt = new RenderTexture(ImpostorRes, ImpostorRes, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var atlas = new Texture2D(ImpostorRes * 2, ImpostorRes, TextureFormat.RGBA32, false, false);
        Vector3 center = bakePos + b.center;
        Vector3[] viewDirs = { Vector3.forward, Vector3.right };
        try
        {
            for (int v = 0; v < 2; v++)
            {
                camGo.transform.SetPositionAndRotation(center - viewDirs[v] * (half * 2f + 0.5f), Quaternion.LookRotation(viewDirs[v], Vector3.up));
                var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request))
                    RenderPipeline.SubmitRenderRequest(cam, request);
                else
                {
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;
                }
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                atlas.ReadPixels(new Rect(0, 0, ImpostorRes, ImpostorRes), v * ImpostorRes, 0);
                RenderTexture.active = prev;
            }
        }
        finally
        {
            Object.DestroyImmediate(treeGo);
            Object.DestroyImmediate(camGo);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
        DilateColors(atlas, 8);
        atlas.Apply();

        // Save the texture and set up its import settings.
        string texPath = $"{MeshFolder}/{baseName}_Impostor.png";
        File.WriteAllBytes(texPath, atlas.EncodeToPNG());
        Object.DestroyImmediate(atlas);
        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.mipMapsPreserveCoverage = true;   // keeps distant impostors from thinning out
        importer.alphaTestReferenceValue = 0.5f;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = ImpostorRes * 2;
        importer.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        // Unlit: the lighting is already baked into the picture.
        string matPath = $"{MeshFolder}/{baseName}_Impostor.mat";
        material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, matPath);
        }
        material.SetTexture("_BaseMap", tex);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.5f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);

        // Two crossed quads through the centre of the bounds.
        // View 0 looked along +Z (screen right = +X); view 1 looked along +X (screen right = -Z).
        Vector3 c = b.center;
        float y0 = c.y - half, y1 = c.y + half;
        var verts = new[]
        {
            new Vector3(c.x - half, y0, c.z), new Vector3(c.x + half, y0, c.z), new Vector3(c.x + half, y1, c.z), new Vector3(c.x - half, y1, c.z),
            new Vector3(c.x, y0, c.z + half), new Vector3(c.x, y0, c.z - half), new Vector3(c.x, y1, c.z - half), new Vector3(c.x, y1, c.z + half),
        };
        var uvs = new[]
        {
            new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 1f),
            new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
        };
        var normals = new Vector3[8];
        for (int i = 0; i < 8; i++) normals[i] = Vector3.up;
        var quads = new Mesh { name = baseName + "_Impostor" };
        quads.vertices = verts;
        quads.uv = uvs;
        quads.normals = normals;
        quads.triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 };
        quads.RecalculateBounds();
        return quads;
    }

    // Spreads the colour of opaque pixels into the transparent area around them,
    // so mip-maps don't get dark fringes from the black background.
    static void DilateColors(Texture2D tex, int iterations)
    {
        int w = tex.width, h = tex.height;
        Color32[] px = tex.GetPixels32();
        var filled = new bool[px.Length];
        for (int i = 0; i < px.Length; i++) filled[i] = px[i].a > 0;
        for (int it = 0; it < iterations; it++)
        {
            var next = (Color32[])px.Clone();
            var nextFilled = (bool[])filled.Clone();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (filled[i]) continue;
                int r = 0, g = 0, bl = 0, n = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int j = ny * w + nx;
                    if (!filled[j]) continue;
                    r += px[j].r; g += px[j].g; bl += px[j].b; n++;
                }
                if (n == 0) continue;
                next[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(bl / n), 0);
                nextFilled[i] = true;
            }
            px = next;
            filled = nextFilled;
        }
        tex.SetPixels32(px);
    }

    static bool IsLeafMaterial(Material m) =>
        m != null && ((m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f) || m.renderQueue >= (int)RenderQueue.AlphaTest);

    static int Find(int[] p, int x)
    {
        while (p[x] != x) { p[x] = p[p[x]]; x = p[x]; }
        return x;
    }

    static void Union(int[] p, int a, int b)
    {
        a = Find(p, a); b = Find(p, b);
        if (a != b) p[b] = a;
    }

    static Renderer CreateLodChild(GameObject root, string name, Mesh mesh, Material[] mats, ShadowCastingMode shadows)
    {
        var go = new GameObject(name);
        go.layer = root.layer;
        GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(root));
        go.transform.SetParent(root.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mats;
        mr.shadowCastingMode = shadows;
        mr.lightProbeUsage = LightProbeUsage.BlendProbes;
        return mr;
    }

    static float Hash01(int i)
    {
        uint x = (uint)i * 2654435761u;
        x ^= x >> 16;
        x *= 0x45d9f3b;
        x ^= x >> 16;
        return (x & 0xFFFFFF) / (float)0x1000000;
    }

    static Bounds TransformBounds(Bounds b, Matrix4x4 m)
    {
        Vector3 c = m.MultiplyPoint3x4(b.center);
        Vector3 e = b.extents;
        Vector3 ax = m.MultiplyVector(new Vector3(e.x, 0, 0));
        Vector3 ay = m.MultiplyVector(new Vector3(0, e.y, 0));
        Vector3 az = m.MultiplyVector(new Vector3(0, 0, e.z));
        Vector3 ext = new Vector3(
            Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x),
            Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y),
            Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
        return new Bounds(c, ext * 2f);
    }

    static long Tris(Mesh m)
    {
        long n = 0;
        for (int s = 0; s < m.subMeshCount; s++) n += m.GetIndexCount(s) / 3;
        return n;
    }

    // ------------------------------------------------------------------
    // Tools > Foliage > Optimize Terrain Details
    // Terrain detail meshes are drawn by the hundred-thousand, so every triangle
    // and every shadow counts. Small ground clutter doesn't need to cast shadows,
    // and the pebble clump (2,880 triangles for a 30 cm object) keeps only a quarter of its pebbles.
    // ------------------------------------------------------------------
    static readonly string[] NoShadowDetailPrefabs =
    {
        "Assets/Imports/pebbles/model.prefab",
        "Assets/Imports/grassgreen/grassgreen3.prefab",
    };
    static readonly string[] DecimateDetailPrefabs = { "Assets/Imports/pebbles/model.prefab" };
    const float DetailKeep = 0.25f;   // share of the individual pebbles kept in each clump

    [MenuItem("Tools/Foliage/Optimize Terrain Details")]
    public static void OptimizeTerrainDetails()
    {
        if (!AssetDatabase.IsValidFolder(MeshFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(MeshFolder).Replace('\\', '/'), Path.GetFileName(MeshFolder));

        foreach (string path in NoShadowDetailPrefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    r.shadowCastingMode = ShadowCastingMode.Off;

                if (System.Array.IndexOf(DecimateDetailPrefabs, path) >= 0)
                {
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        Mesh src = mf.sharedMesh;
                        if (src == null || src.name.EndsWith("_LowPoly")) continue;
                        Mesh low = ThinLeafIslands(src, null, DetailKeep, 1f, src.name + "_LowPoly", allSubMeshes: true);
                        string meshPath = $"{MeshFolder}/{Path.GetFileNameWithoutExtension(path)}_{src.name}_LowPoly.asset";
                        AssetDatabase.DeleteAsset(meshPath);
                        AssetDatabase.CreateAsset(low, meshPath);
                        Debug.Log($"Foliage: {path}: {src.name} {Tris(src)} -> {Tris(low)} triangles");
                        mf.sharedMesh = low;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"Foliage: {path}: shadows off");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            t.terrainData.RefreshPrototypes();
            t.Flush();
        }
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------
    // Tools > Foliage > Fix Foliage Materials
    // Transparent -> Opaque + Alpha Clipping, GPU instancing on.
    // ------------------------------------------------------------------
    [MenuItem("Tools/Foliage/Fix Foliage Materials")]
    public static void FixFoliageMaterials()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", FoliageMaterialFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            bool dirty = false;

            if (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f)
            {
                MakeAlphaClipped(mat);
                Debug.Log($"Foliage: {path}: Transparent -> Opaque + Alpha Clip");
                dirty = true;
            }
            if (!mat.enableInstancing)
            {
                mat.enableInstancing = true;
                dirty = true;
            }
            if (IsLeafMaterial(mat))
                PreserveAlphaCoverage(mat);
            if (dirty)
            {
                EditorUtility.SetDirty(mat);
                changed++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Foliage: updated {changed} material(s).");
    }

    // Without this, the smaller mip-maps of an alpha-clipped leaf texture lose most of their
    // alpha, so leaves "melt away" and trees look bare at a distance.
    static void PreserveAlphaCoverage(Material mat)
    {
        Texture tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
        if (tex == null) return;
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
        if (importer == null || importer.mipMapsPreserveCoverage) return;
        importer.mipMapsPreserveCoverage = true;
        importer.alphaTestReferenceValue = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;
        importer.SaveAndReimport();
        Debug.Log($"Foliage: {importer.assetPath}: mip maps now preserve alpha coverage");
    }

    static void MakeAlphaClipped(Material mat)
    {
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_AlphaClip", 1f);
        if (mat.HasProperty("_Cutoff") && mat.GetFloat("_Cutoff") <= 0.01f) mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.One);
        mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
        mat.SetFloat("_ZWrite", 1f);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        mat.SetShaderPassEnabled("DepthOnly", true);
        mat.SetShaderPassEnabled("SHADOWCASTER", true);
    }
}
