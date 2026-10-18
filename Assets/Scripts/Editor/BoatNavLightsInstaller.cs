using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Tools > Boats > Add Lights + Rocking
//
// Scout_Boat prefab: a blinking white light on the bow, red (port / left)
// and green (starboard / right) on the two ends of the black crossbar on
// the cabin roof, plus BoatRocking for a gentle idle sway. Edits the prefab
// itself, so every scene's Scout_Boat picks it up.
//
// Same cheap glowing unlit balls as the aircraft nav lights (and the same
// materials), blinked by AircraftNavLights. Unlike the aircraft they are
// on day and night, so the boat is easy to spot. Safe to run again.
public static class BoatNavLightsInstaller
{
    private const string BoatPrefab = "Assets/Alstra Infinite/Boats LowPoly/Prefabs/Scout_Boat.prefab";
    private const string MaterialFolder = "Assets/Imports/Aircraft/NavLights";
    private const string LampPrefix = "NavLight_";

    // Scout_Boat's own mesh space: bow points to -X, so port (left) is -Z.
    // Crossbar top is y 2.60, ends at z +-0.72, centred on x 0.46; bow tip is (-4.1, 1.21, 0).
    private const float LampSize = 0.15f; // ~0.6 m with the scenes' x4 scale
    private static readonly Vector3 PortLamp = new Vector3(0.46f, 2.60f + LampSize * 0.45f, -0.68f);
    private static readonly Vector3 StarboardLamp = new Vector3(0.46f, 2.60f + LampSize * 0.45f, 0.68f);
    private static readonly Vector3 BowLamp = new Vector3(-4.1f - LampSize * 0.3f, 1.21f + LampSize * 0.45f, 0f);

    [MenuItem("Tools/Boats/Add Lights + Rocking")]
    public static string AddLights()
    {
        Material red = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/NavLight_Red.mat");
        Material green = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/NavLight_Green.mat");
        Material white = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/NavLight_White.mat");
        if (red == null || green == null || white == null)
        {
            AircraftNavLightsInstaller.AddNavLights(); // creates the shared lamp materials
            red = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/NavLight_Red.mat");
            green = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/NavLight_Green.mat");
            white = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/NavLight_White.mat");
        }
        Mesh ball = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

        GameObject root = PrefabUtility.LoadPrefabContents(BoatPrefab);
        try
        {
            // Old lights out
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child != null && child != root.transform && child.name.StartsWith(LampPrefix)) Object.DestroyImmediate(child.gameObject);

            var lamps = new List<Renderer>
            {
                AddLamp(root.transform, LampPrefix + "Port_Red", PortLamp, LampSize, ball, red),
                AddLamp(root.transform, LampPrefix + "Starboard_Green", StarboardLamp, LampSize, ball, green),
                AddLamp(root.transform, LampPrefix + "Bow_White", BowLamp, LampSize * 0.9f, ball, white),
            };

            var blink = root.GetComponent<AircraftNavLights>();
            if (blink == null) blink = root.AddComponent<AircraftNavLights>();
            var so = new SerializedObject(blink);
            SerializedProperty list = so.FindProperty("lamps");
            list.arraySize = lamps.Count;
            for (int i = 0; i < lamps.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
            // Slow marine blink: on more than off, so the boat stays easy to see
            so.FindProperty("cycle").floatValue = 1.6f;
            so.FindProperty("flash").floatValue = 0.9f;
            so.FindProperty("doubleFlash").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (root.GetComponent<BoatRocking>() == null) root.AddComponent<BoatRocking>();

            PrefabUtility.SaveAsPrefabAsset(root, BoatPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        AssetDatabase.SaveAssets();
        const string summary = "Scout_Boat: bow white, port red, starboard green blinking lights + rocking added to the prefab.";
        Debug.Log(summary);
        return summary;
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
}
