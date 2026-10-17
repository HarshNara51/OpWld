using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Put this on any "Show FPS" button - main menu, pause menu, anywhere.
// Toggles the FPS counter (FPSMonitor.Show, saved) and keeps its label in sync.
// No OnClick wiring needed.
[RequireComponent(typeof(Button))]
public class ShowFpsButton : MonoBehaviour
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
        FPSMonitor.ShowChanged += UpdateLabel;
        UpdateLabel(FPSMonitor.Show);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClicked);
        FPSMonitor.ShowChanged -= UpdateLabel;
    }

    private void OnClicked() => FPSMonitor.Show = !FPSMonitor.Show;

    private void UpdateLabel(bool on)
    {
        if (label != null) label.text = "Show FPS: " + (on ? "On" : "Off");
    }
}
