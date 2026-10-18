using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// The small "current objective" line at the top-left of the screen.
// Lives on the Managers prefab's HUD, so any mission can call:
//     ObjectiveHUD.Set("Take down the guards (1/2)");
//     ObjectiveHUD.Clear();
// It clears itself on every scene change. The dark box grows to fit the
// text (one line up to Max Width, then it wraps onto more lines).
public class ObjectiveHUD : MonoBehaviour
{
    public static ObjectiveHUD Instance { get; private set; }

    [SerializeField] private GameObject objectiveRoot;
    [SerializeField] private TMP_Text objectiveText;

    [Tooltip("Text briefly flashes this color when the objective changes")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.3f);

    [Tooltip("The box never gets wider than this - longer objectives wrap")]
    [SerializeField] private float maxWidth = 760f;
    [Tooltip("Space between the text and the box edge (left/right, top/bottom)")]
    [SerializeField] private Vector2 padding = new Vector2(16f, 8f);

    private Color normalColor = Color.white;
    private string current = "";

    private void Awake()
    {
        Instance = this;
        if (objectiveText != null) normalColor = objectiveText.color;
        PrepareLayout();
        Hide();
    }

    // Box pinned by its top-left corner (where it was designed), background
    // and text filling it, text centred vertically
    private void PrepareLayout()
    {
        if (objectiveRoot == null || objectiveText == null) return;
        var root = (RectTransform)objectiveRoot.transform;
        Vector2 newPivot = new Vector2(0f, 1f);
        root.anchoredPosition += Vector2.Scale(newPivot - root.pivot, root.sizeDelta);
        root.pivot = newPivot;

        foreach (Transform child in root)
        {
            if (child == objectiveText.transform) continue;
            var rt = (RectTransform)child;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        RectTransform t = objectiveText.rectTransform;
        t.anchorMin = Vector2.zero;
        t.anchorMax = Vector2.one;
        t.offsetMin = new Vector2(padding.x, padding.y);
        t.offsetMax = new Vector2(-padding.x, -padding.y);
        objectiveText.alignment = TextAlignmentOptions.MidlineLeft;
        objectiveText.textWrappingMode = TextWrappingModes.Normal;
    }

    private void Fit()
    {
        var root = (RectTransform)objectiveRoot.transform;
        float inner = maxWidth - padding.x * 2f;
        Vector2 one = objectiveText.GetPreferredValues(objectiveText.text, inner, 0f);
        float width = Mathf.Min(objectiveText.GetPreferredValues(objectiveText.text).x, inner);
        root.sizeDelta = new Vector2(width + padding.x * 2f, one.y + padding.y * 2f);
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Hide();

    public static void Set(string objective)
    {
        if (Instance == null) return;
        Instance.Show(objective);
    }

    public static void Clear()
    {
        if (Instance == null) return;
        Instance.Hide();
    }

    private void Show(string objective)
    {
        if (string.IsNullOrEmpty(objective)) { Hide(); return; }
        if (objective == current) return; // unchanged - no flash

        current = objective;
        if (objectiveText != null) objectiveText.text = objective;
        if (objectiveRoot != null) objectiveRoot.SetActive(true);
        if (objectiveRoot != null && objectiveText != null) Fit();

        StopAllCoroutines();
        StartCoroutine(Flash());
    }

    private void Hide()
    {
        current = "";
        StopAllCoroutines();
        if (objectiveText != null) objectiveText.color = normalColor;
        if (objectiveRoot != null) objectiveRoot.SetActive(false);
    }

    private IEnumerator Flash()
    {
        if (objectiveText == null) yield break;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 1.2f;
            objectiveText.color = Color.Lerp(highlightColor, normalColor, t);
            yield return null;
        }
        objectiveText.color = normalColor;
    }
}
