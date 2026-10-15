using UnityEngine;
using UnityEngine.UI;

// Put this on any UI Slider to make it a volume control. Pick the
// channel in the Inspector. Works in the main menu AND the pause menu -
// both read/write the same saved values through AudioManager.
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    public enum Channel { Master, Music, SFX, Ambience, UI }

    [SerializeField] private Channel channel = Channel.Master;

    private Slider slider;

    private string Parameter
    {
        get
        {
            switch (channel)
            {
                case Channel.Music: return AudioManager.MusicVolume;
                case Channel.SFX: return AudioManager.SFXVolume;
                case Channel.Ambience: return AudioManager.AmbienceVolume;
                case Channel.UI: return AudioManager.UIVolume;
                default: return AudioManager.MasterVolume;
            }
        }
    }

    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
    }

    private void OnEnable()
    {
        // Show the saved value whenever the settings panel opens
        if (AudioManager.Instance != null)
        {
            slider.SetValueWithoutNotify(AudioManager.Instance.GetVolume(Parameter));
        }
        slider.onValueChanged.AddListener(OnChanged);
    }

    private void OnDisable()
    {
        slider.onValueChanged.RemoveListener(OnChanged);
    }

    private void OnChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetVolume(Parameter, value);
        }
    }
}
