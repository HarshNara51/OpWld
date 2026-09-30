using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Put this on any "Graphics" button - main menu, pause menu, anywhere.
// It finds the live GraphicsQualityManager at runtime (it lives on the
// Managers prefab), cycles Low -> Medium -> High on click, and keeps
// its own label in sync. No OnClick wiring needed.
[RequireComponent(typeof(Button))]
public class GraphicsButton : MonoBehaviour
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
        GraphicsQualityManager.PresetChanged += UpdateLabel;

        // Show the current preset whenever the settings panel opens
        if (GraphicsQualityManager.Instance != null)
        {
            UpdateLabel(GraphicsQualityManager.Instance.Current);
        }
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClicked);
        GraphicsQualityManager.PresetChanged -= UpdateLabel;
    }

    private void OnClicked()
    {
        if (GraphicsQualityManager.Instance != null)
        {
            GraphicsQualityManager.Instance.OnCyclePressed();
        }
    }

    private void UpdateLabel(GraphicsQualityManager.Preset preset)
    {
        if (label != null) label.text = "Graphics: " + preset;
    }
}
