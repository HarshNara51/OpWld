using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

// Controls page for the Settings screen. Builds its own UI at runtime
// (like BombDefusePuzzle), so there's nothing to wire up by hand.
//
// Put this on a Settings panel - it's on both the main menu's and the
// pause menu's. It adds a "Controls" button (a copy of the Settings
// Back button) and opens a full-screen page listing every control.
// Esc on the page goes back to Settings (MainMenuController and
// PauseManager ask CloseOpenPage() first).
//
// When controls change, edit the Columns list below - that's the only
// place the page's text comes from.
public class ControlsMenu : MonoBehaviour
{
    [Tooltip("The Settings panel's Back button - its look is copied for the new buttons. Found by name if left empty.")]
    [SerializeField] private Button settingsBackButton;

    [Tooltip("Optional - your own Controls button. Left empty, one is created at the bottom centre.")]
    [SerializeField] private Button controlsButton;

    [Header("Look")]
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.97f);
    [SerializeField] private Color headerColor = new Color(1f, 0.74f, 0.3f);
    [SerializeField] private Color textColor = new Color(0.92f, 0.93f, 0.95f);
    [SerializeField] private Color dimTextColor = new Color(0.6f, 0.62f, 0.66f);
    [SerializeField] private Color keyColor = new Color(0.9f, 0.91f, 0.93f);
    [SerializeField] private Color keyTextColor = new Color(0.08f, 0.09f, 0.11f);

    // ================= the controls list =================

    private struct Row
    {
        public string action;
        public bool hold;
        public string[] keys; // "/" between keys = "either one"

        public Row(string action, bool hold, string[] keys)
        {
            this.action = action;
            this.hold = hold;
            this.keys = keys;
        }
    }

    private struct Section
    {
        public string title;
        public Row[] rows;

        public Section(string title, params Row[] rows)
        {
            this.title = title;
            this.rows = rows;
        }
    }

    private static Row Press(string action, params string[] keys) => new Row(action, false, keys);
    private static Row Hold(string action, params string[] keys) => new Row(action, true, keys);

    private static readonly Section[][] Columns =
    {
        new[]
        {
            new Section("On foot",
                Press("Move", "W", "A", "S", "D"),
                Hold("Sprint", "Shift"),
                Press("Jump", "Space"),
                Press("Crouch", "C", "/", "Ctrl"),
                Press("Look around", "Mouse")),
            new Section("Vehicles",
                Press("Enter / exit car", "F"),
                Press("Accelerate / reverse", "W", "/", "S"),
                Press("Steer", "A", "/", "D"),
                Press("Handbrake", "Space"),
                Press("Reset car", "R"),
                Press("Summon your car", "V")),
        },
        new[]
        {
            new Section("Interact",
                Press("Interact / pick up / use", "E"),
                Press("Start mission", "E"),
                Hold("Return home", "E"),
                Hold("Call the cops", "E")),
            new Section("Combat & gadgets",
                Press("Rifle: equip / holster", "1"),
                Press("Knife: equip / holster", "2"),
                Press("Fire / stab", "Left Click"),
                Press("Reload", "R"),
                Hold("EMP device", "G")),
        },
        new[]
        {
            new Section("Puzzles",
                Press("Back off", "Q"),
                Press("Lockpick: move pick", "Mouse"),
                Hold("Lockpick: turn lock", "D"),
                Press("Timing: hit the zone", "Space"),
                Press("Bomb: enter code", "0-9", "/", "Click"),
                Press("Bomb: clear / confirm", "Backspace", "/", "Enter"),
                Press("Bomb: cut a wire", "Left Click")),
            new Section("General",
                Press("Pause", "Esc"),
                Press("Photo mode", "P"),
                Press("Photo: zoom", "Scroll"),
                Press("Photo: take picture", "Left Click")),
        },
    };

    // ================= runtime =================

    private const float ContentWidth = 1560f;
    private const float ContentHeight = 800f;

    private static ControlsMenu openMenu;

    private GameObject page;
    private RectTransform content;
    private TMP_FontAsset font;
    private Sprite keySprite;

    // Called by MainMenuController / PauseManager on Esc. Returns true if
    // a Controls page was open (and is now closed), so Esc stops there.
    public static bool CloseOpenPage()
    {
        if (openMenu == null) return false;
        openMenu.Close();
        return true;
    }

    private void Awake()
    {
        if (settingsBackButton == null)
        {
            Transform t = transform.Find("BackButton");
            if (t != null) settingsBackButton = t.GetComponent<Button>();
        }
        if (settingsBackButton == null)
        {
            Debug.LogWarning("ControlsMenu: no Back button to copy - assign Settings Back Button.", this);
            return;
        }

        TMP_Text backLabel = settingsBackButton.GetComponentInChildren<TMP_Text>(true);
        font = backLabel != null && backLabel.font != null ? backLabel.font : TMP_Settings.defaultFontAsset;
        keySprite = settingsBackButton.image != null ? settingsBackButton.image.sprite : null;

        Vector2 backPos = ((RectTransform)settingsBackButton.transform).anchoredPosition;

        if (controlsButton == null)
            controlsButton = CopyBackButton(transform, "ControlsButton", "Controls", new Vector2(0f, backPos.y), Open);
        else
            controlsButton.onClick.AddListener(Open);

        BuildPage(backPos);
        page.SetActive(false);
    }

    // Settings got hidden (Back, Esc, unpause...) - next time it opens on Settings, not Controls
    private void OnDisable()
    {
        if (page != null) page.SetActive(false);
        if (openMenu == this) openMenu = null;
    }

    public void Open()
    {
        if (page == null) return;
        page.SetActive(true);
        page.transform.SetAsLastSibling();
        openMenu = this;
        FitToScreen();
    }

    public void Close()
    {
        if (page != null) page.SetActive(false);
        if (openMenu == this) openMenu = null;
    }

    private void LateUpdate()
    {
        if (openMenu == this) FitToScreen();
    }

    // Shrinks/grows the page to fit the canvas (the pause canvas is constant pixel size)
    private void FitToScreen()
    {
        Rect r = ((RectTransform)page.transform).rect;
        float s = Mathf.Min(r.width / (ContentWidth + 120f), r.height / (ContentHeight + 260f));
        content.localScale = Vector3.one * Mathf.Clamp(s, 0.4f, 1.6f);
    }

    // ================= building the page =================

    private void BuildPage(Vector2 backPos)
    {
        page = new GameObject("ControlsPage", typeof(RectTransform), typeof(Image));
        RectTransform pageRt = (RectTransform)page.transform;
        pageRt.SetParent(transform, false);
        Stretch(pageRt);
        page.GetComponent<Image>().color = backgroundColor; // also blocks clicks to Settings underneath

        content = NewRect("Content", pageRt);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(ContentWidth, ContentHeight);
        content.anchoredPosition = new Vector2(0f, 30f);

        TMP_Text title = NewText("Title", content, "CONTROLS", 56, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
        TopStrip(title.rectTransform, 0f, 70f);

        TMP_Text subtitle = NewText("Subtitle", content, "Keyboard & mouse", 24, FontStyles.Normal, dimTextColor, TextAlignmentOptions.Center);
        TopStrip(subtitle.rectTransform, 72f, 34f);

        RectTransform columns = NewRect("Columns", content);
        Stretch(columns);
        columns.offsetMax = new Vector2(0f, -140f);
        HorizontalLayoutGroup h = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 70f;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = true;

        foreach (Section[] column in Columns)
        {
            RectTransform col = NewRect("Column", columns);
            VerticalLayoutGroup v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            for (int i = 0; i < column.Length; i++)
            {
                if (i > 0) Spacer(col, 26f);
                BuildSection(col, column[i]);
            }
        }

        CopyBackButton(pageRt, "ControlsBackButton", "Back", backPos, Close);
    }

    private void BuildSection(RectTransform col, Section section)
    {
        TMP_Text header = NewText("Header", col, section.title.ToUpper(), 26, FontStyles.Bold, headerColor, TextAlignmentOptions.BottomLeft);
        header.characterSpacing = 4f;
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

        RectTransform line = NewRect("Line", col);
        Image lineImage = line.gameObject.AddComponent<Image>();
        lineImage.color = new Color(headerColor.r, headerColor.g, headerColor.b, 0.35f);
        lineImage.raycastTarget = false;
        line.gameObject.AddComponent<LayoutElement>().preferredHeight = 2f;

        Spacer(col, 6f);

        foreach (Row row in section.rows) BuildRow(col, row);
    }

    private void BuildRow(RectTransform col, Row row)
    {
        RectTransform rowRt = NewRect("Row", col);
        rowRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;

        TMP_Text label = NewText("Action", rowRt, row.action, 25, FontStyles.Normal, textColor, TextAlignmentOptions.MidlineLeft);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMax = new Vector2(-200f, 0f);
        label.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform keys = NewRect("Keys", rowRt);
        keys.anchorMin = keys.anchorMax = keys.pivot = new Vector2(1f, 0.5f);
        keys.anchoredPosition = Vector2.zero;
        HorizontalLayoutGroup h = keys.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 6f;
        h.childAlignment = TextAnchor.MiddleRight;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        ContentSizeFitter fit = keys.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (row.hold) NewText("Hold", keys, "hold", 21, FontStyles.Italic, dimTextColor, TextAlignmentOptions.MidlineRight);

        foreach (string key in row.keys)
        {
            if (key == "/") NewText("Or", keys, "/", 22, FontStyles.Normal, dimTextColor, TextAlignmentOptions.Center);
            else BuildKeyCap(keys, key);
        }
    }

    private void BuildKeyCap(RectTransform parent, string key)
    {
        RectTransform cap = NewRect("Key", parent);
        Image img = cap.gameObject.AddComponent<Image>();
        img.sprite = keySprite;
        img.type = Image.Type.Sliced;
        img.color = keyColor;
        img.raycastTarget = false;

        HorizontalLayoutGroup h = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(12, 12, 3, 3);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        LayoutElement le = cap.gameObject.AddComponent<LayoutElement>();
        le.minWidth = 40f;
        le.minHeight = 34f;

        NewText("Label", cap, key, 21, FontStyles.Bold, keyTextColor, TextAlignmentOptions.Center);
    }

    // ================= helpers =================

    private Button CopyBackButton(Transform parent, string name, string label, Vector2 position, UnityAction onClick)
    {
        GameObject go = Instantiate(settingsBackButton.gameObject, parent);
        go.name = name;
        go.SetActive(true);
        ((RectTransform)go.transform).anchoredPosition = position;

        Button b = go.GetComponent<Button>();
        b.onClick = new Button.ButtonClickedEvent(); // drop the copied Back wiring
        b.onClick.AddListener(onClick);

        TMP_Text t = go.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.text = label;
        return b;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private TMP_Text NewText(string name, Transform parent, string text, float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private static void Spacer(Transform parent, float height)
    {
        NewRect("Spacer", parent).gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void TopStrip(RectTransform rt, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = new Vector2(0f, height);
    }
}
