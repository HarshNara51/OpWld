using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

// Two-stage bomb defuse. Builds its own UI at runtime - just put this on
// an empty GameObject, wire DefuseTrigger's On Use to Open, and wire
// On Solved to BombSystem.Defuse + Mission5Manager.OnCarSaved.
//
// STAGE 1 - The bomb's screen flashes a code once. Type it back on the
//           keypad (mouse or number keys). Wrong code = time penalty,
//           and the code flashes again.
// STAGE 2 - A blinking LED + a note taped to the bomb with one rule per
//           LED color. Find the rule for the LED you see and cut ONE wire.
//           Repeats for several rounds (new wires + new LED each time,
//           same note). Wrong wire = BOOM (or a time penalty).
//
// The bomb's timer KEEPS RUNNING while you work. Q backs off.
public class BombDefusePuzzle : MonoBehaviour
{
    [Header("Stage 1 - keypad code")]
    [Range(3, 6)][SerializeField] private int codeLength = 4;
    [SerializeField] private float digitShowTime = 0.7f;
    [SerializeField] private float digitGapTime = 0.25f;
    [SerializeField] private float wrongCodePenalty = 10f;

    [Header("Stage 2 - wires")]
    [Range(3, 6)][SerializeField] private int wireCount = 5;
    [Tooltip("How many correct wires in a row are needed (new wires + LED each round)")]
    [Range(1, 3)][SerializeField] private int wireRounds = 3;
    [SerializeField] private bool wrongWireExplodes = true;
    [Tooltip("Only used if Wrong Wire Explodes is off")]
    [SerializeField] private float wrongWirePenalty = 20f;

    [Header("Look (optional - swap in fancy art later)")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private TMP_FontAsset font;

    [Header("Sounds (optional)")]
    [SerializeField] private AudioClip keySound;
    [SerializeField] private AudioClip codeBeepSound;
    [SerializeField] private AudioClip wrongSound;
    [SerializeField] private AudioClip wireCutSound;
    [SerializeField] private AudioClip defusedSound;

    [Tooltip("Fires once defused - wire to BombSystem.Defuse and Mission5Manager.OnCarSaved")]
    public UnityEvent onSolved;

    public bool IsOpen { get; private set; }

    private enum Stage { ShowingCode, EnterCode, Wires, Done }
    private Stage stage;

    // ---- runtime UI ----
    private GameObject root;
    private GameObject keypadGroup;
    private GameObject wiresGroup;
    private TMP_Text titleText;
    private TMP_Text timerText;
    private TMP_Text hintText;
    private TMP_Text displayText;
    private TMP_Text rulesText;
    private Image led;
    private readonly List<Image> wireImages = new List<Image>();
    private readonly List<Button> wireButtons = new List<Button>();
    private readonly List<TMP_Text> wireMarks = new List<TMP_Text>();

    // ---- puzzle state ----
    private string code = "";
    private string typed = "";
    private int[] wires;
    private bool[] cut;
    private int correctWire;
    private int ledIndex;
    private int round;
    private Rule[] rules;
    private float savedTimeScale = 1f;

    private static readonly string[] WireNames = { "RED", "BLUE", "YELLOW", "GREEN", "WHITE", "BLACK" };
    private static readonly Color[] WireColors =
    {
        new Color(0.85f, 0.15f, 0.15f), new Color(0.2f, 0.4f, 0.95f), new Color(0.95f, 0.85f, 0.2f),
        new Color(0.2f, 0.75f, 0.3f), new Color(0.95f, 0.95f, 0.95f), new Color(0.15f, 0.15f, 0.15f)
    };
    private static readonly string[] LedNames = { "RED", "GREEN", "BLUE", "YELLOW" };
    private static readonly Color[] LedColors =
    {
        new Color(1f, 0.15f, 0.15f), new Color(0.2f, 1f, 0.3f), new Color(0.25f, 0.5f, 1f), new Color(1f, 0.9f, 0.2f)
    };

    // 0 = LAST wire of a color, 1 = FIRST wire of a color, 2 = wire number N, 3 = the ONLY wire of a color
    private struct Rule { public int kind; public int color; public int n; }

    // ================= open / close =================

    // Wire DefuseTrigger's On Use to this
    public void Open()
    {
        if (IsOpen) return;

        BombSystem bomb = BombSystem.Instance;
        if (bomb == null || !bomb.IsArmed)
        {
            NotePopup.Show("There's nothing to defuse right now.", 2.5f);
            return;
        }

        if (root == null) BuildUI();
        root.SetActive(true);
        IsOpen = true;

        savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;                 // the world freezes...
        bomb.SetDefusing(true);              // ...but the bomb keeps ticking
        PauseManager.InputBlocked = true;    // Esc can't open the pause menu mid-defuse
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StartStage1();
    }

    private void Close()
    {
        IsOpen = false;
        StopAllCoroutines();
        if (root != null) root.SetActive(false);

        Time.timeScale = savedTimeScale;
        if (BombSystem.Instance != null) BombSystem.Instance.SetDefusing(false);
        PauseManager.InputBlocked = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ================= update =================

    private void Update()
    {
        if (!IsOpen) return;

        BombSystem bomb = BombSystem.Instance;

        // Ran out of time (or something else set it off) while defusing
        if ((bomb == null || !bomb.IsArmed) && stage != Stage.Done)
        {
            Close();
            return;
        }

        if (bomb != null && timerText != null)
        {
            float t = Mathf.Max(0f, bomb.Remaining);
            timerText.text = $"{Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";
        }

        if (stage == Stage.Wires && led != null)
        {
            bool lit = (Time.unscaledTime * 2f) % 1f < 0.6f;
            led.color = lit ? LedColors[ledIndex] : LedColors[ledIndex] * 0.25f;
        }

        if (stage != Stage.Done && GameKeys.Down(GameAction.BackOff))
        {
            Close();
            NotePopup.Show("You back off from the bomb... the clock's still ticking!", 3f);
            return;
        }

        if (stage == Stage.EnterCode) HandleKeyboard();
    }

    private void HandleKeyboard()
    {
        for (int d = 0; d <= 9; d++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d))
            {
                PressDigit(d);
                return;
            }
        }
        if (Input.GetKeyDown(KeyCode.Backspace)) PressClear();
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) PressEnter();
    }

    // ================= stage 1: code =================

    private void StartStage1()
    {
        stage = Stage.ShowingCode;
        typed = "";
        code = "";
        for (int i = 0; i < codeLength; i++) code += UnityEngine.Random.Range(0, 10).ToString();

        keypadGroup.SetActive(true);
        wiresGroup.SetActive(false);
        titleText.text = "STEP 1 / 2  -  Memorize the code, then enter it";
        hintText.text = $"Type with the keypad or number keys   |   Backspace: clear   |   Enter: confirm   |   {GameKeys.Label(GameAction.BackOff)}: back off";

        StartCoroutine(ShowCode(0.8f));
    }

    private IEnumerator ShowCode(float startDelay)
    {
        stage = Stage.ShowingCode;
        displayText.color = new Color(0.3f, 1f, 0.4f);
        displayText.text = "WATCH";
        yield return new WaitForSecondsRealtime(startDelay);

        foreach (char c in code)
        {
            displayText.text = c.ToString();
            Play(codeBeepSound);
            yield return new WaitForSecondsRealtime(digitShowTime);
            displayText.text = "";
            yield return new WaitForSecondsRealtime(digitGapTime);
        }

        stage = Stage.EnterCode;
        typed = "";
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        displayText.color = new Color(0.3f, 1f, 0.4f);
        string s = "";
        for (int i = 0; i < codeLength; i++) s += (i < typed.Length ? typed[i].ToString() : "_") + " ";
        displayText.text = s.TrimEnd();
    }

    private void PressDigit(int d)
    {
        if (stage != Stage.EnterCode || typed.Length >= codeLength) return;
        typed += d.ToString();
        Play(keySound);
        RefreshDisplay();
    }

    private void PressClear()
    {
        if (stage != Stage.EnterCode) return;
        typed = "";
        Play(keySound);
        RefreshDisplay();
    }

    private void PressEnter()
    {
        if (stage != Stage.EnterCode) return;

        if (typed == code)
        {
            Play(codeBeepSound);
            StartStage2();
            return;
        }

        Play(wrongSound);
        if (BombSystem.Instance != null) BombSystem.Instance.ApplyPenalty(wrongCodePenalty);
        StartCoroutine(WrongCode());
    }

    private IEnumerator WrongCode()
    {
        stage = Stage.ShowingCode;
        displayText.color = new Color(1f, 0.3f, 0.3f);
        displayText.text = $"WRONG  -{wrongCodePenalty:0}s";
        yield return new WaitForSecondsRealtime(1.2f);
        yield return ShowCode(0.4f); // flash the same code again
    }

    // ================= stage 2: wires =================

    private void StartStage2()
    {
        stage = Stage.Wires;
        keypadGroup.SetActive(false);
        wiresGroup.SetActive(true);
        hintText.text = $"Click a wire to cut it   |   {GameKeys.Label(GameAction.BackOff)}: back off";
        round = 1;
        ledIndex = -1;
        GenerateRules();
        GenerateRound();
    }

    // The note: one rule per LED color, reshuffled every attempt
    private void GenerateRules()
    {
        // One rule per LED color, shuffled every attempt
        int[] kinds = { 0, 1, 2, 3 };
        for (int i = kinds.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
        }

        rules = new Rule[4];
        for (int i = 0; i < 4; i++)
        {
            rules[i] = new Rule
            {
                kind = kinds[i],
                color = UnityEngine.Random.Range(0, WireNames.Length),
                n = UnityEngine.Random.Range(2, wireCount + 1)
            };
        }

        string note = "<b>NOTE TAPED TO THE BOMB</b>\n\n";
        for (int i = 0; i < 4; i++)
        {
            string hex = ColorUtility.ToHtmlStringRGB(LedColors[i] * 0.7f);
            note += $"<color=#{hex}><b>{LedNames[i]} LED</b></color>:  {RuleText(rules[i])}\n\n";
        }
        note += "<size=80%>(Wires are numbered from the top.)</size>";
        rulesText.text = note;
    }

    // Fresh wires + a different LED color for each round
    private void GenerateRound()
    {
        titleText.text = wireRounds > 1
            ? $"STEP 2 / 2  -  Wire {round} of {wireRounds}:  check the LED, read the note, cut ONE"
            : "STEP 2 / 2  -  Check the LED, read the note, cut ONE wire";

        int previousLed = ledIndex;
        do { ledIndex = UnityEngine.Random.Range(0, 4); } while (ledIndex == previousLed);
        Rule active = rules[ledIndex];

        wires = new int[wireCount];
        for (int i = 0; i < wireCount; i++) wires[i] = UnityEngine.Random.Range(0, WireNames.Length);

        // Make sure the active rule actually has an answer
        if (active.kind == 0 || active.kind == 1)
        {
            if (Array.IndexOf(wires, active.color) < 0) wires[UnityEngine.Random.Range(0, wireCount)] = active.color;
        }
        else if (active.kind == 3)
        {
            for (int i = 0; i < wireCount; i++)
            {
                while (wires[i] == active.color) wires[i] = UnityEngine.Random.Range(0, WireNames.Length);
            }
            wires[UnityEngine.Random.Range(0, wireCount)] = active.color;
        }

        switch (active.kind)
        {
            case 0: correctWire = Array.LastIndexOf(wires, active.color); break;
            case 1: correctWire = Array.IndexOf(wires, active.color); break;
            case 2: correctWire = active.n - 1; break;
            default: correctWire = Array.IndexOf(wires, active.color); break;
        }

        cut = new bool[wireCount];
        for (int i = 0; i < wireImages.Count; i++)
        {
            bool used = i < wireCount;
            wireImages[i].gameObject.SetActive(used);
            if (!used) continue;
            wireImages[i].color = WireColors[wires[i]];
            wireButtons[i].interactable = true;
            wireMarks[i].text = "";
        }
    }

    private static string RuleText(Rule r)
    {
        switch (r.kind)
        {
            case 0: return $"cut the LAST {WireNames[r.color]} wire";
            case 1: return $"cut the FIRST {WireNames[r.color]} wire";
            case 2: return $"cut wire number {r.n}";
            default: return $"cut the only {WireNames[r.color]} wire";
        }
    }

    private void CutWire(int index)
    {
        if (stage != Stage.Wires || index >= wireCount || cut[index]) return;

        cut[index] = true;
        Play(wireCutSound);
        Color c = wireImages[index].color;
        wireImages[index].color = new Color(c.r, c.g, c.b, 0.25f);
        wireButtons[index].interactable = false;
        wireMarks[index].text = "X  CUT";

        if (index == correctWire)
        {
            if (round >= wireRounds) StartCoroutine(Defused());
            else StartCoroutine(NextRound());
            return;
        }

        Play(wrongSound);
        if (wrongWireExplodes)
        {
            Close();
            if (BombSystem.Instance != null) BombSystem.Instance.ExplodeNow();
        }
        else
        {
            if (BombSystem.Instance != null) BombSystem.Instance.ApplyPenalty(wrongWirePenalty);
            hintText.text = $"<color=#ff6666>Wrong wire!  -{wrongWirePenalty:0}s</color>   |   Click a wire to cut it   |   {GameKeys.Label(GameAction.BackOff)}: back off";
        }
    }

    private IEnumerator NextRound()
    {
        stage = Stage.ShowingCode; // blocks clicks during the short pause
        Play(codeBeepSound);
        titleText.text = $"<color=#66ff77>Correct!</color>  The LED is changing...";
        yield return new WaitForSecondsRealtime(1f);

        round++;
        stage = Stage.Wires;
        GenerateRound();
    }

    private IEnumerator Defused()
    {
        stage = Stage.Done;
        Play(defusedSound);
        titleText.text = "<color=#66ff77>DEFUSED</color>";
        hintText.text = "";
        led.color = new Color(0.2f, 1f, 0.3f);
        yield return new WaitForSecondsRealtime(1.5f);

        Close();
        onSolved?.Invoke();
    }

    // ================= helpers =================

    private static void Play(AudioClip clip)
    {
        if (clip != null && AudioManager.Instance != null) AudioManager.Instance.PlayUI(clip);
    }

    private void BuildUI()
    {
        root = new GameObject("BombDefuseUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image dim = MakeImage("Dim", root.transform, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
        dim.rectTransform.anchorMin = Vector2.zero;
        dim.rectTransform.anchorMax = Vector2.one;
        dim.rectTransform.offsetMin = Vector2.zero;
        dim.rectTransform.offsetMax = Vector2.zero;

        Image panel = MakeImage("Panel", root.transform, new Vector2(1240f, 780f), Vector2.zero,
                                new Color(0.08f, 0.09f, 0.1f, 0.97f), panelSprite);

        titleText = MakeText("Title", panel.transform, new Vector2(900f, 60f), new Vector2(-120f, 335f), 32, Color.white);
        timerText = MakeText("Timer", panel.transform, new Vector2(240f, 60f), new Vector2(480f, 335f), 40,
                             new Color(1f, 0.3f, 0.3f));
        hintText = MakeText("Hint", panel.transform, new Vector2(1180f, 50f), new Vector2(0f, -350f), 22,
                            new Color(0.75f, 0.75f, 0.75f));

        // ---- keypad ----
        keypadGroup = MakeGroup("Keypad", panel.transform);
        Image screen = MakeImage("Screen", keypadGroup.transform, new Vector2(560f, 130f), new Vector2(0f, 200f),
                                 new Color(0.02f, 0.05f, 0.02f));
        displayText = MakeText("Display", screen.transform, new Vector2(540f, 120f), Vector2.zero, 72,
                               new Color(0.3f, 1f, 0.4f));

        string[,] keys = { { "1", "2", "3" }, { "4", "5", "6" }, { "7", "8", "9" }, { "CLR", "0", "OK" } };
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                string label = keys[row, col];
                Vector2 pos = new Vector2(-135f + col * 135f, 50f - row * 100f);
                Color c = label == "OK" ? new Color(0.15f, 0.5f, 0.2f)
                        : label == "CLR" ? new Color(0.5f, 0.2f, 0.15f)
                        : new Color(0.25f, 0.27f, 0.3f);

                Action action;
                if (label == "OK") action = PressEnter;
                else if (label == "CLR") action = PressClear;
                else { int digit = int.Parse(label); action = () => PressDigit(digit); }

                MakeButton(label, keypadGroup.transform, new Vector2(120f, 85f), pos, c, action);
            }
        }

        // ---- wires ----
        wiresGroup = MakeGroup("Wires", panel.transform);
        MakeImage("BombBox", wiresGroup.transform, new Vector2(640f, 560f), new Vector2(-270f, -10f),
                  new Color(0.18f, 0.16f, 0.14f));

        for (int i = 0; i < 6; i++)
        {
            float y = 210f - i * 85f;
            MakeText($"Num{i + 1}", wiresGroup.transform, new Vector2(50f, 40f), new Vector2(-555f, y), 28, Color.white);
            wiresGroup.transform.Find($"Num{i + 1}").GetComponent<TMP_Text>().text = (i + 1).ToString();

            int index = i;
            Button b = MakeButton("", wiresGroup.transform, new Vector2(500f, 26f), new Vector2(-270f, y),
                                  Color.white, () => CutWire(index));
            Image img = b.GetComponent<Image>();
            TMP_Text mark = b.GetComponentInChildren<TMP_Text>();
            mark.fontSize = 22;
            mark.color = new Color(1f, 0.4f, 0.4f);

            wireButtons.Add(b);
            wireImages.Add(img);
            wireMarks.Add(mark);
        }

        led = MakeImage("LED", wiresGroup.transform, new Vector2(80f, 80f), new Vector2(330f, 220f), Color.white);
        MakeText("LEDLabel", wiresGroup.transform, new Vector2(200f, 40f), new Vector2(330f, 160f), 24, Color.white)
            .text = "LED";

        Image paper = MakeImage("Note", wiresGroup.transform, new Vector2(470f, 380f), new Vector2(330f, -60f),
                                new Color(0.93f, 0.9f, 0.78f));
        rulesText = MakeText("Rules", paper.transform, new Vector2(430f, 350f), Vector2.zero, 22,
                             new Color(0.12f, 0.1f, 0.08f), TextAlignmentOptions.TopLeft);

        root.SetActive(false);
    }

    private GameObject MakeGroup(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(1240f, 780f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    private Image MakeImage(string name, Transform parent, Vector2 size, Vector2 pos, Color color, Sprite sprite = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Image img = go.GetComponent<Image>();
        img.color = color;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
        }
        return img;
    }

    private TMP_Text MakeText(string name, Transform parent, Vector2 size, Vector2 pos, float fontSize, Color color,
                              TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    private Button MakeButton(string label, Transform parent, Vector2 size, Vector2 pos, Color color, Action onClick)
    {
        Image img = MakeImage($"Btn_{label}", parent, size, pos, color, buttonSprite);
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(() =>
        {
            onClick();
            // Don't let Enter/Space "re-click" the last button pressed
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        });

        TMP_Text t = MakeText("Label", img.transform, size, Vector2.zero, 34, Color.white);
        t.text = label;
        return b;
    }
}