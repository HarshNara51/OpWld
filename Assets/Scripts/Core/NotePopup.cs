using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// On-screen note/message box. Lives on the Managers prefab, so any
// script in any mission can call:  NotePopup.Show("some text");
// Messages queue up - if two arrive at once, the second waits its turn.
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

    private readonly Queue<(string text, float seconds)> queue = new Queue<(string, float)>();
    private Coroutine routine;

    private Object promptOwner;
    private string promptText;

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
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

    // No note playing: show the prompt (or nothing)
    private void ShowPromptIfIdle()
    {
        if (routine != null) return; // a note is up - the prompt comes back after it
        bool any = promptOwner != null && promptText != null;
        if (any && messageText != null) messageText.text = promptText;
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

            if (messageText != null) messageText.text = text;
            if (panel != null) panel.SetActive(true);

            yield return new WaitForSeconds(seconds);

            if (panel != null) panel.SetActive(false);
            yield return new WaitForSeconds(0.2f); // small gap between notes
        }
        routine = null;
        ShowPromptIfIdle();
    }
}
