using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Gizmos > Install Gizmo VFX (this scene)
//
// Gives every gizmo (mission entrances, return home, pickups, doors, clues,
// zones...) a VFX marker and removes the old placeholder spheres/cubes/
// capsules. Gizmos of the same kind share one marker prefab in
// Assets/Prefabs/GizmoVFX - change a prefab there (swap the effect, scale,
// colour) and every gizmo of that kind in every scene follows.
// Safe to run again: old markers are replaced.
public static class GizmoVFXInstaller
{
    private const string Folder = "Assets/Prefabs/GizmoVFX";
    private const string Hovl = "Assets/Hovl Studio/Magic effects pack/Prefabs/";
    private const string Gab = "Assets/Imports/VFX/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/";

    private enum Place { Ground, Centre }

    private class Kind
    {
        public string name;
        public string[] scripts;                         // gizmo scripts that get this marker
        public string[] names = new string[0];           // ...or objects whose name starts with one of these (gizmos with no script)
        public (string prefab, float scale, float y)[] parts;
        public Place place;
        public bool fitToZone;                           // scale to the trigger's size
    }

    private static readonly Kind[] Kinds =
    {
        new Kind { name = "Mission", scripts = new[] { "MissionEntryPoint" }, place = Place.Ground,
                   parts = new[] { (Gab + "vfx_LootDrop_02.prefab", 2.5f, 0f), (Hovl + "Character auras/Buff.prefab", 1f, 0f) } },
        new Kind { name = "Destination", scripts = new[] { "ReturnHomeTrigger", "ReachZone", "CallCopsTrigger" }, place = Place.Ground,
                   parts = new[] { (Gab + "vfx_LootDrop_01.prefab", 2f, 0f), (Hovl + "Magic circles/Magic circle.prefab", 1f, 0.05f) } },
        new Kind { name = "Pickup", scripts = new[] { "CargoPickup", "ItemPickup" }, place = Place.Ground,
                   parts = new[] { (Gab + "vfx_LootDrop_02.prefab", 2f, 0f) } },
        new Kind { name = "Door", scripts = new[] { "TeleportPoint" }, place = Place.Centre,
                   parts = new[] { (Gab + "vfx_Portal_01.prefab", 1f, 0f) } },
        new Kind { name = "Clue", scripts = new[] { "ClueDiscovery", "BodyDiscoveryZone", "PhotoCaptureZone" }, place = Place.Centre,
                   parts = new[] { (Hovl + "Environment/Crystal effect green.prefab", 0.8f, 0f) } },
        new Kind { name = "Zone", scripts = new[] { "DeliveryZone", "PoliceDeliveryZone", "InstructionZone" }, place = Place.Ground, fitToZone = true,
                   parts = new[] { (Hovl + "Magic circles/Magic circle 2.prefab", 1f, 0.05f) } },
        new Kind { name = "Hide", scripts = new[] { "HideZone" }, names = new[] { "HideSpot" }, place = Place.Ground,
                   parts = new[] { (Hovl + "Magic shields/Magic shield blue.prefab", 0.6f, 0f) } },
        new Kind { name = "Bomb", scripts = new[] { "DefuseTrigger" }, place = Place.Centre,
                   parts = new[] { (Hovl + "Character auras/Lightning aura.prefab", 0.5f, 0f) } },

        // Small ones for things you can use any time (not mission objectives)
        new Kind { name = "Service", scripts = new[] { "FuelStation", "TowPhone" }, place = Place.Ground,
                   parts = new[] { (Hovl + "Magic circles/Magic circle.prefab", 0.45f, 0.03f) } },
        new Kind { name = "Respray", scripts = new[] { "ResprayShop" }, place = Place.Ground, fitToZone = true,
                   parts = new[] { (Hovl + "Magic circles/Magic circle.prefab", 1f, 0.03f) } },
        new Kind { name = "Noise", scripts = new[] { "NoiseMaker" }, place = Place.Centre,
                   parts = new[] { (Hovl + "Environment/Crystal effect blue.prefab", 0.5f, 0f) } },
    };

    // Ring radius of a circle effect at scale 1 (metres) - used to fit zones
    private const float RingRadius = 1.5f;

    [MenuItem("Tools/Gizmos/Install Gizmo VFX (this scene)")]
    public static string InstallInOpenScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var log = new List<string>();
        var prefabs = Kinds.ToDictionary(k => k.name, MarkerPrefab);
        referenced = ReferencedObjects(scene);

        // Find them all first - removing placeholders changes the hierarchy
        var gizmos = new List<(Transform t, Kind kind)>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                Kind kind = Kinds.FirstOrDefault(k => k.scripts.Any(s => t.GetComponent(s) != null)
                                                   || k.names.Any(n => t.name.StartsWith(n)));
                if (kind != null) gizmos.Add((t, kind));
            }

        foreach (var (t, kind) in gizmos)
        {
            if (t == null) continue;

            // Replace an older marker
            foreach (Transform c in t.Cast<Transform>().ToList())
                if (c.name.StartsWith("GizmoVFX_")) Undo.DestroyObjectImmediate(c.gameObject);

            int removed = RemovePlaceholders(t.gameObject, scene);
            AddMarker(t, kind, prefabs[kind.name]);
            log.Add($"{t.name} ({kind.name}{(removed > 0 ? $", -{removed} placeholder" : "")})");
        }

        StashSamples(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        string summary = $"Gizmo VFX in {scene.name}: " + string.Join(", ", log);
        Debug.Log(summary);
        return summary;
    }

    // Back to the effects listed above (overwrites hand edits to the marker prefabs;
    // every gizmo in every scene keeps pointing at them)
    [MenuItem("Tools/Gizmos/Rebuild Gizmo VFX Prefabs")]
    public static void RebuildPrefabs()
    {
        foreach (Kind k in Kinds) MarkerPrefab(k, true);
    }

    // One prefab per kind: an empty with GizmoVFX + the effects as nested prefabs
    private static GameObject MarkerPrefab(Kind kind) => MarkerPrefab(kind, false);

    private static GameObject MarkerPrefab(Kind kind, bool rebuild)
    {
        string path = $"{Folder}/GizmoVFX_{kind.name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && !rebuild) return existing;

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "GizmoVFX");
        var root = new GameObject("GizmoVFX_" + kind.name);
        root.AddComponent<GizmoVFX>();
        foreach (var (prefab, scale, y) in kind.parts)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            if (src == null) { Debug.LogWarning("Gizmo VFX: missing " + prefab); continue; }
            var part = (GameObject)PrefabUtility.InstantiatePrefab(src);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = new Vector3(0f, y, 0f);
            part.transform.localScale = src.transform.localScale * scale;
        }
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static void AddMarker(Transform gizmo, Kind kind, GameObject prefab)
    {
        Bounds zone = TriggerBounds(gizmo);
        Vector3 at = zone.center;
        if (kind.place == Place.Ground) at = GroundBelow(gizmo, at);

        var marker = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gizmo.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(marker, "Install gizmo VFX");
        marker.transform.SetParent(gizmo, false);
        marker.transform.position = at;
        // Upright, facing the way the gizmo faces (doors)
        marker.transform.rotation = Quaternion.Euler(0f, gizmo.eulerAngles.y, 0f);

        // Zones: as wide as the trigger, but never taller than 1.5x (a big zone stays a ring, not a wall)
        Vector3 size = Vector3.one;
        if (kind.fitToZone)
        {
            float radius = Mathf.Min(zone.extents.x, zone.extents.z);
            float wide = Mathf.Clamp(radius / RingRadius, 0.5f, 6f);
            size = new Vector3(wide, Mathf.Min(wide, 1.5f), wide);
        }
        Vector3 parent = gizmo.lossyScale;
        marker.transform.localScale = new Vector3(size.x / Safe(parent.x), size.y / Safe(parent.y), size.z / Safe(parent.z));
    }

    private static float Safe(float v) => Mathf.Abs(v) < 1e-4f ? 1f : v;

    private static Bounds TriggerBounds(Transform gizmo)
    {
        Collider c = gizmo.GetComponents<Collider>().FirstOrDefault(x => x.isTrigger) ?? gizmo.GetComponent<Collider>();
        if (c == null)
        {
            // Scripts that check distance instead of a trigger (respray shop, pumps, phones): their 'radius'
            foreach (MonoBehaviour mb in gizmo.GetComponents<MonoBehaviour>())
            {
                SerializedProperty r = mb != null ? new SerializedObject(mb).FindProperty("radius") : null;
                if (r != null && r.propertyType == SerializedPropertyType.Float)
                    return new Bounds(gizmo.position, Vector3.one * r.floatValue * 2f);
            }
            return new Bounds(gizmo.position, Vector3.one);
        }
        // Disabled objects report empty bounds - work them out from the shape
        if (c is SphereCollider s) return new Bounds(gizmo.TransformPoint(s.center), Vector3.one * s.radius * 2f * MaxAbs(gizmo.lossyScale));
        if (c is BoxCollider b) return new Bounds(gizmo.TransformPoint(b.center), Vector3.Scale(b.size, Abs(gizmo.lossyScale)));
        return c.bounds;
    }

    private static Vector3 GroundBelow(Transform gizmo, Vector3 from)
    {
        float best = float.MaxValue;
        Vector3 ground = new Vector3(from.x, from.y - 1f, from.z);
        foreach (RaycastHit hit in Physics.RaycastAll(from + Vector3.up * 0.5f, Vector3.down, 15f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(gizmo)) continue;
            if (hit.distance < best) { best = hit.distance; ground = hit.point; }
        }
        return ground;
    }

    // Unity primitives with the default "Lit" material that stood in for the gizmo
    private static int RemovePlaceholders(GameObject gizmo, UnityEngine.SceneManagement.Scene scene)
    {
        int count = 0;
        foreach (MeshRenderer r in gizmo.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "";
            bool primitive = mesh == "Sphere" || mesh == "Cube" || mesh == "Capsule" || mesh == "Cylinder";
            if (!primitive || r.sharedMaterial == null || r.sharedMaterial.name != "Lit") continue;

            GameObject go = r.gameObject;
            bool onlyVisual = go != gizmo && go.transform.childCount == 0 &&
                              go.GetComponents<Component>().All(c => c is Transform || c is MeshFilter || c is MeshRenderer || c is Collider);
            if (referenced.Contains(go) || referenced.Contains(r) || referenced.Contains(go.transform) || (mf != null && referenced.Contains(mf)))
            {
                Undo.RecordObject(r, "Install gizmo VFX");
                r.enabled = false; // something points at it - just hide it
            }
            else if (onlyVisual)
            {
                Undo.DestroyObjectImmediate(go);
            }
            else
            {
                Undo.DestroyObjectImmediate(r);
                if (mf != null) Undo.DestroyObjectImmediate(mf);
            }
            count++;
        }
        return count;
    }

    private static HashSet<Object> referenced = new HashSet<Object>();

    // Everything any script in the scene points at (so we don't delete it)
    private static HashSet<Object> ReferencedObjects(UnityEngine.SceneManagement.Scene scene)
    {
        var set = new HashSet<Object>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                SerializedProperty p = new SerializedObject(mb).GetIterator();
                while (p.NextVisible(true))
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null)
                        set.Add(p.objectReferenceValue);
            }
        return set;
    }

    // VFX dropped loose into the scene to try them out -> one switched-off group
    private static void StashSamples(UnityEngine.SceneManagement.Scene scene)
    {
        var loose = scene.GetRootGameObjects().Where(g =>
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(g);
            string p = src != null ? AssetDatabase.GetAssetPath(src) : "";
            return p.StartsWith("Assets/Hovl Studio") || p.StartsWith("Assets/Imports/VFX");
        }).ToList();
        if (loose.Count == 0) return;

        GameObject group = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "VFX_Samples");
        if (group == null)
        {
            group = new GameObject("VFX_Samples");
            Undo.RegisterCreatedObjectUndo(group, "Install gizmo VFX");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(group, scene);
        }
        foreach (GameObject g in loose) Undo.SetTransformParent(g.transform, group.transform, "Install gizmo VFX");
        group.SetActive(false);
    }

    private static float MaxAbs(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
