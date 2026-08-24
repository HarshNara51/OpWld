using UnityEngine;
using UnityEditor;

public class AutoLODSetup : MonoBehaviour
{
    // This adds a new button to your top menu bar in Unity!
    [MenuItem("Tools/Auto Setup LOD Groups")]
    static void SetupLODs()
    {
        // Loop through every object you currently have selected in the Hierarchy
        foreach (GameObject obj in Selection.gameObjects)
        {
            // 1. Create the new parent object
            GameObject parent = new GameObject(obj.name + "_LODParent");
            parent.transform.position = obj.transform.position;
            parent.transform.rotation = obj.transform.rotation;

            // 2. Move the original building inside the new parent
            obj.transform.SetParent(parent.transform);

            // 3. Add the LOD Group component to the parent
            LODGroup lodGroup = parent.AddComponent<LODGroup>();
            
            // 4. Set up LOD 0 with the building's renderer
            LOD[] lods = new LOD[1];
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            
            // The '0.5f' means the object drops out when it takes up less than 50% of the screen height. 
            // You can tweak this number!
            lods[0] = new LOD(0.5f, renderers); 
            
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }
        
        Debug.Log("Successfully generated LOD Groups for " + Selection.gameObjects.Length + " objects! 🎉");
    }
}