using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Put this on the "Collection" button (main menu + pause menu Settings).
// Opens the Collection screen (CollectionMenu). No OnClick wiring needed.
[RequireComponent(typeof(Button))]
public class CollectionButton : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private TMP_FontAsset bodyFont;
    [Tooltip("Rounded panel sprite (UI/Skin/UISprite)")]
    [SerializeField] private Sprite panelSprite;

    private Button button;
    private CollectionMenu open;

    private void Awake() => button = GetComponent<Button>();
    private void OnEnable() => button.onClick.AddListener(OnClicked);
    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClicked);
        if (open != null) open.Close(); // settings closed - take the screen with it
    }

    private void OnClicked()
    {
        if (open == null) open = CollectionMenu.Open(titleFont, bodyFont, panelSprite);
    }
}
