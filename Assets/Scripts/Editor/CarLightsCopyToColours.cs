using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools > Vehicles > Copy Car Lights To Colour Variants
//
// Fix the lamps on ONE colour of a traffic car (e.g. gz21_ blue), select
// that prefab in the Project window (several at once is fine - one per
// model), run this. Every other colour of the same model on the Traffic
// prefab's Car Models list gets the same lamp positions, rotations and
// sizes. Only the lamps under "CarLights" are touched.
public static class CarLightsCopyToColours
{
    private const string TrafficPrefab = "Assets/Prefabs/Traffic.prefab";

    [MenuItem("Tools/Vehicles/Copy Car Lights To Colour Variants")]
    public static string CopySelected()
    {
        var spawner = AssetDatabase.LoadAssetAtPath<GameObject>(TrafficPrefab)?.GetComponent<TrafficSpawner>();
        if (spawner == null) return "Car lights: Traffic prefab not found.";

        var log = new List<string>();
        foreach (Object o in Selection.objects)
        {
            var source = o as GameObject;
            if (source == null || PrefabUtility.GetPrefabAssetType(source) == PrefabAssetType.NotAPrefab) continue;
            Transform srcGroup = source.transform.Find("CarLights");
            if (srcGroup == null) { log.Add($"{source.name}: has no CarLights"); continue; }

            // Which model is it?
            TrafficSpawner.CarModel model = null;
            foreach (TrafficSpawner.CarModel m in spawner.carModels)
                foreach (GameObject c in m.colours)
                    if (c == source) model = m;
            if (model == null) { log.Add($"{source.name}: not on the Traffic list"); continue; }

            int copied = 0;
            foreach (GameObject colour in model.colours)
            {
                if (colour == null || colour == source) continue;
                string path = AssetDatabase.GetAssetPath(colour);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform group = root.transform.Find("CarLights");
                    if (group == null) continue;
                    foreach (Transform src in srcGroup)
                    {
                        Transform dst = group.Find(src.name);
                        if (dst == null) continue;
                        dst.localPosition = src.localPosition;
                        dst.localRotation = src.localRotation;
                        dst.localScale = src.localScale;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    copied++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            log.Add($"{model.name}: {source.name} -> {copied} other colours");
        }

        string summary = log.Count == 0
            ? "Car lights: select the fixed car prefab(s) in the Project window first."
            : "Car lights copied - " + string.Join("; ", log);
        Debug.Log(summary);
        return summary;
    }

    [MenuItem("Tools/Vehicles/Copy Car Lights To Colour Variants", true)]
    private static bool Validate() => Selection.activeGameObject != null;
}
