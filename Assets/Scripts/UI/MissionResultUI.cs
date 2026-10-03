using System;
using System.Collections;
using UnityEngine;
using TMPro;

// Lives on the Managers prefab, so it's persistent and available
// in every mission scene without per-scene setup.
public class MissionResultUI : MonoBehaviour
{
    public static MissionResultUI Instance { get; private set; }

    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultText;

    [Tooltip("How long success messages stay up")]
    [SerializeField] private float displaySeconds = 3.5f;

    [Tooltip("How long the fail screen stays up before returning to the Hub")]
    [SerializeField] private float failDisplaySeconds = 5f;

    public bool IsShowingFailure { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    // Success / info messages. onComplete is optional.
    public void ShowMessage(string message, Action onComplete = null)
    {
        StopAllCoroutines();
        StartCoroutine(ShowRoutine(message, displaySeconds, onComplete));
    }

    // The one shared success flow for every mission: optional wait,
    // then "MISSION PASSED" (+ optional line). Free roam continues.
    public void ShowSuccess(string subtitle = null, float delay = 0f)
    {
        string message = string.IsNullOrEmpty(subtitle)
            ? "MISSION PASSED"
            : $"MISSION PASSED\n<size=60%>{subtitle}</size>";

        StopAllCoroutines();
        StartCoroutine(SuccessRoutine(message, delay));
    }

    private IEnumerator SuccessRoutine(string message, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        yield return ShowRoutine(message, displaySeconds, null);
    }

    // The one shared fail flow for every mission: show the reason,
    // wait, then drop the player back in the Hub.
    public void ShowFailure(string reason)
    {
        string message = reason == "Busted"
            ? "BUSTED!"
            : string.IsNullOrEmpty(reason) ? "MISSION FAILED" : $"MISSION FAILED\n<size=60%>{reason}</size>";

        IsShowingFailure = true;
        StopAllCoroutines();
        StartCoroutine(ShowRoutine(message, failDisplaySeconds, () =>
        {
            IsShowingFailure = false;
            GameManager.Instance.ReturnToHub();
        }));
    }

    private IEnumerator ShowRoutine(string message, float seconds, Action onComplete)
    {
        if (resultText != null) resultText.text = message;
        if (resultPanel != null) resultPanel.SetActive(true);

        // Normal scaled time - gameplay keeps running underneath this,
        // nothing freezes (and pausing pauses the countdown too).
        yield return new WaitForSeconds(seconds);

        if (resultPanel != null) resultPanel.SetActive(false);

        onComplete?.Invoke();
    }
}