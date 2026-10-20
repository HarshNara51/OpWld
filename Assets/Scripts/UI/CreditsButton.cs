using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Put this on the "Credits" button (main menu + pause menu Settings).
// Opens the scrolling Credits screen (CreditsMenu). No OnClick wiring needed.
[RequireComponent(typeof(Button))]
public class CreditsButton : MonoBehaviour
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
        CreditsMenu.CloseOpenPage(); // settings closed - take the screen with it
    }

    private void OnClicked() => CreditsMenu.Open(titleFont, bodyFont, panelSprite);
}
