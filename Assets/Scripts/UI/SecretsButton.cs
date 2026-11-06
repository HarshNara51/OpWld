using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Put this on the "Secrets" button (main menu + pause menu Settings).
// Opens the Secrets screen (SecretsMenu). No OnClick wiring needed.
[RequireComponent(typeof(Button))]
public class SecretsButton : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private TMP_FontAsset bodyFont;
    [Tooltip("Rounded panel sprite (UI/Skin/UISprite)")]
    [SerializeField] private Sprite panelSprite;

    private Button button;

    private void Awake() => button = GetComponent<Button>();
    private void OnEnable() => button.onClick.AddListener(OnClicked);
    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClicked);
        SecretsMenu.CloseOpenPage(); // settings closed - take the screen with it
    }

    private void OnClicked() => SecretsMenu.Open(titleFont, bodyFont, panelSprite);
}
