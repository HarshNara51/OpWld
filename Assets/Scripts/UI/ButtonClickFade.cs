using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Gives every UI Button in the game a quick black blink when pressed.
// Creates itself at startup - no setup. Finds buttons each time a scene
// loads, and keeps looking (once a second) while the cursor is free, so
// buttons built at runtime (Controls menu, pause menu...) get it too.
//
// To keep a button from blinking, add a NoClickFade component to it
// (or to any parent, e.g. a whole puzzle panel).
public class ButtonClickFade : MonoBehaviour
{
    private float nextScanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        GameObject go = new GameObject("ButtonClickFade");
        DontDestroyOnLoad(go);
        go.AddComponent<ButtonClickFade>();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HookAll();

    private void Update()
    {
        // While playing, the cursor is locked and no menus are up - skip the search
        if (Cursor.lockState == CursorLockMode.Locked) return;
        if (Time.unscaledTime < nextScanTime) return;
        nextScanTime = Time.unscaledTime + 1f;
        HookAll();
    }

    private static void HookAll()
    {
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.GetComponent<ClickFadeHooked>() != null) continue;
            b.gameObject.AddComponent<ClickFadeHooked>(); // marks it so it's only hooked once
            if (b.GetComponentInParent<NoClickFade>(true) != null) continue;
            b.onClick.AddListener(() => ScreenFader.Instance.Flash());
        }
    }
}
