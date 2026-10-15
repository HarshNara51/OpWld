using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Tools > Vehicles > Add Car Lights
//
// Adds headlamps, tail lamps and - on traffic cars - amber turn signals to:
//   * the player cars (Prometheus + everything in Assets/Prefabs/PlayerCars)
//   * every traffic model on the Traffic prefab's Car Models list
// Cars that already have Light components of their own are left alone.
//
// Each lamp is a small glowing shape (no Light component) stuck onto the
// car's real body surface: it's placed at the front/rear corners, then
// pushed onto the mesh with Unity's own mesh picking. Player cars also get
// two real headlight beams (only lit on the car you drive, at night).
// Everything lives under a "CarLights" child - nudge any lamp by hand.
// Safe to run again: rebuilds its own lamps.
public static class CarLightsInstaller
{
    private const string MaterialFolder = "Assets/Prefabs/CarLights";
    private const string GroupName = "CarLights";
    private const string TrafficPrefab = "Assets/Prefabs/Traffic.prefab";
    private const string PrometheusPrefab = "Assets/Imports/PROMETEO - Car Controller/Prefabs/Prometheus.prefab";

    private static readonly Color HeadColor = new Color(1f, 0.88f, 0.62f) * 3f;   // warm halogen
    private static readonly Color TailColor = new Color(1f, 0.03f, 0.02f) * 2.2f;
    private static readonly Color SignalColor = new Color(1f, 0.42f, 0f) * 4f;     // amber

    private static readonly MethodInfo IntersectRayMesh = typeof(HandleUtility).GetMethod(
        "IntersectRayMesh", BindingFlags.Static | BindingFlags.NonPublic);

    [MenuItem("Tools/Vehicles/Add Car Lights")]
    private static void AddCarLightsMenu()
    {
        if (EditorUtility.DisplayDialog("Add Car Lights",
                "This REBUILDS the lamps on every player and traffic car - any lamps you moved by hand go back to where the tool puts them.\n\nJust moved headlamps? Use 'Add Road Beams To Traffic' instead.",
                "Rebuild all lamps", "Cancel"))
            AddCarLights();
    }

    public static string AddCarLights()
    {
        Directory.CreateDirectory(MaterialFolder);
        Material head = LampMaterial("CarLight_Head", HeadColor);
        Material tail = LampMaterial("CarLight_Tail", TailColor);
        Material signal = LampMaterial("CarLight_Signal", SignalColor);

        var targets = new List<(string path, CarLights.Kind kind)>();
        if (File.Exists(PrometheusPrefab)) targets.Add((PrometheusPrefab, CarLights.Kind.Player));
        foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/PlayerCars" }))
            targets.Add((AssetDatabase.GUIDToAssetPath(g), CarLights.Kind.Player));
        var spawner = AssetDatabase.LoadAssetAtPath<GameObject>(TrafficPrefab)?.GetComponent<TrafficSpawner>();
        if (spawner != null)
            foreach (TrafficSpawner.CarModel model in spawner.carModels)
                foreach (GameObject colour in model.colours)
                    if (colour != null) targets.Add((AssetDatabase.GetAssetPath(colour), CarLights.Kind.Traffic));

        int done = 0;
        var skipped = new List<string>();
        foreach (var (path, kind) in targets)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Rebuild ours; leave cars that came with their own lights alone
                Transform old = root.transform.Find(GroupName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var oldComp = root.GetComponent<CarLights>();
                if (oldComp != null) Object.DestroyImmediate(oldComp);
                if (root.GetComponentsInChildren<Light>(true).Length > 0) { skipped.Add(root.name + " (has its own lights)"); continue; }

                if (!Build(root, kind, head, tail, signal)) { skipped.Add(root.name + " (no body found)"); continue; }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                done++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        AssetDatabase.SaveAssets();
        string summary = $"Car lights added to {done} car prefabs." + (skipped.Count > 0 ? " Skipped: " + string.Join(", ", skipped) : "");
        Debug.Log(summary);
        return summary;
    }

    // Tools > Vehicles > Add Road Beams To Traffic
    // Two real road beams per traffic car, placed at its headlamps (run it again
    // after moving headlamps by hand and the beams follow). They only switch on
    // at night within Beam Distance of the player - see CarLights.
    [MenuItem("Tools/Vehicles/Add Road Beams To Traffic")]
    public static string AddTrafficBeams()
    {
        var spawner = AssetDatabase.LoadAssetAtPath<GameObject>(TrafficPrefab)?.GetComponent<TrafficSpawner>();
        if (spawner == null) return "Car lights: Traffic prefab not found.";
        int done = 0;
        foreach (TrafficSpawner.CarModel model in spawner.carModels)
            foreach (GameObject colour in model.colours)
            {
                if (colour == null) continue;
                string path = AssetDatabase.GetAssetPath(colour);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var lights = root.GetComponent<CarLights>();
                    Transform group = root.transform.Find(GroupName);
                    if (lights == null || group == null) continue;

                    for (int i = group.childCount - 1; i >= 0; i--)
                        if (group.GetChild(i).name.StartsWith("Beam_")) Object.DestroyImmediate(group.GetChild(i).gameObject);

                    var beams = new List<Light>();
                    foreach (string side in new[] { "Left", "Right" })
                    {
                        Transform lamp = group.Find("Headlamp_" + side);
                        if (lamp == null) continue;
                        beams.Add(AddBeam(group, side, lamp.localPosition, side == "Left" ? -1 : 1));
                    }
                    var so = new SerializedObject(lights);
                    SerializedProperty prop = so.FindProperty("beams");
                    prop.arraySize = beams.Count;
                    for (int i = 0; i < beams.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = beams[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        string summary = $"Road beams added to {done} traffic car prefabs.";
        Debug.Log(summary);
        return summary;
    }

    // A real headlight beam: forward, dipped a little toward the road, no shadows
    private static Light AddBeam(Transform group, string side, Vector3 localPos, int sign)
    {
        var go = new GameObject("Beam_" + side);
        go.transform.SetParent(group, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(6f, sign * 2f, 0f);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = new Color(1f, 0.9f, 0.7f);
        l.range = 40f;
        l.spotAngle = 70f;
        l.innerSpotAngle = 35f;
        l.intensity = 120f;
        l.shadows = LightShadows.None;
        l.enabled = false;
        return l;
    }

    private static bool Build(GameObject root, CarLights.Kind kind, Material head, Material tail, Material signal)
    {
        Transform t = root.transform;
        Matrix4x4 toRoot = t.worldToLocalMatrix;

        // The body, in the car's own space (wheels left out - they'd skew the corners)
        var parts = new List<(Mesh mesh, Matrix4x4 m)>();
        Bounds b = default;
        bool any = false;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
            string n = mf.name.ToLower();
            if (n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre") || n.Contains("rim")) continue;
            Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
            parts.Add((mf.sharedMesh, m));
            Bounds mb = mf.sharedMesh.bounds;
            foreach (Vector3 c in Corners(mb))
            {
                Vector3 p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        if (!any) return false;

        float scale = Mathf.Max(0.0001f, t.lossyScale.x); // lamp sizes are in metres
        float w = b.size.x * scale, h = b.size.y * scale;
        Vector3 Size(float x, float y) => new Vector3(x, y, 0.025f) / scale;

        var group = new GameObject(GroupName);
        group.transform.SetParent(t, false);

        float headY = b.min.y + b.size.y * 0.42f, tailY = b.min.y + b.size.y * 0.47f;
        // Body width right where the lamps go (the overall width includes the mirrors)
        float frontHx = HalfWidthAt(parts, b, headY, true), rearHx = HalfWidthAt(parts, b, tailY, false);
        var heads = new List<Renderer>(); var tails = new List<Renderer>();
        var lefts = new List<Renderer>(); var rights = new List<Renderer>();
        var beams = new List<Light>();

        for (int side = -1; side <= 1; side += 2) // -1 = left, +1 = right (car faces +Z)
        {
            string s = side < 0 ? "Left" : "Right";
            heads.Add(Lamp(group.transform, "Headlamp_" + s, parts, b, new Vector3(side * frontHx * 0.62f, headY, 0f), true, Size(w * 0.11f, h * 0.055f), head));
            tails.Add(Lamp(group.transform, "TailLamp_" + s, parts, b, new Vector3(side * rearHx * 0.66f, tailY, 0f), false, Size(w * 0.10f, h * 0.045f), tail));

            if (kind == CarLights.Kind.Traffic)
            {
                var list = side < 0 ? lefts : rights;
                list.Add(Lamp(group.transform, "Signal_Front_" + s, parts, b, new Vector3(side * frontHx * 0.86f, headY, 0f), true, Size(w * 0.045f, h * 0.04f), signal));
                list.Add(Lamp(group.transform, "Signal_Rear_" + s, parts, b, new Vector3(side * rearHx * 0.86f, tailY, 0f), false, Size(w * 0.045f, h * 0.04f), signal));
            }
            else
            {
                // Real beam from each headlamp: forward, dipped a little toward the road
                var beamGo = new GameObject("Beam_" + s);
                beamGo.transform.SetParent(heads[heads.Count - 1].transform.parent, false);
                beamGo.transform.localPosition = heads[heads.Count - 1].transform.localPosition;
                beamGo.transform.localRotation = Quaternion.Euler(6f, side * 2f, 0f);
                beamGo.transform.localScale = Vector3.one / scale; // undo the car's scale so range/angle stay in metres
                Light l = beamGo.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = new Color(1f, 0.9f, 0.7f);
                l.range = 40f;
                l.spotAngle = 70f;
                l.innerSpotAngle = 35f;
                l.intensity = 120f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                beams.Add(l);
            }
        }

        var lights = root.AddComponent<CarLights>();
        var so = new SerializedObject(lights);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        Fill(so.FindProperty("headlamps"), heads);
        Fill(so.FindProperty("tailLamps"), tails);
        Fill(so.FindProperty("leftSignals"), lefts);
        Fill(so.FindProperty("rightSignals"), rights);
        SerializedProperty beamProp = so.FindProperty("beams");
        beamProp.arraySize = beams.Count;
        for (int i = 0; i < beams.Count; i++) beamProp.GetArrayElementAtIndex(i).objectReferenceValue = beams[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // A flat glowing lamp, stuck onto the body: shoot a ray at the car from the front (or back)
    private static Renderer Lamp(Transform parent, string name, List<(Mesh mesh, Matrix4x4 m)> parts, Bounds b,
                                 Vector3 at, bool front, Vector3 size, Material mat)
    {
        Vector3 dir = front ? Vector3.back : Vector3.forward; // ray heads INTO the car
        Vector3 start = new Vector3(at.x, at.y, front ? b.max.z + 1f : b.min.z - 1f);
        Vector3 point = new Vector3(at.x, at.y, front ? b.max.z : b.min.z);
        Vector3 normal = -dir;

        if (IntersectRayMesh != null)
        {
            var ray = new Ray(start, dir);
            float best = float.MaxValue;
            var args = new object[4];
            foreach (var (mesh, m) in parts)
            {
                args[0] = ray; args[1] = mesh; args[2] = m; args[3] = null;
                if (!(bool)IntersectRayMesh.Invoke(null, args)) continue;
                var hit = (RaycastHit)args[3];
                if (hit.distance < best) { best = hit.distance; point = hit.point; normal = hit.normal.normalized; }
            }
        }
        // Lamps face straight out (not along a sloped bonnet), slightly proud of the surface
        if (Vector3.Dot(normal, -dir) < 0.3f) normal = -dir;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = point + normal * 0.012f;
        go.transform.localRotation = Quaternion.LookRotation(normal, Vector3.up);
        go.transform.localScale = size;
        go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        r.enabled = false; // CarLights switches them on
        return r;
    }

    // Widest point of the body in a band around this height, near the front (or rear) end
    private static float HalfWidthAt(List<(Mesh mesh, Matrix4x4 m)> parts, Bounds b, float y, bool front)
    {
        float band = b.size.y * 0.12f, endZone = b.size.z * 0.18f, half = 0f;
        foreach (var (mesh, m) in parts)
            foreach (Vector3 v in mesh.vertices)
            {
                Vector3 p = m.MultiplyPoint3x4(v);
                if (Mathf.Abs(p.y - y) > band) continue;
                if (front ? p.z < b.max.z - endZone : p.z > b.min.z + endZone) continue;
                half = Mathf.Max(half, Mathf.Abs(p.x - b.center.x));
            }
        return half > 0.05f * b.size.x ? half : b.extents.x * 0.9f;
    }

    private static void Fill(SerializedProperty prop, List<Renderer> list)
    {
        prop.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
    }

    private static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
    }

    private static Material LampMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = mat == null;
        if (isNew) mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        mat.enableInstancing = true;
        if (isNew) AssetDatabase.CreateAsset(mat, path);
        else EditorUtility.SetDirty(mat);
        return mat;
    }
}
