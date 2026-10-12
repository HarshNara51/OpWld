using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Vehicles > Make Selected Cars Drivable
//
// Select one or more plain car models in the scene (just the imported
// model, no scripts) and run this. Each one becomes a drivable car set up
// exactly like the Free Racing Car (Prometeo controller, enter/exit, damage,
// fuel, respray, tyre smoke) and is saved as a prefab in
// Assets/Prefabs/PlayerCars. The selected model is replaced by the prefab.
//
// Wheels: separate wheel objects are used as they are. If the wheels are
// baked into one big mesh (e.g. the Unity CarsAssetPack cars), they are
// cut out automatically - the wheel pieces are found as the round parts at
// the four corners - and saved as new meshes next to the prefab.
public static class PlayerCarBuilder
{
    private const string TemplatePath = "Assets/Imports/ARCADE - FREE Racing Car/Prefabs (With Colliders)/Free Racing Car.prefab";
    private const string OutFolder = "Assets/Prefabs/PlayerCars";
    private static readonly string[] WheelNames = { "FrontLeftWheel", "FrontRightWheel", "RearLeftWheel", "RearRightWheel" };

    [MenuItem("Tools/Vehicles/Make Selected Cars Drivable")]
    private static void MakeSelected()
    {
        foreach (GameObject go in Selection.gameObjects.ToArray())
        {
            if (EditorUtility.IsPersistent(go)) continue; // scene objects only
            GameObject car = Make(go);
            if (car != null) Selection.activeGameObject = car;
        }
    }

    [MenuItem("Tools/Vehicles/Make Selected Cars Drivable", true)]
    private static bool MakeSelectedValid() => Selection.gameObjects.Any(g => !EditorUtility.IsPersistent(g));

    // Returns the new prefab instance that replaced the model in the scene
    public static GameObject Make(GameObject model, Texture2D[] paintTextures = null)
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
        if (template == null)
        {
            Debug.LogError("PlayerCarBuilder: template car not found at " + TemplatePath);
            return null;
        }
        EnsureFolder(OutFolder);
        EnsureFolder(OutFolder + "/Meshes");

        string carName = model.name.Replace("(Clone)", "").Trim();
        Transform sceneParent = model.transform.parent;
        Vector3 scenePos = model.transform.position;
        Quaternion sceneRot = model.transform.rotation;

        // Work on a copy, upright at the origin (keeps its size)
        GameObject src = Object.Instantiate(model);
        src.transform.SetParent(null);
        src.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        src.transform.localScale = model.transform.lossyScale;

        foreach (WheelCollider wc in src.GetComponentsInChildren<WheelCollider>(true)) Object.DestroyImmediate(wc);
        foreach (Rigidbody rb in src.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (Collider c in src.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (MonoBehaviour mb in src.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);

        // ---------- wheels ----------
        Renderer[] wheels = FindWheels(src);
        var looseParts = new List<Transform>();
        if (wheels == null)
        {
            wheels = SplitBakedWheels(src, carName, looseParts);
            if (wheels == null)
            {
                Object.DestroyImmediate(src);
                Debug.LogError($"PlayerCarBuilder: couldn't find 4 wheels on '{carName}'.");
                return null;
            }
        }

        // ---------- new car ----------
        var root = new GameObject(carName);
        Transform modelT = Child(root.transform, "Model");
        Transform wheelsT = Child(root.transform, "Wheels");
        Transform meshesT = Child(wheelsT, "Meshes");
        Transform collidersT = Child(wheelsT, "Colliders");

        foreach (Transform t in src.transform.Cast<Transform>().ToList()) t.SetParent(modelT, true);
        foreach (Transform t in looseParts) t.SetParent(modelT, true);
        Object.DestroyImmediate(src);

        // Wheel holders at each wheel's centre, upright (the controller sets their rotation)
        var holders = new Transform[4];
        var radius = new float[4];
        for (int i = 0; i < 4; i++)
        {
            Bounds b = wheels[i].bounds;
            radius[i] = b.extents.y;
            holders[i] = Child(meshesT, WheelNames[i]);
            holders[i].position = b.center;
            wheels[i].transform.SetParent(holders[i], true);
        }
        RemoveEmpty(modelT);

        // Pivot on the ground (like the Free Racing Car)
        float ground = Enumerable.Range(0, 4).Min(i => holders[i].position.y - radius[i]);
        modelT.position -= Vector3.up * ground;
        meshesT.position -= Vector3.up * ground;

        // Wheel colliders: the template's suspension settings, this car's size
        WheelCollider[] templateWheels = template.GetComponentsInChildren<WheelCollider>(true);
        var colliders = new WheelCollider[4];
        for (int i = 0; i < 4; i++)
        {
            Transform t = Child(collidersT, WheelNames[i]);
            t.position = holders[i].position;
            colliders[i] = t.gameObject.AddComponent<WheelCollider>();
            if (templateWheels.Length > 0)
            {
                UnityEditorInternal.ComponentUtility.CopyComponent(templateWheels[0]);
                UnityEditorInternal.ComponentUtility.PasteComponentValues(colliders[i]);
            }
            colliders[i].radius = radius[i];
        }

        // Body collider: the body's box, its bottom a little above the road
        Bounds body = BodyBounds(modelT, wheels);
        var box = modelT.gameObject.AddComponent<BoxCollider>();
        float bottom = Mathf.Max(body.min.y, radius.Average() * 0.5f);
        box.center = modelT.InverseTransformPoint(new Vector3(body.center.x, (bottom + body.max.y) * 0.5f, body.center.z));
        box.size = new Vector3(body.size.x, body.max.y - bottom, body.size.z);

        // ---------- components, copied from the template ----------
        foreach (System.Type type in new[] { typeof(Rigidbody), typeof(PrometeoCarController), typeof(VehicleInteraction),
                                             typeof(VehicleHealth), typeof(VehicleFuel) })
        {
            Component from = template.GetComponent(type);
            if (from == null) continue;
            UnityEditorInternal.ComponentUtility.CopyComponent(from);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(root);
        }

        // Tyre smoke + skid marks at the rear wheels, engine smoke at the bonnet
        Vector3[] at = holders.Select(h => root.transform.InverseTransformPoint(h.position)).ToArray();
        Transform effects = CopyChild(template.transform, "Effects", root.transform);
        if (effects != null)
        {
            effects.localPosition = Vector3.zero;
            Place(effects, "LeftTireSmoke_PS", at[2]);
            Place(effects, "RightTireSmoke_PS", at[3]);
            Place(effects, "LeftTireSkid", new Vector3(at[2].x, 0.1f, at[2].z));
            Place(effects, "RightTireSkid", new Vector3(at[3].x, 0.1f, at[3].z));
        }
        float frontZ = (at[0].z + at[1].z) * 0.5f;
        Vector3 bonnet = new Vector3(0f, radius.Average() + 0.55f, frontZ + 0.1f);
        Transform light = CopyChild(template.transform, "vfx_Smoke_01 (1)", root.transform);
        Transform heavy = CopyChild(template.transform, "vfx_Smoke_01 (Heavy) (1)", root.transform);
        if (light != null) { light.name = "vfx_Smoke_01"; light.localPosition = bonnet; }
        if (heavy != null) { heavy.name = "vfx_Smoke_01 (Heavy)"; heavy.localPosition = bonnet; }

        Transform exit = Child(root.transform, "ExitPoint");
        exit.localPosition = new Vector3(body.min.x - 0.7f, 0.82f, 0f);

        // ---------- wire up ----------
        var pcc = root.GetComponent<PrometeoCarController>();
        var so = new SerializedObject(pcc);
        so.FindProperty("frontLeftMesh").objectReferenceValue = holders[0].gameObject;
        so.FindProperty("frontRightMesh").objectReferenceValue = holders[1].gameObject;
        so.FindProperty("rearLeftMesh").objectReferenceValue = holders[2].gameObject;
        so.FindProperty("rearRightMesh").objectReferenceValue = holders[3].gameObject;
        so.FindProperty("frontLeftCollider").objectReferenceValue = colliders[0];
        so.FindProperty("frontRightCollider").objectReferenceValue = colliders[1];
        so.FindProperty("rearLeftCollider").objectReferenceValue = colliders[2];
        so.FindProperty("rearRightCollider").objectReferenceValue = colliders[3];
        so.FindProperty("RLWParticleSystem").objectReferenceValue = Find<ParticleSystem>(effects, "LeftTireSmoke_PS");
        so.FindProperty("RRWParticleSystem").objectReferenceValue = Find<ParticleSystem>(effects, "RightTireSmoke_PS");
        so.FindProperty("RLWTireSkid").objectReferenceValue = Find<TrailRenderer>(effects, "LeftTireSkid");
        so.FindProperty("RRWTireSkid").objectReferenceValue = Find<TrailRenderer>(effects, "RightTireSkid");
        so.ApplyModifiedPropertiesWithoutUndo();

        so = new SerializedObject(root.GetComponent<VehicleInteraction>());
        so.FindProperty("carControllerScript").objectReferenceValue = pcc;
        so.FindProperty("exitPoint").objectReferenceValue = exit;
        so.ApplyModifiedPropertiesWithoutUndo();

        so = new SerializedObject(root.GetComponent<VehicleHealth>());
        so.FindProperty("lightSmoke").objectReferenceValue = light != null ? light.GetComponent<ParticleSystem>() : null;
        so.FindProperty("heavySmoke").objectReferenceValue = heavy != null ? heavy.GetComponent<ParticleSystem>() : null;
        so.ApplyModifiedPropertiesWithoutUndo();

        var paint = root.AddComponent<CarPaint>();
        if (paintTextures != null && paintTextures.Length > 0)
        {
            so = new SerializedObject(paint);
            SerializedProperty list = so.FindProperty("paintTextures");
            list.arraySize = paintTextures.Length;
            for (int i = 0; i < paintTextures.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = paintTextures[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- save + put in the scene ----------
        string path = $"{OutFolder}/{carName}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, model.scene);
        Undo.RegisterCreatedObjectUndo(instance, "Make car drivable");
        instance.transform.SetParent(sceneParent, true);
        instance.transform.SetPositionAndRotation(scenePos + sceneRot * Vector3.up * ground, sceneRot);
        instance.transform.SetSiblingIndex(model.transform.GetSiblingIndex());
        LinkToScene(instance.GetComponent<VehicleInteraction>());
        Undo.DestroyObjectImmediate(model);

        EditorSceneManager.MarkSceneDirty(instance.scene);
        Debug.Log($"PlayerCarBuilder: '{carName}' is drivable - prefab saved to {path}.", instance);
        return instance;
    }

    // The player and camera are scene objects: copy them from a car that already works in this scene
    public static void LinkToScene(VehicleInteraction vi)
    {
        if (vi == null) return;
        foreach (VehicleInteraction other in Object.FindObjectsByType<VehicleInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other == vi) continue;
            var from = new SerializedObject(other);
            if (from.FindProperty("player").objectReferenceValue == null) continue;
            var to = new SerializedObject(vi);
            to.FindProperty("player").objectReferenceValue = from.FindProperty("player").objectReferenceValue;
            to.FindProperty("cameraOrbit").objectReferenceValue = from.FindProperty("cameraOrbit").objectReferenceValue;
            to.ApplyModifiedProperties();
            return;
        }
        Debug.LogWarning($"{vi.name}: assign Player and Camera Orbit on VehicleInteraction by hand.", vi);
    }

    // ================= wheels =================

    // Separate wheel objects: the biggest wheel-named mesh at each corner (FL, FR, RL, RR)
    private static Renderer[] FindWheels(GameObject car)
    {
        var result = new Renderer[4];
        foreach (Renderer r in car.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name.ToLowerInvariant();
            if (n.Contains("steer") || !(n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre") || n == "fl" || n == "fr" || n == "bl" || n == "br")) continue;
            int q = Corner(r.bounds.center);
            if (result[q] == null || r.bounds.size.sqrMagnitude > result[q].bounds.size.sqrMagnitude) result[q] = r;
        }
        return result.All(r => r != null) ? result : null;
    }

    private static int Corner(Vector3 p) => (p.z > 0f ? 0 : 2) + (p.x > 0f ? 1 : 0); // FL, FR, RL, RR

    // Cuts the wheels out of one big mesh. The wheel pieces are the separate
    // parts (tyre, rim, brake disc...) that fit inside a round tyre at a corner.
    private static Renderer[] SplitBakedWheels(GameObject car, string carName, List<Transform> looseParts)
    {
        MeshFilter mf = car.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => f.sharedMesh != null).OrderByDescending(f => f.sharedMesh.vertexCount).FirstOrDefault();
        if (mf == null) return null;

        Mesh mesh = mf.sharedMesh;
        Matrix4x4 m = mf.transform.localToWorldMatrix; // car is at the origin, so this = car space in metres
        Vector3[] v = mesh.vertices.Select(p => m.MultiplyPoint3x4(p)).ToArray();
        int n = v.Length;

        // Parts = groups of triangles that touch (vertices at the same spot count as touching)
        var parent = Enumerable.Range(0, n).ToArray();
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[b] = a; }
        var bySpot = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < n; i++)
        {
            var key = Vector3Int.RoundToInt(v[i] * 10000f);
            if (bySpot.TryGetValue(key, out int j)) Union(i, j); else bySpot[key] = i;
        }
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            int[] tri = mesh.GetTriangles(s);
            for (int t = 0; t < tri.Length; t += 3) { Union(tri[t], tri[t + 1]); Union(tri[t], tri[t + 2]); }
        }

        var parts = new Dictionary<int, Bounds>();
        for (int i = 0; i < n; i++)
        {
            int r = Find(i);
            if (parts.TryGetValue(r, out Bounds b)) { b.Encapsulate(v[i]); parts[r] = b; }
            else parts[r] = new Bounds(v[i], Vector3.zero);
        }

        // Tyres: round (as tall as long), thin across, low down - the biggest one at each corner
        Bounds all = new Bounds(v[0], Vector3.zero);
        foreach (Vector3 p in v) all.Encapsulate(p);
        var tyres = new Bounds?[4];
        foreach (Bounds b in parts.Values)
        {
            Vector3 s = b.size;
            bool round = s.y > 0.25f && Mathf.Abs(s.y - s.z) < 0.15f * s.y && s.x < 0.7f * s.y && b.center.y < all.center.y;
            if (!round) continue;
            int q = Corner(b.center);
            if (tyres[q] == null || s.y > tyres[q].Value.size.y) tyres[q] = b;
        }
        if (tyres.Any(t => t == null)) return null;

        // Which wheel each part belongs to (-1 = body)
        var owner = new Dictionary<int, int>();
        foreach (var kv in parts)
        {
            owner[kv.Key] = -1;
            for (int q = 0; q < 4; q++)
            {
                Bounds w = tyres[q].Value, b = kv.Value;
                float r = w.extents.y * 1.05f, hw = w.extents.x + 0.1f;
                if (b.min.x >= w.center.x - hw && b.max.x <= w.center.x + hw &&
                    b.min.y >= w.center.y - r && b.max.y <= w.center.y + r &&
                    b.min.z >= w.center.z - r && b.max.z <= w.center.z + r) { owner[kv.Key] = q; break; }
            }
        }

        Material[] mats = mf.GetComponent<MeshRenderer>().sharedMaterials;
        Mesh bodyMesh = BuildPart(mesh, m, v, i => owner[Find(i)] == -1, Vector3.zero, carName + "_Body");
        SaveMesh(bodyMesh);

        // Body: the new mesh, in car space
        if (mf.transform == car.transform)
        {
            var bodyGo = new GameObject(mf.name);
            bodyGo.AddComponent<MeshFilter>();
            bodyGo.AddComponent<MeshRenderer>().sharedMaterials = mats;
            Object.DestroyImmediate(car.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(mf);
            mf = bodyGo.GetComponent<MeshFilter>();
        }
        mf.transform.SetParent(null, true);
        mf.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        mf.transform.localScale = Vector3.one;
        mf.sharedMesh = bodyMesh;
        looseParts.Add(mf.transform);

        var wheels = new Renderer[4];
        for (int q = 0; q < 4; q++)
        {
            Vector3 c = tyres[q].Value.center;
            int qq = q;
            Mesh wm = BuildPart(mesh, m, v, i => owner[Find(i)] == qq, c, $"{carName}_{WheelNames[q]}");
            SaveMesh(wm);
            var go = new GameObject(WheelNames[q] + "Mesh");
            go.transform.position = c;
            go.AddComponent<MeshFilter>().sharedMesh = wm;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            wheels[q] = go.GetComponent<MeshRenderer>();
        }
        return wheels;
    }

    // New mesh from the triangles whose vertices pass 'keep', moved by -origin
    private static Mesh BuildPart(Mesh src, Matrix4x4 m, Vector3[] v, System.Func<int, bool> keep, Vector3 origin, string name)
    {
        Vector3[] normals = src.normals;
        Vector4[] tangents = src.tangents;
        Vector2[] uv = src.uv;
        Vector2[] uv2 = src.uv2;
        Color[] colors = src.colors;

        var remap = new Dictionary<int, int>();
        var pos = new List<Vector3>();
        var nor = new List<Vector3>();
        var tan = new List<Vector4>();
        var uvs = new List<Vector2>();
        var uvs2 = new List<Vector2>();
        var col = new List<Color>();
        var subs = new List<int>[src.subMeshCount];

        int Map(int i)
        {
            if (remap.TryGetValue(i, out int k)) return k;
            k = pos.Count;
            remap[i] = k;
            pos.Add(v[i] - origin);
            if (normals.Length > 0) nor.Add(m.MultiplyVector(normals[i]).normalized);
            if (tangents.Length > 0)
            {
                Vector3 t = m.MultiplyVector(tangents[i]).normalized;
                tan.Add(new Vector4(t.x, t.y, t.z, tangents[i].w));
            }
            if (uv.Length > 0) uvs.Add(uv[i]);
            if (uv2.Length > 0) uvs2.Add(uv2[i]);
            if (colors.Length > 0) col.Add(colors[i]);
            return k;
        }

        for (int s = 0; s < src.subMeshCount; s++)
        {
            subs[s] = new List<int>();
            int[] tri = src.GetTriangles(s);
            for (int t = 0; t < tri.Length; t += 3)
            {
                if (!keep(tri[t])) continue;
                subs[s].Add(Map(tri[t]));
                subs[s].Add(Map(tri[t + 1]));
                subs[s].Add(Map(tri[t + 2]));
            }
        }

        var mesh = new Mesh { name = name };
        if (pos.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(pos);
        if (nor.Count > 0) mesh.SetNormals(nor);
        if (tan.Count > 0) mesh.SetTangents(tan);
        if (uvs.Count > 0) mesh.SetUVs(0, uvs);
        if (uvs2.Count > 0) mesh.SetUVs(1, uvs2);
        if (col.Count > 0) mesh.SetColors(col);
        mesh.subMeshCount = subs.Length;
        for (int s = 0; s < subs.Length; s++) mesh.SetTriangles(subs[s], s);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SaveMesh(Mesh mesh)
    {
        string path = $"{OutFolder}/Meshes/{mesh.name}.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
    }

    // ================= helpers =================

    private static Bounds BodyBounds(Transform model, Renderer[] wheels)
    {
        Bounds b = default;
        bool any = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            if (wheels.Contains(r) || r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static Transform CopyChild(Transform templateRoot, string name, Transform newParent)
    {
        Transform from = templateRoot.Find(name);
        if (from == null) return null;
        Transform copy = Object.Instantiate(from.gameObject, newParent, false).transform;
        copy.name = from.name;
        return copy;
    }

    private static void Place(Transform parent, string child, Vector3 localPos)
    {
        Transform t = parent.Find(child);
        if (t != null) t.localPosition = localPos;
    }

    private static T Find<T>(Transform parent, string child) where T : Component
    {
        Transform t = parent != null ? parent.Find(child) : null;
        return t != null ? t.GetComponent<T>() : null;
    }

    // Leftover empty folders (e.g. the model's old "Wheels/Colliders")
    private static void RemoveEmpty(Transform t)
    {
        foreach (Transform c in t.Cast<Transform>().ToList()) RemoveEmpty(c);
        if (t.parent != null && t.childCount == 0 && t.GetComponents<Component>().Length == 1) Object.DestroyImmediate(t.gameObject);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
