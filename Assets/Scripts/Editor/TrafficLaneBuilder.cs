using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

// Tools > Traffic > Build Lanes From Roads
//
// Reads the road splines (which are separate pieces, not connected) and
// works out the road network itself: a road end close to another road is a
// junction, a road end near nothing is a dead end. Then it makes lane
// routes for the TrafficSpawner:
//   - right-hand traffic, lane = laneOffset metres right of the centre line
//   - the closed loop (roundabout) is one-way, counter-clockwise
//   - every route runs from one spawn point (dead end at the map edge) to
//     another, with rounded corners, and together they cover every road
//     reachable from the spawn points
// Old routes under Traffic/Routes are replaced. Spawn points: the empties
// under Traffic/SpawnPoints. If there are none, one is made at every dead
// end on the left and bottom edges of the map - move/add/delete them and
// build again to change where cars come and go.
public static class TrafficLaneBuilder
{
    private const float SampleStep = 1f;
    private const float NodeMerge = 7f;   // junction points closer than this on one road are the same junction
    private const float SpawnSnap = 30f;  // spawn point -> dead end
    private const float EdgeBand = 45f;   // "map edge" for auto spawn points

    private class Road
    {
        public string name;
        public List<Vector3> pts;
        public bool closed;
        public List<(int index, int node)> cuts = new List<(int, int)>();
    }

    private class Node
    {
        public Vector3 pos;
        public int degree;
    }

    private class Lane
    {
        public int from, to;
        public List<Vector3> pts;
        public int twin = -1;
        public float length;
        public string road;
    }

    [MenuItem("Tools/Traffic/Build Lanes From Roads")]
    private static void Build()
    {
        TrafficSpawner spawner = Object.FindFirstObjectByType<TrafficSpawner>();
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("Traffic", "Add a TrafficSpawner to the 'Traffic' object first.", "OK");
            return;
        }
        Transform roadsRoot = spawner.roadsRoot != null ? spawner.roadsRoot : GameObject.Find("Roads")?.transform;
        if (roadsRoot == null)
        {
            EditorUtility.DisplayDialog("Traffic", "Couldn't find the 'Roads' object - set Roads Root on the TrafficSpawner.", "OK");
            return;
        }

        // ---------- 1. roads as point lists ----------
        var roads = new List<Road>();
        foreach (SplineContainer sc in roadsRoot.GetComponentsInChildren<SplineContainer>())
        {
            for (int s = 0; s < sc.Splines.Count; s++)
            {
                Spline spline = sc.Splines[s];
                float len = spline.GetLength() * sc.transform.lossyScale.x;
                int n = Mathf.Max(2, Mathf.CeilToInt(len / SampleStep));
                var pts = new List<Vector3>();
                for (int i = 0; i <= n; i++)
                {
                    if (spline.Closed && i == n) break;
                    pts.Add(sc.transform.TransformPoint((Vector3)spline.EvaluatePosition((float)i / n)));
                }
                roads.Add(new Road { name = sc.name, pts = pts, closed = spline.Closed });
            }
        }

        // ---------- 2. junctions ----------
        var nodes = new List<Node>();
        int NewNode(Vector3 p) { nodes.Add(new Node { pos = p }); return nodes.Count - 1; }

        // Cut point on a road, reusing a nearby cut (two roads meeting one road at almost the same spot)
        int CutAt(Road r, int index)
        {
            foreach (var c in r.cuts)
                if (Vector3.Distance(r.pts[c.index], r.pts[index]) < NodeMerge) return c.node;
            int node = NewNode(r.pts[index]);
            r.cuts.Add((index, node));
            return node;
        }

        foreach (Road r in roads)
        {
            if (r.closed) continue;
            foreach (int end in new[] { 0, r.pts.Count - 1 })
            {
                if (r.cuts.Any(c => c.index == end)) continue; // already joined
                Vector3 p = r.pts[end];

                // Nearest point on any other road
                Road best = null;
                int bestIndex = -1;
                float bestDist = spawner.joinDistance;
                foreach (Road o in roads)
                {
                    if (o == r) continue;
                    for (int i = 0; i < o.pts.Count; i++)
                    {
                        float d = Flat(o.pts[i] - p).magnitude;
                        if (d < bestDist) { bestDist = d; best = o; bestIndex = i; }
                    }
                }

                int node;
                if (best == null)
                {
                    node = NewNode(p); // dead end
                }
                else
                {
                    // Close to the other road's end? Then join end to end
                    if (!best.closed)
                    {
                        int last = best.pts.Count - 1;
                        if (Flat(best.pts[0] - best.pts[bestIndex]).magnitude < spawner.joinDistance) bestIndex = 0;
                        else if (Flat(best.pts[last] - best.pts[bestIndex]).magnitude < spawner.joinDistance) bestIndex = last;
                    }
                    node = CutAt(best, bestIndex);
                }
                r.cuts.Add((end, node));
            }
        }

        // ---------- 3. road pieces between junctions -> lanes ----------
        var lanes = new List<Lane>();
        foreach (Road r in roads)
        {
            var cuts = r.cuts.OrderBy(c => c.index).ToList();
            if (cuts.Count < 2 && !r.closed) continue;
            if (cuts.Count == 0) continue;

            bool forwardOk = true, backwardOk = true;
            if (r.closed)
            {
                // Roundabout: counter-clockwise seen from above only
                bool ccw = SignedArea(r.pts) > 0f;
                forwardOk = ccw;
                backwardOk = !ccw;
            }

            int pieces = r.closed ? cuts.Count : cuts.Count - 1;
            for (int k = 0; k < pieces; k++)
            {
                var a = cuts[k];
                var b = cuts[(k + 1) % cuts.Count];
                var piece = new List<Vector3> { nodes[a.node].pos };
                int i = a.index;
                int guard = 0;
                while (i != b.index && guard++ < 100000)
                {
                    i = r.closed ? (i + 1) % r.pts.Count : i + 1;
                    piece.Add(r.pts[i]);
                }
                piece[piece.Count - 1] = nodes[b.node].pos;
                if (a.node == b.node || PathLength(piece) < 1f) continue;

                int fwd = -1, bwd = -1;
                if (forwardOk) { fwd = lanes.Count; lanes.Add(MakeLane(a.node, b.node, piece, r.name)); }
                if (backwardOk)
                {
                    var rev = new List<Vector3>(piece);
                    rev.Reverse();
                    bwd = lanes.Count;
                    lanes.Add(MakeLane(b.node, a.node, rev, r.name));
                }
                if (fwd >= 0 && bwd >= 0) { lanes[fwd].twin = bwd; lanes[bwd].twin = fwd; }
                nodes[a.node].degree++;
                nodes[b.node].degree++;
            }
        }

        // ---------- 4. spawn points ----------
        Transform traffic = spawner.transform;
        Transform spawnRoot = spawner.spawnPointsRoot != null ? spawner.spawnPointsRoot : traffic.Find("SpawnPoints");
        if (spawnRoot == null)
        {
            spawnRoot = new GameObject("SpawnPoints").transform;
            Undo.RegisterCreatedObjectUndo(spawnRoot.gameObject, "Build traffic lanes");
            spawnRoot.SetParent(traffic, false);
        }
        Undo.RecordObject(spawner, "Build traffic lanes");
        spawner.spawnPointsRoot = spawnRoot;
        spawner.roadsRoot = roadsRoot;

        var deadEnds = Enumerable.Range(0, nodes.Count).Where(n => nodes[n].degree == 1).ToList();
        if (spawnRoot.childCount == 0)
        {
            float minX = roads.SelectMany(r => r.pts).Min(p => p.x);
            float minZ = roads.SelectMany(r => r.pts).Min(p => p.z);
            int count = 0;
            foreach (int n in deadEnds)
            {
                Vector3 p = nodes[n].pos;
                if (p.x > minX + EdgeBand && p.z > minZ + EdgeBand) continue;
                var go = new GameObject($"Spawn_{++count:00}");
                Undo.RegisterCreatedObjectUndo(go, "Build traffic lanes");
                go.transform.SetParent(spawnRoot, false);
                go.transform.position = p;
            }
        }

        var gates = new HashSet<int>();
        foreach (Transform t in spawnRoot)
        {
            int best = -1;
            float bestDist = SpawnSnap;
            foreach (int n in deadEnds)
            {
                float d = Flat(nodes[n].pos - t.position).magnitude;
                if (d < bestDist) { bestDist = d; best = n; }
            }
            if (best >= 0) gates.Add(best);
            else Debug.LogWarning($"Traffic: spawn point '{t.name}' isn't near a road end - ignored.", t);
        }
        if (gates.Count < 2)
        {
            EditorUtility.DisplayDialog("Traffic", "Need at least 2 spawn points at road ends (under Traffic/SpawnPoints).", "OK");
            return;
        }

        // ---------- 5. routes: gate -> gate, covering every lane ----------
        var outgoing = new List<int>[nodes.Count];
        for (int n = 0; n < nodes.Count; n++) outgoing[n] = new List<int>();
        for (int l = 0; l < lanes.Count; l++) outgoing[lanes[l].from].Add(l);

        var startLanes = lanes.Select((l, i) => (l, i)).Where(x => gates.Contains(x.l.from)).Select(x => x.i).ToList();
        bool IsExit(int lane) => gates.Contains(lanes[lane].to);

        var routes = new List<List<int>>();
        var seen = new HashSet<string>();
        void AddRoute(List<int> r)
        {
            if (r == null || r.Count == 0) return;
            if (!IsExit(r[r.Count - 1]) || !gates.Contains(lanes[r[0]].from)) return;
            if (lanes[r[0]].from == lanes[r[r.Count - 1]].to) return; // straight back out where it came in
            if (seen.Add(string.Join(",", r))) routes.Add(r);
        }

        // Every gate to every other gate
        foreach (int s in startLanes)
            foreach (int e in Enumerable.Range(0, lanes.Count).Where(IsExit))
                if (lanes[s].from != lanes[e].to)
                    AddRoute(ShortestPath(lanes, outgoing, new[] { s }, x => x == e));

        // Then make sure every lane gets used by at least one route
        var covered = new HashSet<int>(routes.SelectMany(r => r));
        for (int l = 0; l < lanes.Count; l++)
        {
            if (covered.Contains(l)) continue;
            List<int> toIt = ShortestPath(lanes, outgoing, startLanes, x => x == l);
            List<int> fromIt = ShortestPath(lanes, outgoing, new[] { l }, x => IsExit(x) && lanes[x].to != lanes[toIt?[0] ?? l].from);
            if (toIt == null || fromIt == null) continue;
            var r = new List<int>(toIt);
            r.AddRange(fromIt.Skip(1));
            AddRoute(r);
            foreach (int x in r) covered.Add(x);
        }

        // ---------- 6. route splines ----------
        Transform routesRoot = spawner.routesRoot != null ? spawner.routesRoot : traffic.Find("Routes");
        if (routesRoot == null)
        {
            routesRoot = new GameObject("Routes").transform;
            Undo.RegisterCreatedObjectUndo(routesRoot.gameObject, "Build traffic lanes");
            routesRoot.SetParent(traffic, false);
        }
        spawner.routesRoot = routesRoot;
        for (int i = routesRoot.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(routesRoot.GetChild(i).gameObject);

        var gateNames = new Dictionary<int, string>();
        foreach (Transform t in spawnRoot)
        {
            int best = gates.OrderBy(g => Flat(nodes[g].pos - t.position).magnitude).First();
            if (!gateNames.ContainsKey(best)) gateNames[best] = t.name;
        }

        int made = 0;
        foreach (List<int> route in routes)
        {
            List<Vector3> centre = new List<Vector3>();
            foreach (int l in route)
                foreach (Vector3 p in lanes[l].pts)
                    if (centre.Count == 0 || Flat(centre[centre.Count - 1] - p).magnitude > 0.3f) centre.Add(p);

            List<Vector3> lane = LaneLine(centre, spawner.laneOffset, spawner.cornerRounding);
            if (lane.Count < 2) continue;

            var go = new GameObject($"Route_{++made:00} ({Name(gateNames, lanes[route[0]].from)} > {Name(gateNames, lanes[route[route.Count - 1]].to)})");
            Undo.RegisterCreatedObjectUndo(go, "Build traffic lanes");
            go.transform.SetParent(routesRoot, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var container = go.AddComponent<SplineContainer>();

            // A knot every ~4 m is plenty after the rounding
            var knots = new List<float3>();
            float acc = 0f;
            knots.Add(go.transform.InverseTransformPoint(lane[0]));
            for (int i = 1; i < lane.Count - 1; i++)
            {
                acc += Vector3.Distance(lane[i - 1], lane[i]);
                if (acc >= 4f) { knots.Add(go.transform.InverseTransformPoint(lane[i])); acc = 0f; }
            }
            knots.Add(go.transform.InverseTransformPoint(lane[lane.Count - 1]));
            container.Spline = new Spline(knots, TangentMode.AutoSmooth, false);
        }

        int unused = lanes.Count - covered.Count;
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        Debug.Log($"Traffic: {roads.Count} roads -> {nodes.Count} junctions/ends, {lanes.Count} lanes, {gates.Count} spawn points, {made} routes." +
                  (unused > 0 ? $" {unused} lanes can't be reached from the spawn points (dead-end roads) and get no traffic." : ""));
    }

    // Tools > Traffic > Fill Car Models From "Trf_Crs"
    // One Car Model per car placed under Trf_Crs, at the scale it has there.
    // Colours = the other prefabs next to it named the same apart from the
    // colour ("gz21_ blue" -> every "gz21_*" in that folder).
    [MenuItem("Tools/Traffic/Fill Car Models From \"Trf_Crs\"")]
    private static void FillCarModels()
    {
        TrafficSpawner spawner = Object.FindFirstObjectByType<TrafficSpawner>();
        GameObject group = GameObject.Find("Trf_Crs");
        if (spawner == null || group == null)
        {
            EditorUtility.DisplayDialog("Traffic", "Needs a TrafficSpawner and a 'Trf_Crs' object with the traffic cars under it.", "OK");
            return;
        }

        var models = new List<TrafficSpawner.CarModel>();
        foreach (Transform car in group.transform)
        {
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(car.gameObject);
            if (prefab == null)
            {
                Debug.LogWarning($"Traffic: '{car.name}' isn't a prefab instance - skipped.", car);
                continue;
            }
            string path = AssetDatabase.GetAssetPath(prefab);
            string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string family = Family(prefab.name);

            var colours = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetDirectoryName(p).Replace('\\', '/') == folder)
                .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p))
                .Where(g => g != null && Family(g.name) == family)
                .OrderBy(g => g.name)
                .ToArray();
            if (colours.Length == 0) colours = new[] { prefab };

            float baseScale = Mathf.Abs(prefab.transform.localScale.x) > 1e-4f ? prefab.transform.localScale.x : 1f;
            models.Add(new TrafficSpawner.CarModel
            {
                name = family,
                colours = colours,
                scale = car.lossyScale.x / baseScale,
                weight = 1f,
            });
        }

        Undo.RecordObject(spawner, "Fill traffic car models");
        spawner.carModels = models.ToArray();
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        Debug.Log("Traffic: car models - " + string.Join(", ", models.Select(m => $"{m.name} ({m.colours.Length} colours, x{m.scale:0.##})")));
    }

    // "gz21_ blue" -> "gz21", "car_6_black" -> "car_6", "Jeep2" -> "Jeep2"
    private static string Family(string prefabName)
    {
        int cut = prefabName.LastIndexOf('_');
        return (cut > 0 ? prefabName.Substring(0, cut) : prefabName).Trim();
    }

    private static string Name(Dictionary<int, string> names, int node) => names.TryGetValue(node, out string n) ? n : "?";

    private static Lane MakeLane(int from, int to, List<Vector3> pts, string road)
    {
        return new Lane { from = from, to = to, pts = pts, length = PathLength(pts), road = road };
    }

    // Dijkstra over lanes (a lane can follow any lane that ends where it starts, except turning back the same road)
    private static List<int> ShortestPath(List<Lane> lanes, List<int>[] outgoing, IEnumerable<int> starts, System.Func<int, bool> isGoal)
    {
        var dist = new Dictionary<int, float>();
        var prev = new Dictionary<int, int>();
        var open = new SortedSet<(float, int)>();
        foreach (int s in starts)
        {
            dist[s] = lanes[s].length;
            prev[s] = -1;
            open.Add((lanes[s].length, s));
        }
        while (open.Count > 0)
        {
            var (d, l) = open.Min;
            open.Remove(open.Min);
            if (d > dist[l]) continue;
            if (isGoal(l))
            {
                var path = new List<int>();
                for (int x = l; x >= 0; x = prev[x]) path.Add(x);
                path.Reverse();
                return path;
            }
            foreach (int next in outgoing[lanes[l].to])
            {
                if (next == lanes[l].twin) continue; // no U-turns
                float nd = d + lanes[next].length;
                if (dist.TryGetValue(next, out float old) && old <= nd) continue;
                dist[next] = nd;
                prev[next] = l;
                open.Add((nd, next));
            }
        }
        return null;
    }

    // Centre line -> rounded corners -> shifted to the right-hand lane
    private static List<Vector3> LaneLine(List<Vector3> centre, float offset, float rounding)
    {
        List<Vector3> line = Resample(centre, 1f);
        int w = Mathf.Max(1, Mathf.RoundToInt(rounding));
        line = Smooth(line, w);
        line = Smooth(line, w);

        var shifted = new List<Vector3>(line.Count);
        for (int i = 0; i < line.Count; i++)
        {
            Vector3 dir = Flat(line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 right = new Vector3(dir.z, 0f, -dir.x);
            shifted.Add(line[i] + right * offset);
        }

        // Inside of tight corners: drop points that would make the lane go backwards
        var clean = new List<Vector3> { shifted[0] };
        for (int i = 1; i < shifted.Count; i++)
        {
            Vector3 along = Flat(line[Mathf.Min(i + 1, line.Count - 1)] - line[i - 1]);
            if (Vector3.Dot(Flat(shifted[i] - clean[clean.Count - 1]), along) > 0.05f) clean.Add(shifted[i]);
        }
        return Resample(Smooth(clean, 2), 1f);
    }

    private static List<Vector3> Smooth(List<Vector3> pts, int w)
    {
        var result = new List<Vector3>(pts.Count);
        for (int i = 0; i < pts.Count; i++)
        {
            int r = Mathf.Min(w, Mathf.Min(i, pts.Count - 1 - i)); // shrink near the ends so they stay put
            Vector3 sum = Vector3.zero;
            for (int k = -r; k <= r; k++) sum += pts[i + k];
            result.Add(sum / (2 * r + 1));
        }
        return result;
    }

    private static List<Vector3> Resample(List<Vector3> pts, float step)
    {
        var result = new List<Vector3> { pts[0] };
        float carry = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            Vector3 a = pts[i - 1], b = pts[i];
            float seg = Vector3.Distance(a, b);
            float t = step - carry;
            while (t <= seg)
            {
                result.Add(Vector3.Lerp(a, b, t / seg));
                t += step;
            }
            carry = seg - (t - step);
        }
        if (Vector3.Distance(result[result.Count - 1], pts[pts.Count - 1]) > 0.2f) result.Add(pts[pts.Count - 1]);
        return result;
    }

    private static float PathLength(List<Vector3> pts)
    {
        float len = 0f;
        for (int i = 1; i < pts.Count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
        return len;
    }

    private static float SignedArea(List<Vector3> pts)
    {
        float a = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 p = pts[i], q = pts[(i + 1) % pts.Count];
            a += p.x * q.z - q.x * p.z;
        }
        return a * 0.5f;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
