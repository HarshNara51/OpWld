using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using TMPro;

// Lives on the Managers prefab. The end-of-mission screen, with animated
// art:
//   BUSTED  - NFS Most Wanted style: slams in huge, impact flash + shake,
//             world goes grey and slow-mo, slow push-in, then blasts out.
//   PASSED  - springs up from small with a bounce, gentle zoom, then
//             floats up and fades.
//   FAILED  - drops in from above with a heavy thud, then sinks away.
public class MissionResultUI : MonoBehaviour
{
    public static MissionResultUI Instance { get; private set; }

    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultText;

    [Header("Result art (optional)")]
    [Tooltip("An Image inside the result panel that shows the passed/failed art")]
    [SerializeField] private Image resultImage;
    [SerializeField] private Sprite missionPassedSprite;
    [SerializeField] private Sprite missionFailedSprite;
    [Tooltip("Shown instead of the failed image when the cops catch you")]
    [SerializeField] private Sprite bustedSprite;

    [Header("Timing")]
    [Tooltip("How long success messages stay up")]
    [SerializeField] private float displaySeconds = 4f;
    [Tooltip("How long the fail screen stays up before returning to the Hub")]
    [SerializeField] private float failDisplaySeconds = 5f;

    [Header("Busted extras (NFS Most Wanted feel)")]
    [Tooltip("World slows down while BUSTED is on screen")]
    [SerializeField] private bool bustedSlowMotion = true;
    [Range(0.05f, 1f)][SerializeField] private float slowMotionScale = 0.3f;
    [Tooltip("Optional: a Volume with Color Adjustments (Saturation -100). Its weight fades in to turn the world grey. " +
             "Found automatically on every scene load by name (see bustedGreyVolumeName).")]
    [SerializeField] private Volume bustedGreyVolume;
    [Tooltip("Name of the scene object holding the grey Volume. Looked up each time a scene loads.")]
    [SerializeField] private string bustedGreyVolumeName = "BustedGreyVolume";

    public bool IsShowingFailure { get; private set; }

    private enum Style { Plain, Busted, Passed, Failed }

    private CanvasGroup group;
    private Image flash;
    private RectTransform imageRect;
    private RectTransform textRect;
    private Vector2 imageHome;
    private Vector2 textHome;

    private void Awake()
    {
        Instance = this;

        if (resultPanel != null)
        {
            group = resultPanel.GetComponent<CanvasGroup>();
            if (group == null) group = resultPanel.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            // Full-screen white flash for the BUSTED impact (built automatically)
            GameObject f = new GameObject("ImpactFlash", typeof(RectTransform), typeof(Image));
            f.transform.SetParent(resultPanel.transform, false);
            f.transform.SetAsFirstSibling();
            RectTransform fr = (RectTransform)f.transform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
            flash = f.GetComponent<Image>();
            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.raycastTarget = false;

            resultPanel.SetActive(false);
        }

        if (resultImage != null) { imageRect = resultImage.rectTransform; imageHome = imageRect.anchoredPosition; }
        if (resultText != null) { textRect = resultText.rectTransform; textHome = textRect.anchoredPosition; }
        if (bustedGreyVolume != null) bustedGreyVolume.weight = 0f;
    }

    // Managers is a persistent prefab and can't reference scene objects, so
    // grab the scene's BustedGreyVolume each time a scene loads.
    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Volume found = FindGreyVolume(scene);
        if (found == null && mode == LoadSceneMode.Additive) return; // keep the main scene's one

        bustedGreyVolume = found;
        if (bustedGreyVolume != null) bustedGreyVolume.weight = 0f;
    }

    private Volume FindGreyVolume(Scene scene)
    {
        foreach (Volume v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (v.gameObject.scene == scene && v.name == bustedGreyVolumeName) return v;
        }
        return null;
    }

    // ---------------- public API (same as before) ----------------

    public void ShowMessage(string message, Action onComplete = null)
    {
        SetArt(null);
        Run(message, Style.Plain, displaySeconds, onComplete);
    }

    public void ShowSuccess(string subtitle = null, float delay = 0f)
    {
        StopAllCoroutines();
        StartCoroutine(SuccessAfterDelay(subtitle, delay));
    }

    private IEnumerator SuccessAfterDelay(string subtitle, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        string message;
        if (missionPassedSprite != null)
        {
            SetArt(missionPassedSprite);
            message = subtitle ?? "";
        }
        else
        {
            SetArt(null);
            message = string.IsNullOrEmpty(subtitle) ? "MISSION PASSED" : $"MISSION PASSED\n<size=60%>{subtitle}</size>";
        }
        yield return Show(message, Style.Passed, displaySeconds, null);
    }

    public void ShowFailure(string reason)
    {
        IsShowingFailure = true;
        Style style = PrepareFailure(reason, out string message);
        Run(message, style, failDisplaySeconds, () =>
        {
            IsShowingFailure = false;
            GameManager.Instance.ReturnToHub();
        });
    }

    // Failed, but the player stays in the scene (free roam)
    public void ShowFailureStay(string reason)
    {
        Style style = PrepareFailure(reason, out string message);
        Run(message, style, failDisplaySeconds, null);
    }

    private Style PrepareFailure(string reason, out string message)
    {
        if (reason == "Busted")
        {
            if (bustedSprite != null) { SetArt(bustedSprite); message = ""; }
            else { SetArt(null); message = "BUSTED!"; }
            return Style.Busted;
        }

        if (missionFailedSprite != null) { SetArt(missionFailedSprite); message = reason ?? ""; }
        else
        {
            SetArt(null);
            message = string.IsNullOrEmpty(reason) ? "MISSION FAILED" : $"MISSION FAILED\n<size=60%>{reason}</size>";
        }
        return Style.Failed;
    }

    private void SetArt(Sprite sprite)
    {
        if (resultImage == null) return;
        resultImage.sprite = sprite;
        resultImage.gameObject.SetActive(sprite != null);
    }

    private void Run(string message, Style style, float seconds, Action onComplete)
    {
        StopAllCoroutines();
        StartCoroutine(Show(message, style, seconds, onComplete));
    }

    // ---------------- the show ----------------

    private IEnumerator Show(string message, Style style, float seconds, Action onComplete)
    {
        if (resultText != null)
        {
            resultText.text = message;
            resultText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        ResetVisuals();
        if (resultPanel != null) resultPanel.SetActive(true);

        bool slowMo = style == Style.Busted && bustedSlowMotion;
        if (slowMo) Time.timeScale = slowMotionScale;

        float inTime = AnimateInDuration(style);
        float outTime = 0.4f;
        float hold = Mathf.Max(0.5f, seconds - inTime - outTime);

        yield return AnimateIn(style);
        yield return Hold(style, hold);
        yield return AnimateOut(style);

        if (resultPanel != null) resultPanel.SetActive(false);
        if (slowMo && Time.timeScale > 0f) Time.timeScale = 1f;
        if (bustedGreyVolume != null) bustedGreyVolume.weight = 0f;

        onComplete?.Invoke();
    }

    private float AnimateInDuration(Style style)
    {
        switch (style)
        {
            case Style.Busted: return 0.55f;
            case Style.Passed: return 0.6f;
            case Style.Failed: return 0.7f;
            default: return 0.25f;
        }
    }

    private void ResetVisuals()
    {
        if (group != null) group.alpha = 1f;
        if (flash != null) flash.color = new Color(1f, 1f, 1f, 0f);
        if (imageRect != null) { imageRect.anchoredPosition = imageHome; imageRect.localScale = Vector3.one; imageRect.localRotation = Quaternion.identity; }
        if (textRect != null) { textRect.anchoredPosition = textHome; textRect.localScale = Vector3.one; }
        SetTextAlpha(0f);
        SetImageAlpha(0f);
    }

    private IEnumerator AnimateIn(Style style)
    {
        switch (style)
        {
            case Style.Busted:
                {
                    // Slams in from huge, slightly twisted
                    yield return Tween(0.18f, t =>
                    {
                        float e = t * t; // accelerate into the hit
                        SetImage(Vector2.zero, Mathf.Lerp(5f, 0.9f, e), Mathf.Lerp(-10f, 0f, e), Mathf.Clamp01(t * 3f));
                    });

                    // IMPACT: flash, camera shake, grey world
                    if (flash != null) flash.color = new Color(1f, 1f, 1f, 0.75f);
                    CameraShake shake = FindAnyObjectByType<CameraShake>();
                    if (shake != null) shake.Shake(0.35f, 0.35f);

                    yield return Tween(0.37f, t =>
                    {
                        float settle = 1f + 0.12f * Mathf.Sin(t * Mathf.PI) * (1f - t); // overshoot, then settle
                        float shakeAmt = 22f * (1f - t);
                        Vector2 jitter = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * shakeAmt;
                        SetImage(jitter, Mathf.Lerp(0.9f, 1f, t) * settle, 0f, 1f);
                        if (flash != null) flash.color = new Color(1f, 1f, 1f, 0.75f * (1f - t));
                        if (bustedGreyVolume != null) bustedGreyVolume.weight = t;
                        SetTextAlpha(t);
                    });
                    break;
                }

            case Style.Passed:
                {
                    // Springs up from small with a bouncy overshoot
                    yield return Tween(0.6f, t =>
                    {
                        float s = BackOut(t);
                        SetImage(new Vector2(0f, Mathf.Lerp(-60f, 0f, EaseOut(t))), Mathf.Lerp(0.4f, 1f, s), 0f, Mathf.Clamp01(t * 2.5f));
                        SetText(Vector2.up * Mathf.Lerp(-25f, 0f, EaseOut(Mathf.Clamp01((t - 0.35f) / 0.65f))),
                                Mathf.Clamp01((t - 0.35f) / 0.65f));
                    });
                    break;
                }

            case Style.Failed:
                {
                    // Drops in from above and lands with a heavy bounce
                    yield return Tween(0.7f, t =>
                    {
                        SetImage(new Vector2(0f, Mathf.Lerp(260f, 0f, BounceOut(t))), 1f, 0f, Mathf.Clamp01(t * 3f));
                        SetText(Vector2.zero, Mathf.Clamp01((t - 0.5f) / 0.5f));
                    });
                    CameraShake shake = FindAnyObjectByType<CameraShake>();
                    if (shake != null) shake.Shake(0.2f, 0.15f);
                    break;
                }

            default:
                yield return Tween(0.25f, t => { SetImage(Vector2.zero, 1f, 0f, t); SetText(Vector2.zero, t); });
                break;
        }
    }

    private IEnumerator Hold(Style style, float seconds)
    {
        float zoomTo = style == Style.Busted ? 1.12f : style == Style.Passed ? 1.05f : 1.03f;
        yield return Tween(seconds, t =>
        {
            if (imageRect != null) imageRect.localScale = Vector3.one * Mathf.Lerp(1f, zoomTo, t); // slow push-in
        });
    }

    private IEnumerator AnimateOut(Style style)
    {
        float startScale = imageRect != null ? imageRect.localScale.x : 1f;

        switch (style)
        {
            case Style.Busted:
                // Blasts outward past the camera
                yield return Tween(0.4f, t =>
                {
                    float e = t * t;
                    SetImage(Vector2.zero, Mathf.Lerp(startScale, 7f, e), Mathf.Lerp(0f, 8f, e), 1f - t);
                    SetTextAlpha(1f - t);
                    if (bustedGreyVolume != null) bustedGreyVolume.weight = 1f - t;
                });
                break;

            case Style.Passed:
                // Floats up and fades
                yield return Tween(0.4f, t =>
                {
                    SetImage(Vector2.up * Mathf.Lerp(0f, 80f, t * t), Mathf.Lerp(startScale, startScale * 1.1f, t), 0f, 1f - t);
                    SetText(Vector2.up * Mathf.Lerp(0f, 40f, t), 1f - t);
                });
                break;

            case Style.Failed:
                // Sinks away
                yield return Tween(0.4f, t =>
                {
                    SetImage(Vector2.down * Mathf.Lerp(0f, 200f, t * t), startScale, 0f, 1f - t);
                    SetTextAlpha(1f - t);
                });
                break;

            default:
                yield return Tween(0.4f, t => { SetImageAlpha(1f - t); SetTextAlpha(1f - t); });
                break;
        }
    }

    // ---------------- helpers ----------------

    // Runs an animation over real time (works during slow-mo too)
    private IEnumerator Tween(float duration, Action<float> step)
    {
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
            step(t);
            yield return null;
        }
    }

    private void SetImage(Vector2 offset, float scale, float rotation, float alpha)
    {
        if (imageRect == null) return;
        imageRect.anchoredPosition = imageHome + offset;
        imageRect.localScale = Vector3.one * scale;
        imageRect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        SetImageAlpha(alpha);
    }

    private void SetText(Vector2 offset, float alpha)
    {
        if (textRect != null) textRect.anchoredPosition = textHome + offset;
        SetTextAlpha(alpha);
    }

    private void SetImageAlpha(float a)
    {
        if (resultImage != null) { Color c = resultImage.color; c.a = a; resultImage.color = c; }
    }

    private void SetTextAlpha(float a)
    {
        if (resultText != null) resultText.alpha = a;
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    private static float BackOut(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private static float BounceOut(float t)
    {
        const float n1 = 7.5625f, d1 = 2.75f;
        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
        if (t < 2.5f / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        return n1 * (t -= 2.625f / d1) * t + 0.984375f;
    }
}