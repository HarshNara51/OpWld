using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// On-screen note/message box. Lives on the Managers prefab, so any
// script in any mission can call:  NotePopup.Show("some text");
// Messages queue up - if two arrive at once, the second waits its turn.
public class NotePopup : MonoBehaviour
{
    public static NotePopup Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private float defaultSeconds = 5f;

    private readonly Queue<(string text, float seconds)> queue = new Queue<(string, float)>();
    private Coroutine routine;

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
        if (panel != null) panel.SetActive(false);
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
    }
}
