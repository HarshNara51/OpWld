using UnityEngine;
using UnityEngine.UI;

// A menu button drawn with a picture that has the text baked in
// (Assets/Imports/image/Menu). Hovering swaps to the highlighted picture;
// while the button can't be pressed (e.g. Continue before there's a save)
// it's greyed out. The button's own text label is hidden.
//
// Remembers the button's old plain look, so copies that need their own
// text (ControlsMenu's "Reset to defaults", the key caps) can go back to it.
[RequireComponent(typeof(Button))]
public class MenuButtonArt : MonoBehaviour
{
    public Sprite normal;
    public Sprite highlighted;

    [Tooltip("Colour while the button can't be pressed")]
    public Color disabledTint = new Color(0.42f, 0.42f, 0.42f, 0.6f);

    [SerializeField, HideInInspector] private Sprite plainSprite;
    [SerializeField, HideInInspector] private Vector2 plainSize;
    public Sprite PlainSprite => plainSprite;

    private Button button;
    private Image image;

    private void Awake()
    {
        button = GetComponent<Button>();
        image = button.image;
    }

    private void LateUpdate()
    {
        if (image == null) return;
        Color want = button.IsInteractable() ? Color.white : disabledTint;
        if (image.color != want) image.color = want;
    }

    // Set up the button with this art (used by the editor setup, and safe at runtime)
    public void Apply(Sprite normalSprite, Sprite highlightedSprite)
    {
        Button b = GetComponent<Button>();
        Image img = b.image;
        if (img == null) return;
        if (plainSprite == null && img.sprite != normalSprite)
        {
            plainSprite = img.sprite;
            plainSize = ((RectTransform)transform).sizeDelta;
        }

        normal = normalSprite;
        highlighted = highlightedSprite;
        img.sprite = normal;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.color = Color.white;

        b.transition = Selectable.Transition.SpriteSwap;
        b.spriteState = new SpriteState
        {
            highlightedSprite = highlighted,
            pressedSprite = highlighted,
            selectedSprite = null, // after a click it goes back to normal, not stuck lit
            disabledSprite = null,
        };
    }

    // Back to the old plain button with its own text (for copies that say something else)
    public void RemoveArt()
    {
        Button b = GetComponent<Button>();
        if (b.image != null)
        {
            b.image.sprite = plainSprite;
            b.image.type = Image.Type.Sliced;
            b.image.preserveAspect = false;
            b.image.color = Color.white;
        }
        b.transition = Selectable.Transition.ColorTint;
        if (plainSize != Vector2.zero) ((RectTransform)transform).sizeDelta = plainSize;
        foreach (TMPro.TMP_Text t in GetComponentsInChildren<TMPro.TMP_Text>(true)) t.gameObject.SetActive(true);
        if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
    }
}
