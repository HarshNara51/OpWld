using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.UI;

// Put this on an empty GameObject in each mission scene, alongside a
// briefing panel (instructions + OK button) and a countdown panel
// (one big number). Both panels are children of this same object
// or referenced directly - either is fine.
public class MissionBriefing : MonoBehaviour
{
    [SerializeField] private GameObject briefingPanel;
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private int countdownSeconds = 3;

    [Header("Countdown look (applied in code - same in every mission)")]
    [Tooltip("Empty = Anton (bold poster font from TextMesh Pro's extras)")]
    [SerializeField] private TMP_FontAsset countdownFont;
    [SerializeField] private float countdownFontSize = 260f;
    [Tooltip("How see-through the white screen is (0 = invisible, 1 = solid)")]
    [Range(0f, 1f)] [SerializeField] private float screenAlpha = 0.18f;
    [SerializeField] private Color numberColor = new Color(1f, 0.74f, 0.3f);   // warm amber, like the menu titles
    [SerializeField] private Color goColor = new Color(0.95f, 0.22f, 0.12f);   // hot red-orange
    [SerializeField] private Color outlineColor = new Color(0.08f, 0.06f, 0.05f);

    [Tooltip("Wire this per-scene: e.g. Mission1Manager.StartMission + TrainTimer.StartTimer for Railway, or just Mission3Manager.StartMission for Farm")]
    [SerializeField] private UnityEvent onMissionStart;

    // True from scene load until OK is pressed. Drives the cursor
    // re-assert in Update() so it works even if briefingPanel is missing.
    private bool waitingForOk;

    private void Awake()
    {
        // The null checks below silently skip missing references, which
        // makes an unwired briefing look like a "dead" OK button. Shout
        // about it instead so a new mission scene gets caught on first play.
        if (briefingPanel == null)
            Debug.LogError($"[MissionBriefing] '{name}' in scene '{gameObject.scene.name}' has no Briefing Panel assigned - OK will not hide anything.", this);
        if (countdownPanel == null)
            Debug.LogError($"[MissionBriefing] '{name}' in scene '{gameObject.scene.name}' has no Countdown Panel assigned.", this);
        if (countdownText == null)
            Debug.LogError($"[MissionBriefing] '{name}' in scene '{gameObject.scene.name}' has no Countdown Text assigned.", this);
    }

    private void Start()
    {
        waitingForOk = true;

        // Freeze gameplay the instant the scene loads, so the player
        // can't wander off or grab cargo before the mission "officially"
        // begins.
        Time.timeScale = 0f;

        // Force the cursor free regardless of what CameraOrbit does on
        // its own — Update() still runs at timeScale 0, so a click-to-lock
        // camera script could otherwise steal the click meant for OK.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (briefingPanel != null) briefingPanel.SetActive(true);
        if (countdownPanel != null) countdownPanel.SetActive(false);
    }

    private void Update()
    {
        // Keeps re-asserting the free cursor for as long as the briefing
        // panel is showing, so nothing else (e.g. PauseManager's own
        // scene-load cursor logic, which fires after every Start()) can
        // silently override it later in the same frame sequence.
        if (waitingForOk)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // Wire this to the briefing panel's OK button OnClick event
    public void OnOkPressed()
    {
        // TEMP DIAGNOSTIC - remove once click is confirmed working
        Debug.Log("[DIAGNOSTIC] OnOkPressed called.");

        if (!waitingForOk) return; // ignore double-clicks restarting the countdown
        waitingForOk = false;

        if (briefingPanel != null) briefingPanel.SetActive(false);
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        if (countdownPanel != null) countdownPanel.SetActive(true);
        StyleCountdown();

        // Time.timeScale is still 0 here, so this uses realtime,
        // not the scaled WaitForSeconds, or it would never finish.
        for (int i = countdownSeconds; i > 0; i--)
            yield return Punch(i.ToString(), numberColor, 1f, 1f);

        yield return Punch("GO!", goColor, 0.7f, 1.25f);

        if (countdownPanel != null) countdownPanel.SetActive(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        onMissionStart?.Invoke();
    }

    // Big, centred, outlined - whatever the scene's text was set up as
    private void StyleCountdown()
    {
        if (countdownPanel != null && countdownPanel.TryGetComponent(out Image screen))
        {
            Color c = screen.color;
            c.a = screenAlpha;
            screen.color = c;
        }
        if (countdownText == null) return;

        TMP_FontAsset font = countdownFont != null ? countdownFont : Resources.Load<TMP_FontAsset>("Fonts & Materials/Anton SDF");
        if (font != null) countdownText.font = font;

        RectTransform rt = countdownText.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(1200f, 500f);
        countdownText.alignment = TextAlignmentOptions.Center;
        countdownText.enableAutoSizing = false;
        countdownText.textWrappingMode = TextWrappingModes.NoWrap;
        countdownText.fontSize = countdownFontSize;
        countdownText.characterSpacing = 4f;

        // Thick dark outline so it reads on the white screen and bright scenery
        countdownText.outlineWidth = 0.22f;
        countdownText.outlineColor = outlineColor;
    }

    // Each number slams in big, settles, then fades out before the next
    private IEnumerator Punch(string text, Color color, float duration, float size)
    {
        if (countdownText == null) { yield return new WaitForSecondsRealtime(duration); yield break; }

        countdownText.text = text;
        countdownText.color = color;
        Transform t = countdownText.rectTransform;
        const float slam = 0.18f, fade = 0.25f;

        for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(time / slam);
            float scale = Mathf.LerpUnclamped(1.9f, 1f, 1f - (1f - k) * (1f - k) * (1f - k)); // ease out
            t.localScale = Vector3.one * scale * size;
            countdownText.alpha = Mathf.Min(Mathf.Clamp01(time / 0.08f), Mathf.Clamp01((duration - time) / fade));
            yield return null;
        }
        countdownText.alpha = 0f;
    }
}