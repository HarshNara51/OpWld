using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fade to black and back. Creates itself the first time it's used -
// no setup, no UI to build. From anywhere:
//     ScreenFader.Instance.FadeOutIn(onBlack: () => { ...swap things... });
//     ScreenFader.Instance.LoadScene("Hub");   // fade out, load, fade in
//     ScreenFader.Instance.Flash();            // quick blink (button presses)
public class ScreenFader : MonoBehaviour
{
    private static ScreenFader instance;

    public static ScreenFader Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("ScreenFader");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<ScreenFader>();
                instance.Build();
            }
            return instance;
        }
    }

    private CanvasGroup group;
    private Coroutine flashRoutine;

    // True during any fade except a quick button flash
    public bool IsFading { get; private set; }
    public bool IsLoadingScene { get; private set; }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // above every other UI

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject imageGO = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imageGO.transform.SetParent(transform, false);
        RectTransform rt = (RectTransform)imageGO.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = imageGO.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true; // only blocks clicks while group.blocksRaycasts is on
    }

    // Fade to black, run onBlack, hold a moment, fade back in, then run onDone
    public void FadeOutIn(Action onBlack, Action onDone = null, float fadeTime = 0.6f, float holdTime = 0.8f)
    {
        StopFlash();
        StartCoroutine(Routine(onBlack, onDone, fadeTime, holdTime));
    }

    // Fade to black, load the scene while black, fade back in once it's ready.
    // Every scene change goes through here (see GameManager).
    public void LoadScene(string sceneName, float fadeTime = 0.5f)
    {
        if (IsLoadingScene) return; // already on the way somewhere
        StopFlash();
        StartCoroutine(SceneRoutine(sceneName, fadeTime));
    }

    // Quick blink to black and back - used for every UI button press.
    // Skipped while a bigger fade (scene load, teleport...) is running.
    public void Flash(float outTime = 0.08f, float holdTime = 0.04f, float inTime = 0.15f)
    {
        if (IsFading) return;
        StopFlash();
        flashRoutine = StartCoroutine(FlashRoutine(outTime, holdTime, inTime));
    }

    private void StopFlash()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = null;
    }

    private IEnumerator Routine(Action onBlack, Action onDone, float fadeTime, float holdTime)
    {
        IsFading = true;

        yield return Fade(group.alpha, 1f, fadeTime);
        onBlack?.Invoke();
        yield return new WaitForSecondsRealtime(holdTime);
        yield return Fade(1f, 0f, fadeTime);

        IsFading = false;
        onDone?.Invoke();
    }

    private IEnumerator SceneRoutine(string sceneName, float fadeTime)
    {
        IsFading = true;
        IsLoadingScene = true;
        group.blocksRaycasts = true; // no double clicks while we leave

        yield return Fade(group.alpha, 1f, fadeTime);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone) yield return null;

        // Let the new scene run its Start()s and settle the camera before revealing it
        yield return null;
        yield return new WaitForSecondsRealtime(0.15f);

        IsLoadingScene = false;
        yield return Fade(1f, 0f, fadeTime);

        group.blocksRaycasts = false;
        IsFading = false;
    }

    private IEnumerator FlashRoutine(float outTime, float holdTime, float inTime)
    {
        yield return Fade(group.alpha, 1f, outTime);
        yield return new WaitForSecondsRealtime(holdTime);
        yield return Fade(1f, 0f, inTime);
        flashRoutine = null;
    }

    private IEnumerator Fade(float from, float to, float time)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, time);
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        group.alpha = to;
    }
}
