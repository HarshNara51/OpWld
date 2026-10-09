using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
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
// Keys tied to a GameAction can be changed: click the key, press the new
// one (Esc cancels). They're saved by GameKeys. "Reset to defaults" puts
// everything back. When an action is added or moved, edit the Columns
// list below - that's the only place the page's layout comes from.
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
        public object[] keys; // GameAction = changeable key, string = fixed label, "/" = "either one"

        public Row(string action, bool hold, object[] keys)
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

    private static Row Press(string action, params object[] keys) => new Row(action, false, keys);
    private static Row Hold(string action, params object[] keys) => new Row(action, true, keys);

    private static readonly Section[][] Columns =
    {
        new[]
        {
            new Section("On foot",
                Press("Move", GameAction.MoveForward, GameAction.MoveLeft, GameAction.MoveBack, GameAction.MoveRight),
                Hold("Sprint", GameAction.Sprint),
                Press("Jump", GameAction.Jump),
                Press("Crouch", GameAction.Crouch),
                Press("Look around", "Mouse")),
            new Section("Vehicles",
                Press("Enter / exit car", GameAction.EnterExitCar),
                Press("Accelerate / reverse", GameAction.Accelerate, "/", GameAction.Reverse),
                Press("Steer", GameAction.SteerLeft, "/", GameAction.SteerRight),
                Hold("Handbrake", GameAction.Handbrake),
                Press("Reset car", GameAction.ResetCar),
                Press("Summon your car", GameAction.SummonCar)),
        },
        new[]
        {
            new Section("Interact",
                Press("Interact / pick up / use", GameAction.Interact),
                Press("Start mission", GameAction.Interact),
                Hold("Return home", GameAction.Interact),
                Hold("Call the cops", GameAction.Interact)),
            new Section("Combat & gadgets",
                Press("Rifle: equip / holster", GameAction.EquipRifle),
                Press("Knife: equip / holster", GameAction.EquipKnife),
                Hold("Aim", GameAction.Aim),
                Press("Fire / stab", GameAction.FireStab),
                Press("Reload", GameAction.Reload),
                Hold("EMP device", GameAction.EMP)),
        },
        new[]
        {
            new Section("Puzzles",
                Press("Back off", GameAction.BackOff),
                Press("Lockpick: move pick", "Mouse"),
                Hold("Lockpick: turn lock", GameAction.TurnLock),
                Press("Timing: hit the zone", GameAction.TimingHit),
                Press("Bomb: enter code", "0-9", "/", "Click"),
                Press("Bomb: clear", "Backspace"),
                Press("Bomb: confirm", "Enter"),
                Press("Bomb: cut a wire", "Left Click")),
            new Section("General",
                Press("Pause", "Esc"),
                Press("Photo mode", GameAction.PhotoMode),
                Press("Photo: zoom", "Scroll"),
                Press("Photo: take picture", GameAction.TakePicture)),
        },
    };

    // ================= runtime =================

    private const float ContentWidth = 1560f;
    private const float ContentHeight = 800f;

    private static ControlsMenu openMenu;

    // Every changeable key cap on the page, so labels can be refreshed
    private struct BoundCap
    {
        public GameAction action;
        public Image image;
        public TMP_Text label;
    }
    private readonly List<BoundCap> boundCaps = new List<BoundCap>();

    // Each row's action label gets whatever width its keys leave free
    private struct RowParts
    {
        public TMP_Text label;
        public RectTransform keys;
    }
    private readonly List<RowParts> rows = new List<RowParts>();
    private TMP_Text status;
    private GameAction? listening;   // waiting for a key for this action
    private int listenStartFrame;
    private static KeyCode[] allKeys;

    private GameObject page;
    private RectTransform content;
    private TMP_FontAsset font;
    private Sprite keySprite;

    // Called by MainMenuController / PauseManager on Esc. Returns true if
    // a Controls page was open (and is now closed), so Esc stops there.
    public static bool CloseOpenPage()
    {
        if (openMenu == null) return false;
        if (openMenu.listening != null) openMenu.StopListening("Cancelled."); // Esc while waiting for a key = cancel
        else openMenu.Close();
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
        FitLabels();
        page.SetActive(false);
        GameKeys.Changed += RefreshKeys;
    }

    private void OnDestroy()
    {
        GameKeys.Changed -= RefreshKeys;
    }

    // Settings got hidden (Back, Esc, unpause...) - next time it opens on Settings, not Controls
    private void OnDisable()
    {
        listening = null;
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
        RefreshKeys();
        SetStatus("Click a key to change it.", false);
    }

    public void Close()
    {
        listening = null;
        if (page != null) page.SetActive(false);
        if (openMenu == this) openMenu = null;
    }

    private void LateUpdate()
    {
        if (openMenu == this) FitToScreen();
    }

    // ================= changing a key =================

    private void StartListening(GameAction action)
    {
        listening = action;
        listenStartFrame = Time.frameCount;
        // Otherwise Space/Enter would "click" the selected key cap again
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        RefreshKeys();
        SetStatus($"Press a key for <b>{GameKeys.ActionName(action)}</b>   (Esc to cancel)", true);
    }

    private void StopListening(string message)
    {
        listening = null;
        RefreshKeys();
        SetStatus(message, false);
    }

    // Update still runs in the pause menu (time stopped), so this works there
    private void Update()
    {
        if (listening == null || Time.frameCount == listenStartFrame) return;

        if (allKeys == null)
        {
            var list = new List<KeyCode>();
            foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
                if (GameKeys.CanBind(k) && !list.Contains(k)) list.Add(k);
            allKeys = list.ToArray();
        }

        foreach (KeyCode k in allKeys)
        {
            if (!Input.GetKeyDown(k)) continue;

            GameAction action = listening.Value;
            listening = null;
            GameAction? swapped = GameKeys.Set(action, k);
            string name = GameKeys.ActionName(action);
            if (swapped != null)
                SetStatus($"<b>{name}</b> is now {GameKeys.KeyName(k)}.   <b>{GameKeys.ActionName(swapped.Value)}</b> took its old key: {GameKeys.Label(swapped.Value)}.", false);
            else
                SetStatus($"<b>{name}</b> is now {GameKeys.KeyName(k)}.", false);
            RefreshKeys();
            return;
        }
    }

    private void ResetToDefaults()
    {
        listening = null;
        GameKeys.ResetAll();
        RefreshKeys();
        SetStatus("All keys are back to their defaults.", false);
    }

    private void RefreshKeys()
    {
        foreach (BoundCap cap in boundCaps)
        {
            bool waiting = listening == cap.action;
            cap.label.text = waiting ? "..." : GameKeys.Label(cap.action);
            cap.image.color = waiting ? headerColor : Color.white;
        }
        FitLabels();
    }

    // Key caps change width when keys change ("E" -> "Middle Click")
    private void FitLabels()
    {
        foreach (RowParts row in rows)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(row.keys);
            row.label.rectTransform.offsetMax = new Vector2(-(row.keys.rect.width + 12f), 0f);
        }
    }

    private void SetStatus(string text, bool highlight)
    {
        if (status == null) return;
        status.text = text;
        status.color = highlight ? headerColor : dimTextColor;
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
        // Fully opaque, so Settings doesn't show through; also blocks clicks to it
        page.GetComponent<Image>().color = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 1f);

        content = NewRect("Content", pageRt);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(ContentWidth, ContentHeight);
        content.anchoredPosition = new Vector2(0f, 30f);

        TMP_Text title = NewText("Title", content, "CONTROLS", 56, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
        TopStrip(title.rectTransform, 0f, 70f);

        TMP_Text subtitle = NewText("Subtitle", content, "Keyboard & mouse  -  click a key to change it", 24, FontStyles.Normal, dimTextColor, TextAlignmentOptions.Center);
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

        status = NewText("Status", content, "", 24, FontStyles.Normal, dimTextColor, TextAlignmentOptions.Center);
        status.rectTransform.anchorMin = new Vector2(0f, 0f);
        status.rectTransform.anchorMax = new Vector2(1f, 0f);
        status.rectTransform.pivot = new Vector2(0.5f, 1f);
        status.rectTransform.anchoredPosition = new Vector2(0f, -10f);
        status.rectTransform.sizeDelta = new Vector2(0f, 36f);

        CopyBackButton(pageRt, "ControlsBackButton", "Back", backPos, Close);
        // Reset sits mirrored on the other side from Back (or beside it if Back is centred)
        Vector2 resetPos = Mathf.Abs(backPos.x) > 150f ? new Vector2(-backPos.x, backPos.y) : backPos + new Vector2(340f, 0f);
        Button reset = CopyBackButton(pageRt, "ResetKeysButton", "Reset to defaults", resetPos, ResetToDefaults);
        TMP_Text resetLabel = reset.GetComponentInChildren<TMP_Text>(true);
        if (resetLabel != null) resetLabel.enableAutoSizing = true;
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
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.enableAutoSizing = true; // long names shrink a little before being cut off
        label.fontSizeMin = 19f;
        label.fontSizeMax = 25f;

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

        foreach (object key in row.keys)
        {
            if (key is GameAction action) BuildKeyCap(keys, GameKeys.Label(action), action);
            else if ((string)key == "/") NewText("Or", keys, "/", 22, FontStyles.Normal, dimTextColor, TextAlignmentOptions.Center);
            else BuildKeyCap(keys, (string)key, null);
        }

        rows.Add(new RowParts { label = label, keys = keys });
    }

    private void BuildKeyCap(RectTransform parent, string key, GameAction? action)
    {
        RectTransform cap = NewRect("Key", parent);
        Image img = cap.gameObject.AddComponent<Image>();
        img.sprite = keySprite;
        img.type = Image.Type.Sliced;
        img.color = keyColor;
        img.raycastTarget = false;

        if (action != null)
        {
            // Changeable: a button. The Button tints the image, so the image itself stays white.
            img.color = Color.white;
            img.raycastTarget = true;
            Button b = cap.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = b.colors;
            colors.normalColor = colors.selectedColor = keyColor;
            colors.highlightedColor = Color.Lerp(keyColor, headerColor, 0.6f);
            colors.pressedColor = headerColor;
            colors.colorMultiplier = 1f;
            b.colors = colors;
            GameAction a = action.Value;
            b.onClick.AddListener(() => StartListening(a));
        }

        HorizontalLayoutGroup h = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(12, 12, 3, 3);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        LayoutElement le = cap.gameObject.AddComponent<LayoutElement>();
        le.minWidth = 40f;
        le.minHeight = 34f;

        TMP_Text label = NewText("Label", cap, key, 21, FontStyles.Bold, keyTextColor, TextAlignmentOptions.Center);
        if (action != null) boundCaps.Add(new BoundCap { action = action.Value, image = img, label = label });
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
