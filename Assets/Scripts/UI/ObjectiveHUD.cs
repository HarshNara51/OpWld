using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// The small "current objective" line at the top-left of the screen.
// Lives on the Managers prefab's HUD, so any mission can call:
//     ObjectiveHUD.Set("Take down the guards (1/2)");
//     ObjectiveHUD.Clear();
// It clears itself on every scene change.
public class ObjectiveHUD : MonoBehaviour
{
    public static ObjectiveHUD Instance { get; private set; }

    [SerializeField] private GameObject objectiveRoot;
    [SerializeField] private TMP_Text objectiveText;

    [Tooltip("Text briefly flashes this color when the objective changes")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.3f);

    private Color normalColor = Color.white;
    private string current = "";

    private void Awake()
    {
        Instance = this;
        if (objectiveText != null) normalColor = objectiveText.color;
        Hide();
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
