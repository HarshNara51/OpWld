using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Fade to black and back. Creates itself the first time it's used -
// no setup, no UI to build. From anywhere:
//     ScreenFader.Instance.FadeOutIn(onBlack: () => { ...swap things... });
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
    public bool IsFading { get; private set; }

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
        img.raycastTarget = false;
    }

    // Fade to black, run onBlack, hold a moment, fade back in, then run onDone
    public void FadeOutIn(Action onBlack, Action onDone = null, float fadeTime = 0.6f, float holdTime = 0.8f)
    {
        StartCoroutine(Routine(onBlack, onDone, fadeTime, holdTime));
    }

    private IEnumerator Routine(Action onBlack, Action onDone, float fadeTime, float holdTime)
    {
        IsFading = true;

        yield return Fade(0f, 1f, fadeTime);
        onBlack?.Invoke();
        yield return new WaitForSecondsRealtime(holdTime);
        yield return Fade(1f, 0f, fadeTime);

        IsFading = false;
        onDone?.Invoke();
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
