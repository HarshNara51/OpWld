using UnityEngine;
using UnityEditor;

public class PrefabReplacer : EditorWindow
{
    public GameObject replacementPrefab;

    // Creates a new window under the Tools menu
    [MenuItem("Tools/Replace Selected Objects")]
    static void CreateReplaceWindow()
    {
        EditorWindow.GetWindow<PrefabReplacer>("Replace Objects");
    }

    void OnGUI()
    {
        GUILayout.Label("1. Select the objects in your Scene you want to replace.\n2. Drag your Master Prefab below.\n3. Click Replace!", EditorStyles.wordWrappedLabel);

        GUILayout.Space(10);

        replacementPrefab = (GameObject)EditorGUILayout.ObjectField("Master Prefab", replacementPrefab, typeof(GameObject), false);

        GUILayout.Space(10);

        if (GUILayout.Button("Replace!") && replacementPrefab != null)
        {
            int count = 0;
            foreach (GameObject obj in Selection.gameObjects)
            {
                // Instantiate the correct prefab
                GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(replacementPrefab);

                // Copy the exact position, rotation, scale, and parent
                newObj.transform.position = obj.transform.position;
                newObj.transform.rotation = obj.transform.rotation;
                newObj.transform.localScale = obj.transform.localScale;
                newObj.transform.parent = obj.transform.parent;

                // Register undo so you can Ctrl+Z if you mess up!
                Undo.RegisterCreatedObjectUndo(newObj, "Replace Object");
                Undo.DestroyObjectImmediate(obj);
                count++;
            }
            Debug.Log($"Successfully swapped {count} objects! 🚗");
        }
    }
}