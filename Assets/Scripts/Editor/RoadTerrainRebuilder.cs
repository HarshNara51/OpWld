using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

// Tools > Roads > Rebuild Terrain Under Roads
//
// Fixes the ground around ALL roads, keeping today's terrain wherever it's fine
// (so fences, props and lamps beside the roads don't end up floating/buried):
//   - under the asphalt: ground sits just below the road (raised OR lowered),
//     so it never pokes through and never leaves a gap
//   - beside the road: where ground is missing, a smooth slope down to the
//     ground, longer and gentler the bigger the drop          _ - T - _
//     (ground that's already there is left alone - never cut beside a road)
//   - old embankments where NO road runs any more go back to the clean
//     "before roads" ground (TerrainBeforeRoads.bytes) - e.g. after moving a road
//
// All scenes share one TerrainData, so running this in any scene fixes the
// terrain for every scene (they all have the same roads).
// The clean base lives in TerrainBeforeRoads.bytes next to this script's settings.
public static class RoadTerrainRebuilder
{
    public const string BasePath = "Assets/Settings/TerrainBeforeRoads.bytes";

    // Road shape (measured: these roads are 5 m from centre to asphalt edge)
    const float RoadHalfWidth = 5f;
    const float UnderRoadMargin = 1f;   // ground under road + this much beyond the edge is set to road height
    const float FlatShoulder = 1.5f;    // flat strip beside the asphalt before the slope starts
    const float BelowRoad = 0.25f;      // ground sits this far under the road surface
    const float MinSlope = 6f;          // slope length limits (metres)
    const float MaxSlope = 25f;
    const float SlopePerMetre = 2.5f;   // slope length per metre of height difference (~22 degrees)
    const float SampleStep = 0.5f;      // metres between road samples

    [MenuItem("Tools/Roads/Rebuild Terrain Under Roads")]
    public static void RebuildFromMenu()
    {
        Terrain terrain = Terrain.activeTerrain;
        GameObject roads = GameObject.Find("Roads");
        if (terrain == null || roads == null) { Debug.LogError("Need a Terrain and a 'Roads' object in the open scene."); return; }
        if (!File.Exists(BasePath)) { Debug.LogError("No clean base heightmap at " + BasePath); return; }
        if (!EditorUtility.DisplayDialog("Rebuild Terrain Under Roads",
            "Rebuilds the shared terrain around every road from the clean base. Affects ALL scenes (they share the terrain). Undo is available.", "Rebuild", "Cancel"))
            return;
        Debug.Log(Rebuild(terrain, roads.transform, ObjectFootprints(terrain, roads.transform, null)));
    }

    // Terrain cells where something already stands on the ground (fence posts,
    // rocks, wheels, building corners...). Slopes never raise these, so nothing
    // that's already placed gets buried. Pass 'mask' to add to an existing one
    // (all scenes share the terrain, so footprints from every scene matter).
    public static bool[,] ObjectFootprints(Terrain terrain, Transform roads, bool[,] mask)
    {
        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        mask ??= new bool[res, res];
        Vector3 tPos = terrain.transform.position, size = data.size;
        foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (mf.sharedMesh == null) continue;
            Transform t = mf.transform;
            if ((roads != null && t.IsChildOf(roads)) || t.GetComponentInParent<StreetLightCuller>() != null) continue;
            Vector3[] v;
            try { v = mf.sharedMesh.vertices; } catch { continue; }
            Matrix4x4 m = t.localToWorldMatrix;
            int step = Mathf.Max(1, v.Length / 4000);
            for (int i = 0; i < v.Length; i += step)
            {
                Vector3 w = m.MultiplyPoint3x4(v[i]);
                float nx = (w.x - tPos.x) / size.x, nz = (w.z - tPos.z) / size.z;
                if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;
                float ground = tPos.y + data.GetInterpolatedHeight(nx, nz);
                if (Mathf.Abs(w.y - ground) > 0.4f) continue; // not touching the ground
                int cx = Mathf.FloorToInt(nx * (res - 1)), cz = Mathf.FloorToInt(nz * (res - 1));
                for (int dz = 0; dz <= 1; dz++)
                    for (int dx = 0; dx <= 1; dx++)
                        mask[Mathf.Min(res - 1, cz + dz), Mathf.Min(res - 1, cx + dx)] = true;
            }
        }
        return mask;
    }

    public static string Rebuild(Terrain terrain, Transform roads, bool[,] protect)
    {
        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] baseH = LoadBase(res);
        if (baseH == null) return "Base heightmap missing or wrong size.";

        Vector3 tPos = terrain.transform.position;
        Vector3 size = data.size;
        float cellX = size.x / (res - 1), cellZ = size.z / (res - 1);

        // ---- Sample every road: position + actual road surface height ----------
        var samples = new List<Vector3>();
        foreach (SplineContainer c in roads.GetComponentsInChildren<SplineContainer>())
        {
            Collider col = c.GetComponent<Collider>();
            for (int s = 0; s < c.Splines.Count; s++)
            {
                float len = c.CalculateLength(s);
                int n = Mathf.Max(2, Mathf.CeilToInt(len / SampleStep));
                for (int i = 0; i <= n; i++)
                {
                    Vector3 p = c.EvaluatePosition(s, i / (float)n);
                    if (col != null && col.Raycast(new Ray(p + Vector3.up * 20f, Vector3.down), out RaycastHit hit, 40f))
                        p.y = hit.point.y;
                    samples.Add(p);
                }
            }
        }

        // ---- Bucket samples for fast neighbour lookup -----------------------------
        const float bucket = 10f;
        var grid = new Dictionary<long, List<int>>();
        for (int i = 0; i < samples.Count; i++)
        {
            long k = Key(Mathf.FloorToInt(samples[i].x / bucket), Mathf.FloorToInt(samples[i].z / bucket));
            if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
            l.Add(i);
        }
        float reach = RoadHalfWidth + FlatShoulder + MaxSlope;
        int bucketReach = Mathf.CeilToInt(reach / bucket);

        // ---- New heights -------------------------------------------------------------
        float[,] cur = data.GetHeights(0, 0, res, res);
        float[,] h = (float[,])cur.Clone();
        var reached = new bool[res, res];
        var visited = new HashSet<long>();
        int changed = 0, lowered = 0, raised = 0, kept = 0;
        float underEdge = RoadHalfWidth + UnderRoadMargin;
        float flatEdge = RoadHalfWidth + FlatShoulder;

        foreach (var kv in grid)
        {
            int bx = (int)(kv.Key >> 32), bz = (int)(kv.Key & 0xffffffff);
            // Cells inside this bucket
            int x0 = Mathf.Max(0, Mathf.FloorToInt((bx * bucket - tPos.x) / cellX));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt(((bx + 1) * bucket - tPos.x) / cellX));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((bz * bucket - tPos.z) / cellZ));
            int z1 = Mathf.Min(res - 1, Mathf.CeilToInt(((bz + 1) * bucket - tPos.z) / cellZ));
            // Expand to everything a road in this bucket can reach
            int pad = Mathf.CeilToInt(reach / Mathf.Min(cellX, cellZ));
            for (int z = Mathf.Max(0, z0 - pad); z <= Mathf.Min(res - 1, z1 + pad); z++)
                for (int x = Mathf.Max(0, x0 - pad); x <= Mathf.Min(res - 1, x1 + pad); x++)
                {
                    long ck = Key(x, z);
                    if (!visited.Add(ck)) continue;

                    float wx = tPos.x + x * cellX, wz = tPos.z + z * cellZ;
                    float ground = tPos.y + cur[z, x] * size.y;
                    float under = float.MaxValue;   // lowest road surface covering this cell
                    float raiseTo = float.MinValue; // highest slope/shoulder reaching this cell
                    bool any = false, near = false;

                    int cbx = Mathf.FloorToInt(wx / bucket), cbz = Mathf.FloorToInt(wz / bucket);
                    for (int dz = -bucketReach; dz <= bucketReach; dz++)
                        for (int dx = -bucketReach; dx <= bucketReach; dx++)
                        {
                            if (!grid.TryGetValue(Key(cbx + dx, cbz + dz), out var list)) continue;
                            foreach (int i in list)
                            {
                                Vector3 sp = samples[i];
                                float ddx = wx - sp.x, ddz = wz - sp.z;
                                float d = Mathf.Sqrt(ddx * ddx + ddz * ddz);
                                if (d > reach) continue;
                                near = true; // a road runs within reach: never treat as an old leftover
                                float road = sp.y - BelowRoad;

                                if (d <= underEdge) { under = Mathf.Min(under, road); any = true; }

                                // Raise-only beside the road: flat shoulder, then a smooth slope
                                if (road > ground)
                                {
                                    float slope = Mathf.Clamp((road - ground) * SlopePerMetre, MinSlope, MaxSlope);
                                    if (d > flatEdge + slope) continue;
                                    float t = d <= flatEdge ? 0f : (d - flatEdge) / slope;
                                    t = t * t * (3f - 2f * t); // smoothstep: eases into the road AND the ground
                                    raiseTo = Mathf.Max(raiseTo, Mathf.Lerp(road, ground, t));
                                    any = true;
                                }
                            }
                        }

                    if (near) reached[z, x] = true;
                    if (!any) continue;
                    float result = ground;
                    bool keep = protect != null && protect[z, x];
                    if (raiseTo > result && !keep) { result = raiseTo; }
                    if (under != float.MaxValue) result = under; // under the asphalt: exactly road height (raise or lower)
                    if (keep && raiseTo > ground && under == float.MaxValue) kept++;
                    if (Mathf.Abs(result - ground) < 0.001f) continue;

                    if (result < ground) lowered++; else raised++;
                    h[z, x] = Mathf.Clamp01((result - tPos.y) / size.y);
                    changed++;
                }
        }

        // ---- Old embankments no road reaches any more -> clean base -----------------
        int orphans = 0;
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                if (!reached[z, x] && (cur[z, x] - baseH[z, x]) * size.y > 0.05f)
                {
                    h[z, x] = baseH[z, x];
                    orphans++;
                }

        // ---- Apply ---------------------------------------------------------------------
        Undo.RegisterCompleteObjectUndo(data, "Rebuild Terrain Under Roads");
        float[,] before = data.GetHeights(0, 0, res, res);
        data.SetHeights(0, 0, h);

        // ---- Re-seat trees on the new ground ---------------------------------------
        TreeInstance[] trees = data.treeInstances;
        int moved = 0, onRoad = 0;
        for (int i = 0; i < trees.Length; i++)
        {
            Vector3 np = trees[i].position;
            float newY = data.GetInterpolatedHeight(np.x, np.z) / size.y;
            if (Mathf.Abs(newY - np.y) * size.y > 0.02f) { np.y = newY; trees[i].position = np; moved++; }
            Vector3 w = tPos + Vector3.Scale(np, size);
            if (NearRoad(w, samples, grid, bucket, RoadHalfWidth)) onRoad++;
        }
        data.treeInstances = trees;
        data.SyncHeightmap();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        int diffCells = 0;
        float maxDiff = 0f;
        for (int z = 0; z < res; z++) for (int x = 0; x < res; x++)
        {
            float d = Mathf.Abs(h[z, x] - before[z, x]) * size.y;
            if (d > 0.05f) { diffCells++; maxDiff = Mathf.Max(maxDiff, d); }
        }

        return $"Terrain fixed around {samples.Count} road samples: {raised} cells raised, {lowered} lowered, {orphans} old-embankment cells removed, {kept} cells left alone because something stands there. " +
               $"{diffCells} cells differ from before this run (max {maxDiff:F1} m). Trees re-seated: {moved}. Trees standing on asphalt: {onRoad}.";
    }

    static bool NearRoad(Vector3 w, List<Vector3> samples, Dictionary<long, List<int>> grid, float bucket, float dist)
    {
        int bx = Mathf.FloorToInt(w.x / bucket), bz = Mathf.FloorToInt(w.z / bucket);
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (grid.TryGetValue(Key(bx + dx, bz + dz), out var list))
                    foreach (int i in list)
                    {
                        float ddx = w.x - samples[i].x, ddz = w.z - samples[i].z;
                        if (ddx * ddx + ddz * ddz <= dist * dist) return true;
                    }
        return false;
    }

    static long Key(int a, int b) => ((long)a << 32) | (uint)b;

    // ---- Clean base storage: 16-bit heights, row by row ------------------------------
    public static void SaveBase(float[,] h)
    {
        int res = h.GetLength(0);
        var bytes = new byte[res * res * 2];
        int k = 0;
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                ushort v = (ushort)Mathf.RoundToInt(Mathf.Clamp01(h[z, x]) * 65535f);
                bytes[k++] = (byte)(v & 0xff);
                bytes[k++] = (byte)(v >> 8);
            }
        File.WriteAllBytes(BasePath, bytes);
        AssetDatabase.ImportAsset(BasePath);
    }

    static float[,] LoadBase(int res)
    {
        byte[] bytes = File.ReadAllBytes(BasePath);
        if (bytes.Length != res * res * 2) return null;
        var h = new float[res, res];
        int k = 0;
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++, k += 2)
                h[z, x] = (bytes[k] | (bytes[k + 1] << 8)) / 65535f;
        return h;
    }
}
