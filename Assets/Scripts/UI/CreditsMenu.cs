using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Credits screen: scrolls Resources/Credits.txt up the screen like film credits.
// Hold left click / Space to speed it up, mouse wheel to scroll by hand,
// Esc / right click / BACK to close. Built in code when opened (CreditsButton)
// and destroyed on close. The text comes from CREDITS.md via
// Tools > Credits > Build Credits Screen Text.
public class CreditsMenu : MonoBehaviour
{
    private static readonly Color Amber = new Color(1f, 0.72f, 0.3f);
    private static readonly Color Body = new Color(0.93f, 0.91f, 0.87f);
    private static readonly Color Dim = new Color(0.6f, 0.6f, 0.65f);

    private const float Speed = 55f;      // canvas units per second
    private const float FastFactor = 5f;

    private static CreditsMenu open;
    private TMP_FontAsset titleFont, bodyFont;
    private Sprite panelSprite;
    private RectTransform content, view;
    private float y;
    private bool built;

    public static CreditsMenu Open(TMP_FontAsset titleFont, TMP_FontAsset bodyFont, Sprite panelSprite)
    {
        if (open != null) return open;
        var go = new GameObject("CreditsMenu", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // above the pause / settings menus
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        open = go.AddComponent<CreditsMenu>();
        open.titleFont = titleFont; open.bodyFont = bodyFont; open.panelSprite = panelSprite;
        open.Build();
        return open;
    }

    // Esc in the pause menu / main menu closes this first (returns true if it did)
    public static bool CloseOpenPage()
    {
        if (open == null) return false;
        open.Close();
        return true;
    }

    public void Close()
    {
        if (open == this) open = null;
        Destroy(gameObject);
    }

    private RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private TextMeshProUGUI Line(string text, TMP_FontAsset font, float size, Color color, float height, float spacing = 0f)
    {
        var r = Rect("Line", content);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSize = size; t.color = color; t.text = text; t.characterSpacing = spacing;
        t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        var le = r.gameObject.AddComponent<LayoutElement>(); le.minHeight = height;
        return t;
    }

    private void Build()
    {
        var bg = Rect("Background", transform); bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
        bg.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.03f, 1f);

        // the text scrolls only between the two amber bars (clear of the hint at the bottom)
        view = Rect("View", transform); view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one; view.offsetMin = new Vector2(0f, 82f); view.offsetMax = new Vector2(0f, -36f);
        view.gameObject.AddComponent<RectMask2D>();

        content = Rect("Content", view);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f); content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(1300f, 0f);
        var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.UpperCenter; v.spacing = 6f;
        v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var asset = Resources.Load<TextAsset>("Credits");
        string[] lines = asset != null ? asset.text.Replace("\r", "").Split('\n') : new[] { "!CREDITS", "~Credits.txt is missing - run Tools > Credits > Build Credits Screen Text" };
        foreach (string l in lines)
        {
            if (l.Length == 0) { Line("", bodyFont, 10f, Body, 26f); continue; }
            switch (l[0])
            {
                case '!': Line(l.Substring(1), titleFont, 120f, Amber, 140f, 14f); break;
                case '#': Line(l.Substring(1), titleFont, 64f, Amber, 80f, 8f); break;
                case '~': Line(l.Substring(1), bodyFont, 24f, Dim, 34f).fontStyle = FontStyles.Italic; break;
                default: Line(l, bodyFont, 30f, Body, 40f); break;
            }
        }

        // Thin amber bars top and bottom, the hint and the BACK button stay put while the text scrolls
        Bar(new Vector2(0f, 1f), -26f); Bar(new Vector2(0f, 0f), 76f);
        var hint = Rect("Hint", transform); hint.anchorMin = new Vector2(0f, 0f); hint.anchorMax = new Vector2(1f, 0f); hint.pivot = new Vector2(0.5f, 0f);
        hint.sizeDelta = new Vector2(0f, 30f); hint.anchoredPosition = new Vector2(0f, 34f);
        var ht = hint.gameObject.AddComponent<TextMeshProUGUI>();
        ht.font = bodyFont; ht.fontSize = 20f; ht.color = Dim; ht.alignment = TextAlignmentOptions.Center; ht.raycastTarget = false;
        ht.text = "Hold left click or Space to speed up  -  scroll to move  -  Esc or right click to close";
        BackButton();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        y = -ViewHeight(); // start just below the bottom of the screen
        content.anchoredPosition = new Vector2(0f, y);
        built = true;
    }

    private void Bar(Vector2 anchor, float yPos)
    {
        var b = Rect("Bar", transform); b.anchorMin = anchor; b.anchorMax = new Vector2(1f, anchor.y); b.pivot = new Vector2(0.5f, anchor.y);
        b.sizeDelta = new Vector2(-120f, 4f); b.anchoredPosition = new Vector2(0f, yPos);
        b.gameObject.AddComponent<Image>().color = Amber;
    }

    private void BackButton()
    {
        var r = Rect("BackButton", transform);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f); r.anchoredPosition = new Vector2(-50f, -40f); r.sizeDelta = new Vector2(170f, 60f);
        var img = r.gameObject.AddComponent<Image>(); img.sprite = panelSprite; img.type = Image.Type.Sliced; img.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = img;
        var cb = b.colors; cb.highlightedColor = new Color(1f, 0.85f, 0.55f); cb.pressedColor = Amber; b.colors = cb;
        b.onClick.AddListener(Close);
        var t = Rect("Label", r); t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one; t.offsetMin = t.offsetMax = Vector2.zero;
        var tx = t.gameObject.AddComponent<TextMeshProUGUI>();
        tx.font = bodyFont; tx.fontSize = 26f; tx.color = Amber; tx.alignment = TextAlignmentOptions.Center; tx.fontStyle = FontStyles.Bold; tx.text = "BACK"; tx.raycastTarget = false;
    }

    private float ViewHeight() => view.rect.height > 1f ? view.rect.height : 1080f;

    private void Update()
    {
        if (!built) return;
        if (Input.GetMouseButtonDown(1)) { Close(); return; }

        // Unscaled time: the pause menu stops game time
        bool fast = Input.GetKey(KeyCode.Space) || (Input.GetMouseButton(0) && !OverBackButton());
        y += Speed * (fast ? FastFactor : 1f) * Time.unscaledDeltaTime;
        y -= Input.mouseScrollDelta.y * 120f;

        // Stop when the last line ("Thank you for playing") reaches the middle of the screen
        float end = content.rect.height - ViewHeight() * 0.5f - 70f;
        y = Mathf.Clamp(y, -ViewHeight(), Mathf.Max(end, 0f));
        content.anchoredPosition = new Vector2(0f, y);
    }

    private bool OverBackButton()
    {
        var back = transform.Find("BackButton") as RectTransform;
        return back != null && RectTransformUtility.RectangleContainsScreenPoint(back, Input.mousePosition, null);
    }
}
