using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// On-screen note/message box. Lives on the Managers prefab, so any
// script in any mission can call:  NotePopup.Show("some text");
// The box pops in, the text TYPES itself out letter by letter, holds,
// then fades away. Messages queue up and play one after another.
//
// Prompts ("Press [E] to ...") are different: NotePopup.Prompt(this, text)
// shows one for as long as the player is in range, ClearPrompt(this) takes
// it away. They never queue - a real note always goes first, and the
// prompt comes back after it if the player is still there.
public class NotePopup : MonoBehaviour
{
    public static NotePopup Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private float defaultSeconds = 5f;

    [Header("Typewriter")]
    [Tooltip("Letters per second")]
    [SerializeField] private float typeSpeed = 45f;
    [Tooltip("Minimum time the full text stays readable after typing finishes")]
    [SerializeField] private float minReadTime = 1.5f;
    [Tooltip("Optional click/tick sound while typing")]
    [SerializeField] private AudioClip typeSound;
    [Tooltip("Play the type sound every N letters")]
    [SerializeField] private int soundEveryLetters = 2;

    private readonly Queue<(string text, float seconds)> queue = new Queue<(string, float)>();
    private Coroutine routine;
    private CanvasGroup group;
    private RectTransform panelRect;

    private Object promptOwner;
    private string promptText;

    private void Awake()
    {
        Instance = this;
        if (panel != null)
        {
            panelRect = panel.GetComponent<RectTransform>();
            group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            panel.SetActive(false);
        }
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // Notes never carry over into the next scene
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        queue.Clear();
        if (routine != null) { StopCoroutine(routine); routine = null; }
        promptOwner = null;
        promptText = null;
        if (panel != null) panel.SetActive(false);
    }

    // ---------- prompts ----------

    // Shows (or updates) this object's prompt until ClearPrompt. Call it again
    // with new text any time (e.g. "Calling the cops... 3/5").
    public static void Prompt(Object owner, string text)
    {
        if (Instance == null || owner == null || string.IsNullOrWhiteSpace(text)) return;
        if (Instance.promptOwner == owner && Instance.promptText == text) return;
        Instance.promptOwner = owner;
        Instance.promptText = text;
        Instance.ShowPromptIfIdle();
    }

    // Takes the prompt away - only if it's still this object's
    public static void ClearPrompt(Object owner)
    {
        if (Instance == null || Instance.promptOwner != owner) return;
        Instance.promptOwner = null;
        Instance.promptText = null;
        Instance.ShowPromptIfIdle();
    }

    // A key, the way prompts show it: [E]
    public static string Key(GameAction action) => Key(GameKeys.Label(action));
    public static string Key(string label) => $"<b><color=#FFE08A>[{label}]</color></b>";

    // No note playing: show the prompt (or nothing). Prompts show instantly -
    // no typewriter, since they change as the player moves around.
    private void ShowPromptIfIdle()
    {
        if (routine != null) return; // a note is up - the prompt comes back after it
        bool any = promptOwner != null && promptText != null;
        if (any && messageText != null)
        {
            messageText.text = promptText;
            messageText.maxVisibleCharacters = int.MaxValue;
        }
        if (group != null) group.alpha = 1f;
        if (panelRect != null) panelRect.localScale = Vector3.one;
        if (panel != null) panel.SetActive(any);
    }

    // seconds <= 0 uses the default (5s)
    public static void Show(string message, float seconds = 0f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        if (Instance == null)
        {
            Debug.Log($"[Note] {message}");
            return;
        }

        Instance.queue.Enqueue((message, seconds > 0f ? seconds : Instance.defaultSeconds));
        if (Instance.routine == null) Instance.routine = Instance.StartCoroutine(Instance.ShowQueue());
    }

    private IEnumerator ShowQueue()
    {
        while (queue.Count > 0)
        {
            var (text, seconds) = queue.Dequeue();

            // Set up: full text loaded, but nothing visible yet
            messageText.text = text;
            messageText.maxVisibleCharacters = 0;
            panel.SetActive(true);
            messageText.ForceMeshUpdate();
            int totalLetters = messageText.textInfo.characterCount;

            // Pop in: slight scale-up + fade
            yield return Tween(0.18f, t =>
            {
                group.alpha = t;
                if (panelRect != null) panelRect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, 1f - (1f - t) * (1f - t));
            });

            // Typewriter
            float shown = 0f;
            int lastSoundAt = 0;
            float typingTime = 0f;
            while (shown < totalLetters)
            {
                float dt = Time.unscaledDeltaTime;
                typingTime += dt;
                shown += typeSpeed * dt;
                int visible = Mathf.Min(totalLetters, Mathf.FloorToInt(shown));
                messageText.maxVisibleCharacters = visible;

                if (typeSound != null && AudioManager.Instance != null && visible - lastSoundAt >= Mathf.Max(1, soundEveryLetters))
                {
                    lastSoundAt = visible;
                    AudioManager.Instance.PlayUI(typeSound);
                }
                yield return null;
            }
            messageText.maxVisibleCharacters = totalLetters;

            // Hold (whatever's left of its time, but always readable for a moment)
            float hold = Mathf.Max(minReadTime, seconds - typingTime - 0.4f);
            yield return new WaitForSecondsRealtime(hold);

            // Fade out
            yield return Tween(0.22f, t => group.alpha = 1f - t);

            panel.SetActive(false);
            yield return new WaitForSecondsRealtime(0.15f); // small gap between notes
        }
        routine = null;
        ShowPromptIfIdle();
    }

    private IEnumerator Tween(float duration, System.Action<float> step)
    {
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
            step(t);
            yield return null;
        }
    }
}