using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Tools > Aircraft > Add Navigation Lights
//
// Puts a blinking red light on the LEFT wingtip, a green one on the
// RIGHT wingtip and a warm white one on the tail of every aircraft prefab
// in Assets/Imports/Aircraft (helicopters: sides of the body, end of the
// tail boom). They only light up in Night scenes (NightOnly + SceneTimeOfDay). Left/right are worked out
// from each model's AircraftFlight "Model Rotation Fix", so they're the
// pilot's left/right in flight. The tips are found from the model's own
// mesh (rotors/propellers/wheels ignored).
//
// The lights are small glowing unlit balls - NOT Light components - so
// they cost next to nothing. Safe to run again: replaces the old ones.
public static class AircraftNavLightsInstaller
{
    private const string AircraftFolder = "Assets/Imports/Aircraft";
    private const string MaterialFolder = AircraftFolder + "/NavLights";
    private const string LampPrefix = "NavLight_";

    // HDR colours (above 1 = glows with bloom)
    private static readonly Color Red = new Color(1f, 0.05f, 0.03f) * 6f;
    private static readonly Color Green = new Color(0f, 1f, 0.06f) * 3.5f; // less boost than red, or it washes out to cyan
    private static readonly Color White = new Color(1f, 0.88f, 0.62f) * 4f;  // warm halogen, like car headlamps

    [MenuItem("Tools/Aircraft/Add Navigation Lights")]
    public static string AddNavLights()
    {
        Directory.CreateDirectory(MaterialFolder);
        Material red = LampMaterial("NavLight_Red", Red);
        Material green = LampMaterial("NavLight_Green", Green);
        Material white = LampMaterial("NavLight_White", White);
        Mesh ball = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

        var log = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { AircraftFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var flight = root.GetComponent<AircraftFlight>();
                var body = root.GetComponent<MeshFilter>();
                if (flight == null || body == null || body.sharedMesh == null) continue; // e.g. the AirTraffic spawner

                // Old lights out
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child != null && child != root.transform && child.name.StartsWith(LampPrefix)) Object.DestroyImmediate(child.gameObject);

                // The pilot's left, in the model's own space
                Quaternion fix = Quaternion.Euler(flight.modelRotationFix);
                Vector3 left = Quaternion.Inverse(fix) * Vector3.left;
                Vector3 back = Quaternion.Inverse(fix) * Vector3.back;

                Vector3[] v = body.sharedMesh.vertices;
                Vector3 leftTip = Tip(v, left), rightTip = Tip(v, -left), tail = Tip(v, back);
                float span = Vector3.Distance(leftTip, rightTip) * root.transform.lossyScale.x; // metres
                bool helicopter = root.name.ToLower().Contains("heli");

                // Big enough to read from the ground at cruising height, tiny next to the aircraft
                float worldSize = Mathf.Clamp(span * (helicopter ? 0.12f : 0.035f), 0.35f, 1.1f);
                float localSize = worldSize / root.transform.lossyScale.x;

                var lamps = new List<Renderer>
                {
                    AddLamp(root.transform, LampPrefix + "Left_Red", leftTip + left * localSize * 0.5f, localSize, ball, red),
                    AddLamp(root.transform, LampPrefix + "Right_Green", rightTip - left * localSize * 0.5f, localSize, ball, green),
                    AddLamp(root.transform, LampPrefix + "Tail_White", tail + back * localSize * 0.4f, localSize * 0.85f, ball, white),
                };

                var blink = root.GetComponent<AircraftNavLights>();
                if (blink == null) blink = root.AddComponent<AircraftNavLights>();
                var so = new SerializedObject(blink);
                SerializedProperty list = so.FindProperty("lamps");
                list.arraySize = lamps.Count;
                for (int i = 0; i < lamps.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
                so.ApplyModifiedPropertiesWithoutUndo();

                // Only at night (when the scene's SceneTimeOfDay says so)
                var night = root.GetComponent<NightOnly>();
                if (night == null) night = root.AddComponent<NightOnly>();
                var nso = new SerializedObject(night);
                SerializedProperty comps = nso.FindProperty("components");
                comps.arraySize = 1;
                comps.GetArrayElementAtIndex(0).objectReferenceValue = blink;
                SerializedProperty rends = nso.FindProperty("renderers");
                rends.arraySize = lamps.Count;
                for (int i = 0; i < lamps.Count; i++) rends.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
                nso.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.Add($"{root.name} (span {span:F0} m, dot {worldSize:F2} m)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        AssetDatabase.SaveAssets();
        string summary = $"Nav lights added to {log.Count} aircraft: {string.Join(", ", log)}";
        Debug.Log(summary);
        return summary;
    }

    // The middle of the outermost edge in a direction (averages the last 2% so it sits on the tip, not a stray corner)
    private static Vector3 Tip(Vector3[] vertices, Vector3 dir)
    {
        float max = float.MinValue, min = float.MaxValue;
        foreach (Vector3 p in vertices) { float d = Vector3.Dot(p, dir); max = Mathf.Max(max, d); min = Mathf.Min(min, d); }
        float cut = max - (max - min) * 0.02f;
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (Vector3 p in vertices)
            if (Vector3.Dot(p, dir) >= cut) { sum += p; count++; }
        return count > 0 ? sum / count : dir * max;
    }

    private static Renderer AddLamp(Transform parent, string name, Vector3 localPos, float localSize, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * localSize;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        return r;
    }

    // URP Unlit, bright HDR colour, GPU instancing on (all the red dots draw together)
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
