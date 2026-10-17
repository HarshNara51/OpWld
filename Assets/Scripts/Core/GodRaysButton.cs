using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Put this on any "God Rays" button - main menu, pause menu, anywhere.
// Like GraphicsButton: finds the live GraphicsQualityManager at runtime, toggles
// volumetric fog / god rays on click, and keeps its own label in sync. No OnClick wiring needed.
[RequireComponent(typeof(Button))]
public class GodRaysButton : MonoBehaviour
{
    [Tooltip("The button's text - leave empty to find it automatically")]
    [SerializeField] private TMP_Text label;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
    }

    private void OnEnable()
    {
        button.onClick.AddListener(OnClicked);
        GraphicsQualityManager.VolumetricFogChanged += UpdateLabel;

        if (GraphicsQualityManager.Instance != null)
        {
            UpdateLabel(GraphicsQualityManager.Instance.VolumetricFog);
        }
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClicked);
        GraphicsQualityManager.VolumetricFogChanged -= UpdateLabel;
    }

    private void OnClicked()
    {
        if (GraphicsQualityManager.Instance != null)
        {
            GraphicsQualityManager.Instance.ToggleVolumetricFog();
        }
    }

    private void UpdateLabel(bool on)
    {
        if (label == null) return;
        string state = on ? "ON" : "OFF";
        label.text = TryGetComponent(out MenuButtonArt _) ? state : "God Rays: " + (on ? "On" : "Off");
    }
}
