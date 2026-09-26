using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Editor-only tool. Keep this file inside an "Editor" folder (e.g. Assets/Scripts/Editor).
public static class AutoLODSetup
{
    // The object is culled (hidden) when it becomes smaller than this fraction of the screen height.
    // 0.02 = 2% of screen height, i.e. tiny / far away.
    // The OLD value was 0.5 (50%), which hid objects while they were still clearly visible.
    const float CullScreenHeight = 0.02f;

    // Name suffix used to recognise LOD parents created by this tool.
    const string ParentSuffix = "_LODParent";

    // ------------------------------------------------------------------
    // Tools > Auto Setup LOD Groups
    // Wraps each selected object in a parent that has an LOD Group.
    // Supports Ctrl+Z.
    // ------------------------------------------------------------------
    [MenuItem("Tools/Auto Setup LOD Groups")]
    static void SetupLODs()
    {
        int created = 0;
        int skipped = 0;

        foreach (GameObject obj in Selection.gameObjects)
        {
            // Skip objects that already have an LOD Group (their own or one made by this tool).
            bool alreadyHasLOD = obj.GetComponentInChildren<LODGroup>(true) != null;
            bool alreadyWrapped = obj.transform.parent != null &&
                                  obj.transform.parent.name.EndsWith(ParentSuffix);
            if (alreadyHasLOD || alreadyWrapped)
            {
                skipped++;
                continue;
            }

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                skipped++;
                continue;
            }

            int siblingIndex = obj.transform.GetSiblingIndex();

            // 1. Create the parent in the SAME place in the hierarchy as the original object.
            GameObject parent = new GameObject(obj.name + ParentSuffix);
            Undo.RegisterCreatedObjectUndo(parent, "Auto Setup LOD Groups");
            parent.transform.SetParent(obj.transform.parent, false);
            parent.transform.SetPositionAndRotation(obj.transform.position, obj.transform.rotation);
            parent.transform.SetSiblingIndex(siblingIndex);

            // Copy static flags and layer so occlusion culling / batching / lighting still work.
            GameObjectUtility.SetStaticEditorFlags(parent, GameObjectUtility.GetStaticEditorFlags(obj));
            parent.layer = obj.layer;

            // 2. Move the original object inside the new parent.
            Undo.SetTransformParent(obj.transform, parent.transform, "Auto Setup LOD Groups");

            // 3. Add the LOD Group with a single level that culls when the object is tiny.
            LODGroup lodGroup = Undo.AddComponent<LODGroup>(parent);
            lodGroup.SetLODs(new[] { new LOD(CullScreenHeight, renderers) });
            lodGroup.RecalculateBounds();

            created++;
        }

        Debug.Log($"Auto LOD: created {created} LOD Group(s), skipped {skipped} object(s).");
    }

    // ------------------------------------------------------------------
    // Tools > Fix Existing Auto LOD Groups
    // Updates LOD Groups this tool made earlier (with the old 0.5 value)
    // in the currently open scene(s). Run it once in every scene.
    // Only touches objects whose name ends in "_LODParent".
    // ------------------------------------------------------------------
    [MenuItem("Tools/Fix Existing Auto LOD Groups")]
    static void FixExistingLODs()
    {
        int fixedCount = 0;

        LODGroup[] groups = Object.FindObjectsByType<LODGroup>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (LODGroup group in groups)
        {
            if (!group.gameObject.name.EndsWith(ParentSuffix))
                continue; // not made by this tool — leave it alone

            LOD[] lods = group.GetLODs();
            if (lods.Length != 1)
                continue;

            Undo.RecordObject(group, "Fix Auto LOD Groups");
            lods[0].screenRelativeTransitionHeight = CullScreenHeight;
            group.SetLODs(lods);
            group.RecalculateBounds();
            fixedCount++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"Auto LOD: fixed {fixedCount} LOD Group(s). Remember to save the scene (Ctrl+S).");
    }
}