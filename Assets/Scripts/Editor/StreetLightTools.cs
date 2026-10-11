using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

// Tools > Street Lights:
//  1. "Build Optimized Street Light" - turns the imported Street_Light model
//     (146 pieces, 34 materials, 41k triangles, pivot ~640 m away) into ONE
//     prefab: merged LOD0 + light LOD1 + cull, 4 materials, pivot at the pole
//     base, a no-shadow spot light and a pole collider.
//  2. "Place Street Lights Along Roads" - puts copies along every road spline
//     under 'Roads' in the open scene, into 'St_Lts'. One Ctrl+Z undoes it all.
public static class StreetLightTools
{
    const string SourcePrefab = "Assets/Imports/Streetlight/Street_Light.prefab";
    const string OutFolder = "Assets/Prefabs/StreetLight";
    const string OutPrefab = "Assets/Prefabs/StreetLight/StreetLight_Optimized.prefab";

    // Parts smaller than this (bolts, threads, rivets) are invisible from the road
    const float MinPartSize = 0.1f;

    enum Slot { Pole, Arm, Light, Dark, Bulb }

    class Part
    {
        public Mesh mesh;
        public int subMesh;
        public Matrix4x4 matrix;
        public Slot slot;
        public Bounds bounds;   // whole renderer, world (prefab) space
        public int tris;
    }

    [MenuItem("Tools/Street Lights/Build Optimized Street Light")]
    public static void BuildOptimizedStreetLight()
    {
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
        if (src == null) { Debug.LogError("Street light source not found: " + SourcePrefab); return; }
        if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "StreetLight");

        // ---- Collect parts --------------------------------------------------
        var parts = new List<Part>();
        float minY = float.MaxValue, maxY = float.MinValue;
        int sourceRenderers = 0, sourceTris = 0;
        foreach (MeshRenderer r in src.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            sourceRenderers++;
            Mesh m = mf.sharedMesh;
            sourceTris += m.triangles.Length / 3;
            minY = Mathf.Min(minY, r.bounds.min.y);
            maxY = Mathf.Max(maxY, r.bounds.max.y);
            if (r.bounds.size.magnitude < MinPartSize) continue;

            for (int s = 0; s < m.subMeshCount && s < r.sharedMaterials.Length; s++)
            {
                Material mat = r.sharedMaterials[s];
                if (mat == null) continue;
                parts.Add(new Part
                {
                    mesh = m, subMesh = s, matrix = r.transform.localToWorldMatrix,
                    slot = SlotFor(mat), bounds = r.bounds, tris = (int)m.GetIndexCount(s) / 3
                });
            }
        }

        // ---- Pivot: centre of the pole's foot ------------------------------
        Vector3 footMin = new Vector3(float.MaxValue, 0, float.MaxValue), footMax = -footMin;
        foreach (Part p in parts)
        {
            Vector3[] v = p.mesh.vertices;
            foreach (Vector3 lv in v)
            {
                Vector3 w = p.matrix.MultiplyPoint3x4(lv);
                if (w.y > minY + 0.3f) continue;
                footMin = Vector3.Min(footMin, w);
                footMax = Vector3.Max(footMax, w);
            }
        }
        Vector3 pivot = new Vector3((footMin.x + footMax.x) * 0.5f, minY, (footMin.z + footMax.z) * 0.5f);
        float footCenterX = pivot.x;
        Matrix4x4 toLocal = Matrix4x4.Translate(-pivot);

        // ---- LOD1: pole, arm, base, bulb + one box for the lamp head ----------
        var lod1 = new List<Part>();
        Bounds head = new Bounds();
        bool hasHead = false;
        foreach (Part p in parts)
        {
            bool big = p.bounds.size.magnitude >= 0.3f;
            // The lamp head: high up AND out at the end of the arm (not the pole's top cap)
            bool inHead = p.bounds.center.y > maxY - 1.5f && p.bounds.size.magnitude < 2f
                          && Mathf.Abs(p.bounds.center.x - footCenterX) > 1.5f;
            if (p.slot == Slot.Bulb && big) lod1.Add(p);
            else if (!inHead && big) lod1.Add(p);
            else if (inHead)
            {
                if (!hasHead) { head = p.bounds; hasHead = true; }
                else head.Encapsulate(p.bounds);
            }
        }

        Material[] mats = BuildMaterials(src); // order matches Slot
        Mesh lod0Mesh = Combine(parts, toLocal, "StreetLight_LOD0", null, false, mats, out Material[] lod0Mats);
        Mesh lod1Mesh = Combine(lod1, toLocal, "StreetLight_LOD1", hasHead ? (Bounds?)new Bounds(head.center - pivot + Vector3.up * head.size.y * 0.2f, Vector3.Scale(head.size, new Vector3(0.95f, 0.6f, 0.95f))) : null, true, mats, out Material[] lod1Mats);
        SaveAsset(lod0Mesh, OutFolder + "/StreetLight_LOD0.asset");
        SaveAsset(lod1Mesh, OutFolder + "/StreetLight_LOD1.asset");

        // ---- Bulb position (for the Light) -----------------------------------
        Bounds bulb = new Bounds();
        bool hasBulb = false;
        foreach (Part p in parts)
            if (p.slot == Slot.Bulb)
            {
                if (!hasBulb) { bulb = p.bounds; hasBulb = true; }
                else bulb.Encapsulate(p.bounds);
            }
        Vector3 lightPos = hasBulb ? new Vector3(bulb.center.x, bulb.min.y - 0.15f, bulb.center.z) - pivot
                                   : new Vector3(2.8f, maxY - minY - 0.5f, 0f);

        // ---- Prefab ------------------------------------------------------------
        var root = new GameObject("StreetLight");
        GameObject l0 = MakeLodChild(root, "LOD0", lod0Mesh, lod0Mats, ShadowCastingMode.On);
        // Beyond ~60 m a pole's shadow is a hairline nobody sees - skip it
        GameObject l1 = MakeLodChild(root, "LOD1", lod1Mesh, lod1Mats, ShadowCastingMode.Off);

        var lodGroup = root.AddComponent<LODGroup>();
        lodGroup.SetLODs(new[]
        {
            new LOD(0.15f, new Renderer[] { l0.GetComponent<Renderer>() }),
            new LOD(0.035f, new Renderer[] { l1.GetComponent<Renderer>() }), // hidden beyond ~250 m
        });
        lodGroup.fadeMode = LODFadeMode.None;
        lodGroup.RecalculateBounds();

        var lightGo = new GameObject("Lamp Light");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = lightPos;
        // Down, tilted ~20 degrees out over the road (+X is the arm's direction)
        lightGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0.36f, -1f, 0f), Vector3.right);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Spot;
        light.range = 24f;
        light.spotAngle = 110f;
        light.innerSpotAngle = 60f;
        light.intensity = 70f;
        light.color = new Color(1f, 0.82f, 0.58f); // warm sodium-ish white
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;

        // Pole collider so cars and the player don't drive through it
        var col = root.AddComponent<CapsuleCollider>();
        col.direction = 1;
        col.radius = Mathf.Max(0.12f, (footMax.x - footMin.x) * 0.5f);
        col.height = maxY - minY - 1f;
        col.center = new Vector3(0f, col.height * 0.5f, 0f);

        PrefabUtility.SaveAsPrefabAsset(root, OutPrefab);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        Debug.Log($"Street light optimized: {sourceRenderers} renderers / {sourceTris} tris / 34 materials  ->  1 renderer, " +
                  $"LOD0 {Tris(lod0Mesh)} tris, LOD1 {Tris(lod1Mesh)} tris, {mats.Length} materials. Saved {OutPrefab}");
    }

    // Every source material is a shade of grey: the big textured pole keeps
    // its texture, glass becomes the glowing bulb, everything else collapses
    // into a light or dark grey metal.
    static Slot SlotFor(Material m)
    {
        if (m.name == "material_100") return Slot.Pole;
        if (m.name == "material_3") return Slot.Arm; // bronze arm - its UVs use only part of the texture, so keep it textured
        if (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) return Slot.Bulb;
        Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
        Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
        if (t != null) c *= AverageColor(t);
        return c.grayscale < 0.55f ? Slot.Dark : Slot.Light;
    }

    // True average colour: halve the texture repeatedly (each bilinear
    // halving averages 2x2 texels exactly) down to a single pixel
    public static Color AverageColor(Texture t)
    {
        int w = Mathf.Max(1, Mathf.NextPowerOfTwo(t.width)), h = Mathf.Max(1, Mathf.NextPowerOfTwo(t.height));
        var cur = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        cur.filterMode = FilterMode.Bilinear;
        Graphics.Blit(t, cur);
        while (w > 1 || h > 1)
        {
            w = Mathf.Max(1, w / 2); h = Mathf.Max(1, h / 2);
            var next = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            next.filterMode = FilterMode.Bilinear;
            Graphics.Blit(cur, next);
            RenderTexture.ReleaseTemporary(cur);
            cur = next;
        }
        var prev = RenderTexture.active;
        RenderTexture.active = cur;
        var px = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        px.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
        RenderTexture.active = prev;
        Color c = px.GetPixel(0, 0);
        Object.DestroyImmediate(px);
        RenderTexture.ReleaseTemporary(cur);
        return c;
    }

    static Material[] BuildMaterials(GameObject src)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material poleSrc = null, armSrc = null;
        foreach (Renderer r in src.GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials)
            {
                if (m != null && m.name == "material_100") poleSrc = m;
                if (m != null && m.name == "material_3") armSrc = m;
            }

        Material pole = MakeMaterial("StreetLight_Pole", poleSrc != null ? new Material(poleSrc) : new Material(lit), null);
        pole.SetFloat("_Smoothness", 0.25f);

        Material arm = MakeMaterial("StreetLight_Arm", armSrc != null ? new Material(armSrc) : new Material(lit), null);
        arm.SetFloat("_Smoothness", 0.3f);

        Material light = MakeMaterial("StreetLight_MetalLight", new Material(lit), new Color(0.80f, 0.80f, 0.80f));
        light.SetFloat("_Metallic", 0.4f);
        light.SetFloat("_Smoothness", 0.4f);

        Material dark = MakeMaterial("StreetLight_MetalDark", new Material(lit), new Color(0.42f, 0.43f, 0.44f));
        dark.SetFloat("_Metallic", 0.4f);
        dark.SetFloat("_Smoothness", 0.35f);

        // Opaque glowing bulb - looks lit (with Bloom) even when the Light is culled
        Material bulb = MakeMaterial("StreetLight_Bulb", new Material(lit), new Color(1f, 0.92f, 0.8f));
        bulb.EnableKeyword("_EMISSION");
        bulb.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.55f) * 8f);
        bulb.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        foreach (Material m in new[] { pole, arm, light, dark, bulb }) EditorUtility.SetDirty(m);
        return new[] { pole, arm, light, dark, bulb };
    }

    static Material MakeMaterial(string name, Material m, Color? color)
    {
        string path = OutFolder + "/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) { existing.CopyPropertiesFromMaterial(m); existing.shader = m.shader; m = existing; }
        else AssetDatabase.CreateAsset(m, path);
        m.name = name;
        if (color.HasValue) m.SetColor("_BaseColor", color.Value);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
        m.SetOverrideTag("RenderType", "Opaque");
        m.renderQueue = -1;
        m.enableInstancing = true;
        return m;
    }

    // One sub-mesh per used Slot (max 5 draw calls), optionally plus the head stand-in.
    // far = LOD1: the grey metals share the pole's material (3 draw calls)
    static Mesh Combine(List<Part> parts, Matrix4x4 toLocal, string name, Bounds? box, bool far, Material[] slotMats, out Material[] usedMats)
    {
        var perSlot = new List<Mesh>();
        var used = new List<Material>();
        foreach (Slot slot in System.Enum.GetValues(typeof(Slot)))
        {
            var ci = new List<CombineInstance>();
            foreach (Part p in parts)
                if (FarSlot(p.slot, far) == slot)
                    ci.Add(new CombineInstance { mesh = p.mesh, subMeshIndex = p.subMesh, transform = toLocal * p.matrix });
            if (box.HasValue && slot == FarSlot(Slot.Light, far))
                ci.Add(new CombineInstance { mesh = BoxMesh(box.Value), subMeshIndex = 0, transform = Matrix4x4.identity });

            if (ci.Count == 0) continue; // skip empty slots so sub-mesh N always matches material N
            var m = new Mesh { indexFormat = IndexFormat.UInt32 };
            m.CombineMeshes(ci.ToArray(), true, true);
            perSlot.Add(m);
            used.Add(slotMats[(int)slot]);
        }
        usedMats = used.ToArray();

        var final = new List<CombineInstance>();
        int totalVerts = 0;
        foreach (Mesh m in perSlot)
        {
            totalVerts += m.vertexCount;
            final.Add(new CombineInstance { mesh = m, transform = Matrix4x4.identity });
        }
        var mesh = new Mesh { name = name, indexFormat = totalVerts < 65000 ? IndexFormat.UInt16 : IndexFormat.UInt32 };
        mesh.CombineMeshes(final.ToArray(), false, false);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        foreach (Mesh m in perSlot) Object.DestroyImmediate(m);
        return mesh;
    }

    static Slot FarSlot(Slot s, bool far) => far && (s == Slot.Light || s == Slot.Dark) ? Slot.Pole : s;

    static Mesh BoxMesh(Bounds b)
    {
        // The lamp head is a round disc, so a squashed cylinder stands in for it
        GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Mesh src = cyl.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(cyl);
        var m = Object.Instantiate(src);
        Vector3[] v = m.vertices;
        // Unity's cylinder is 1 wide, 2 tall
        for (int i = 0; i < v.Length; i++) v[i] = b.center + Vector3.Scale(v[i], new Vector3(b.size.x, b.size.y * 0.5f, b.size.z));
        m.vertices = v;
        m.RecalculateBounds();
        return m;
    }

    static GameObject MakeLodChild(GameObject root, string name, Mesh mesh, Material[] mats, ShadowCastingMode shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = mats;
        r.shadowCastingMode = shadows;
        return go;
    }

    static void SaveAsset(Object asset, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
    }

    static int Tris(Mesh m) => m.triangles.Length / 3;

    // ======================================================================
    // Placement
    // ======================================================================

    const float Spacing = 35f;        // metres between lamps along a road (sides alternate)
    const float EdgeGap = 1.0f;       // metres beyond the road edge
    const float MinLampDistance = 20f; // no two lamps closer than this (junctions, parallel roads)

    [MenuItem("Tools/Street Lights/Place Street Lights Along Roads")]
    public static void PlaceStreetLights()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutPrefab);
        if (prefab == null) { Debug.LogError("Build the optimized street light first."); return; }
        GameObject roads = GameObject.Find("Roads");
        if (roads == null) { Debug.LogError("No 'Roads' object in the open scene."); return; }

        GameObject parent = GameObject.Find("St_Lts");
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Place Street Lights");
        int undoGroup = Undo.GetCurrentGroup();
        if (parent == null)
        {
            parent = new GameObject("St_Lts");
            Undo.RegisterCreatedObjectUndo(parent, "Place Street Lights");
        }
        if (parent.GetComponent<StreetLightCuller>() == null) Undo.AddComponent<StreetLightCuller>(parent);

        var existing = new List<Vector3>();
        foreach (Transform t in parent.transform) existing.Add(t.position);

        List<Pose> spots = FindSpots(roads.transform, existing, out string report);
        for (int i = 0; i < spots.Count; i++)
        {
            var lamp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            lamp.name = $"StreetLight_{existing.Count + i:000}";
            lamp.transform.SetPositionAndRotation(spots[i].position, spots[i].rotation);
            Undo.RegisterCreatedObjectUndo(lamp, "Place Street Lights");
        }

        Undo.CollapseUndoOperations(undoGroup);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(parent.scene);
        Debug.Log($"Placed {spots.Count} street lights under '{parent.name}'. {report} Ctrl+Z removes them all.");
    }

    // Works out where lamps go without placing anything (also used for a dry run)
    public static List<Pose> FindSpots(Transform roads, List<Vector3> existing, out string report)
    {
        var spots = new List<Pose>();
        var placed = new List<Vector3>(existing);
        int onJunction = 0, nearOther = 0, blocked = 0, onRoadEdge = 0, offCorner = 0;
        List<Renderer> obstacles = CollectObstacles(roads);

        foreach (SplineExtrude extrude in roads.GetComponentsInChildren<SplineExtrude>())
        {
            SplineContainer container = extrude.Container;
            if (container == null) continue;
            // NB: extrude.Radius (10) is NOT the asphalt's half width (5) for these
            // road shapes, so the real edge is measured at every spot instead
            Collider roadCollider = extrude.GetComponent<Collider>();

            for (int s = 0; s < container.Splines.Count; s++)
            {
                float length = container.CalculateLength(s);
                int side = 1;
                for (float d = Spacing * 0.5f; d < length; d += Spacing, side = -side)
                {
                    float t = d / length;
                    Vector3 center = container.EvaluatePosition(s, t);
                    Vector3 tangent = container.EvaluateTangent(s, t);
                    tangent.y = 0f;
                    if (tangent.sqrMagnitude < 0.0001f) continue;
                    tangent.Normalize();
                    Vector3 outward = Vector3.Cross(Vector3.up, tangent) * side; // right side, then left
                    float halfWidth = RoadEdge(center, outward, roadCollider);
                    Vector3 basePos = center + outward * (halfWidth + EdgeGap);

                    if (TooClose(basePos, placed)) { nearOther++; continue; }

                    // What's below the spot beside the road?
                    if (!TopHit(basePos, out RaycastHit hit)) { blocked++; continue; }
                    if (hit.collider.transform.IsChildOf(roads)) { onJunction++; continue; } // another road runs here
                    if (!(hit.collider is TerrainCollider)) { blocked++; continue; }          // building, bridge, prop...
                    basePos.y = hit.point.y;

                    // Road on an embankment (well above/below the ground beside it):
                    // stand the lamp on the road's outer edge instead of down the slope
                    Vector3 edge = center + outward * (halfWidth - 0.4f);
                    if (TopHit(edge, out RaycastHit roadHit) && roadHit.collider.transform.IsChildOf(roads)
                        && Mathf.Abs(roadHit.point.y - basePos.y) > 0.5f)
                    {
                        basePos = new Vector3(edge.x, roadHit.point.y, edge.z);
                        onRoadEdge++;
                    }

                    if (OverlapsSomethingSolid(basePos, roads) || OverlapsSomethingVisible(basePos, obstacles)) { blocked++; continue; }

                    // On sharp corners the road can curve away from the spot - the
                    // arm must still reach over asphalt within 3 m
                    if (!RoadWithin(basePos, -outward, 3f, roads)) { offCorner++; continue; }

                    // Arm (model +X) reaches over the road
                    spots.Add(new Pose(basePos, Quaternion.LookRotation(Vector3.Cross(-outward, Vector3.up), Vector3.up)));
                    placed.Add(basePos);
                }
            }
        }

        report = $"Skipped: {onJunction} on junctions, {nearOther} too close to another lamp, {blocked} blocked by objects, {offCorner} on corners where the road curves away. {onRoadEdge} stand on the road edge (embankments).";
        return spots;
    }

    static bool TooClose(Vector3 p, List<Vector3> placed)
    {
        foreach (Vector3 q in placed)
        {
            float dx = p.x - q.x, dz = p.z - q.z;
            if (dx * dx + dz * dz < MinLampDistance * MinLampDistance) return true;
        }
        return false;
    }

    // Distance from the spline to where this road's surface ends, on one side
    static float RoadEdge(Vector3 center, Vector3 outward, Collider road)
    {
        float edge = 0f;
        for (float o = 0f; o <= 15f; o += 0.25f)
        {
            Vector3 p = center + outward * o;
            var ray = new Ray(new Vector3(p.x, p.y + 30f, p.z), Vector3.down);
            if (road != null && road.Raycast(ray, out _, 80f)) edge = o;
            else if (o > 1f) break; // left the road
        }
        return edge > 0.5f ? edge : 5f;
    }

    // Visible things that might have no collider (billboards, signs, props, trees)
    static List<Renderer> CollectObstacles(Transform roads)
    {
        var list = new List<Renderer>();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.transform.IsChildOf(roads)) continue;
            if (r.GetComponentInParent<StreetLightCuller>() != null) continue; // our own lamps
            Vector3 size = r.bounds.size;
            if (size.x > 40f || size.z > 40f) continue; // ground planes, water, huge meshes - too coarse to judge by box
            list.Add(r);
        }
        return list;
    }

    // Does anything's bounding box stand in the pole's column (or the arm's reach)?
    static bool OverlapsSomethingVisible(Vector3 basePos, List<Renderer> obstacles)
    {
        var column = new Bounds(basePos + Vector3.up * 5.5f, new Vector3(1.2f, 9f, 1.2f));
        foreach (Renderer r in obstacles)
            if (r.bounds.Intersects(column)) return true;
        return false;
    }

    static bool RoadWithin(Vector3 from, Vector3 dir, float maxDist, Transform roads)
    {
        for (float o = 0f; o <= maxDist; o += 0.25f)
            if (TopHit(from + dir * o, out RaycastHit h) && h.collider.transform.IsChildOf(roads)) return true;
        return false;
    }

    static bool TopHit(Vector3 p, out RaycastHit top)
    {
        top = default;
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, p.y + 60f, p.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return false;
        top = hits[0];
        foreach (RaycastHit h in hits) if (h.distance < top.distance) top = h;
        return true;
    }

    static bool OverlapsSomethingSolid(Vector3 basePos, Transform roads)
    {
        foreach (Collider c in Physics.OverlapCapsule(basePos + Vector3.up * 1f, basePos + Vector3.up * 9f, 0.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c is TerrainCollider || c.transform.IsChildOf(roads)) continue;
            return true;
        }
        return false;
    }
}
