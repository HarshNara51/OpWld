using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

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

        // Time.timeScale is still 0 here, so this uses realtime,
        // not the scaled WaitForSeconds, or it would never finish.
        for (int i = countdownSeconds; i > 0; i--)
        {
            if (countdownText != null) countdownText.text = i.ToString();
            yield return new WaitForSecondsRealtime(1f);
        }

        if (countdownText != null) countdownText.text = "GO!";
        yield return new WaitForSecondsRealtime(0.5f);

        if (countdownPanel != null) countdownPanel.SetActive(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        onMissionStart?.Invoke();
    }
}