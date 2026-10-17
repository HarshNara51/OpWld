using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Lives on the Managers prefab (like PauseManager), so it's persistent and runs in every scene.
//
// Switches between the "Low", "Medium", "High" and "Ultra" quality levels (Project Settings > Quality).
// Each level has its own URP asset (shadows) and terrain overrides (grass density/distance,
// tree distance), so every scene gets the same foliage settings automatically.
//
// Also owns the "God Rays" switch (AERO volumetric fog, see SceneVolumetricFog). Picking a preset
// resets it to that preset's default (on for Ultra only); the God Rays button can then override it.
//
// First launch picks a level from the GPU's video memory; after that the player's choice is saved.
public class GraphicsQualityManager : MonoBehaviour
{
    public static GraphicsQualityManager Instance { get; private set; }

    public enum Preset { Low, Medium, High, Ultra }

    public static event System.Action<Preset> PresetChanged;
    public static event System.Action<bool> VolumetricFogChanged;

    const string PrefsKey = "GraphicsPreset";
    const string FogPrefsKey = "VolumetricFog";

    [Tooltip("Optional: label on a pause-menu button, updated to show the current preset")]
    [SerializeField] private TMPro.TMP_Text buttonLabel;

    public Preset Current { get; private set; }
    public bool VolumetricFog { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        Preset preset = PlayerPrefs.HasKey(PrefsKey)
            ? (Preset)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey), 0, 3)
            : DetectDefault();
        Apply(preset, save: false);

        if (PlayerPrefs.HasKey(FogPrefsKey))
            SetVolumetricFog(PlayerPrefs.GetInt(FogPrefsKey) == 1, save: false);
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyTerrainSettings();

    static Preset DetectDefault()
    {
        int vram = SystemInfo.graphicsMemorySize; // MB
        if (vram > 0 && vram < 3000) return Preset.Low;
        if (vram > 0 && vram < 6000) return Preset.Medium;
        return Preset.High;
    }

    public void Apply(Preset preset, bool save = true)
    {
        Current = preset;
        int level = System.Array.IndexOf(QualitySettings.names, preset.ToString());
        if (level >= 0)
            QualitySettings.SetQualityLevel(level, true);
        else
            Debug.LogWarning($"GraphicsQualityManager: no quality level named '{preset}' in Project Settings > Quality.");

        if (save)
        {
            PlayerPrefs.SetInt(PrefsKey, (int)preset);
            PlayerPrefs.Save();
        }

        ApplyTerrainSettings();
        if (buttonLabel != null) buttonLabel.text = "Graphics: " + preset;
        PresetChanged?.Invoke(preset);

        SetVolumetricFog(preset == Preset.Ultra, save);
    }

    // God Rays / volumetric mist on or off (independent of the preset once the player sets it).
    public void SetVolumetricFog(bool on, bool save = true)
    {
        VolumetricFog = on;
        if (save)
        {
            PlayerPrefs.SetInt(FogPrefsKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }
        SceneVolumetricFog.Refresh();
        VolumetricFogChanged?.Invoke(on);
    }

    public void ToggleVolumetricFog() => SetVolumetricFog(!VolumetricFog);

    // Settings that live on the Terrain component rather than in the quality level.
    void ApplyTerrainSettings()
    {
        foreach (Terrain t in Terrain.activeTerrains)
        {
            t.drawInstanced = true;
            t.shadowCastingMode = Current == Preset.Low ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }
    }

    // Wire this to a pause-menu button: cycles Low -> Medium -> High -> Ultra -> Low.
    public void OnCyclePressed() => Apply((Preset)(((int)Current + 1) % 4));
}
