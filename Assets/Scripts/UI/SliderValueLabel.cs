using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows a slider's value as a percentage ("80%") next to it - used on the volume rows
// of the Settings screens. Put it on the text and drag in the slider.
public class SliderValueLabel : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text label;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (slider == null) return;
        slider.onValueChanged.AddListener(Show);
        Show(slider.value);
    }

    private void OnDisable()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(Show);
    }

    // LateUpdate as well, because VolumeSlider sets the saved value without notifying
    private void LateUpdate()
    {
        if (slider != null) Show(slider.value);
    }

    private void Show(float value)
    {
        if (label == null) return;
        float range = slider.maxValue - slider.minValue;
        int percent = Mathf.RoundToInt(range > 0f ? (value - slider.minValue) / range * 100f : 0f);
        string text = percent + "%";
        if (label.text != text) label.text = text;
    }
}
