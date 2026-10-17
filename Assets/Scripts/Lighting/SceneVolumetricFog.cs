using UnityEngine;
using UnityEngine.Rendering.Universal;

// One per scene (on the Global Volume object). Holds this scene's AERO volumetric fog material
// (mist, god rays) and plugs it into the "Volumetric Fog" renderer feature on PC_Renderer.
//
// The feature is only on while a scene with this component is loaded AND the player has
// "God Rays" switched on (GraphicsQualityManager.VolumetricFog). Scenes without it get plain fog.
// In the editor (not playing) the fog always previews so you can tweak the material.
[ExecuteAlways]
public class SceneVolumetricFog : MonoBehaviour
{
    [Tooltip("This scene's fog look: a copy of AERO's 'Volumetric Fog PC' material (Assets/Settings/VolumetricFog)")]
    [SerializeField] private Material fogMaterial;
    [Tooltip("The URP renderer that has the 'Volumetric Fog' features (PC_Renderer)")]
    [SerializeField] private UniversalRendererData rendererData;

    const string FogFeatureName = "Volumetric Fog";

    static SceneVolumetricFog current;
    static UniversalRendererData lastRenderer;

    private void OnEnable()
    {
        current = this;
        if (rendererData != null) lastRenderer = rendererData;
        Refresh();
    }

    private void OnDisable()
    {
        if (current == this) current = null;
        Refresh();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (isActiveAndEnabled && current == this) Refresh();
    }
#endif

    static bool SettingOn
    {
        get
        {
            if (!Application.isPlaying) return true;
            return GraphicsQualityManager.Instance == null || GraphicsQualityManager.Instance.VolumetricFog;
        }
    }

    // Called on scene load/unload and whenever the God Rays setting changes.
    public static void Refresh()
    {
        UniversalRendererData data = current != null && current.rendererData != null ? current.rendererData : lastRenderer;
        if (data == null) return;

        Material mat = current != null ? current.fogMaterial : null;
        bool on = mat != null && SettingOn;

        foreach (ScriptableRendererFeature feature in data.rendererFeatures)
        {
            if (feature == null) continue;
            if (feature is FullScreenPassRendererFeature fullscreen && feature.name == FogFeatureName)
            {
                if (mat != null) fullscreen.passMaterial = mat;
                feature.SetActive(on);
            }
            else if (feature is Mirza.AERO.FullscreenCustomLightingFeature lighting)
            {
                if (mat != null) lighting.material = mat;
                feature.SetActive(on);
            }
        }
    }
}
