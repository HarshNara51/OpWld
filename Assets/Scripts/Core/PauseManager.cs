using UnityEngine;
using UnityEngine.SceneManagement;

// Lives on the same Managers prefab as GameManager, so it's
// automatically persistent — no extra DontDestroyOnLoad needed.
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    [Tooltip("The Canvas/Panel GameObject shown while paused")]
    [SerializeField] private GameObject pauseCanvas;

    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

    [Tooltip("Scenes where the cursor should stay visible/unlocked on load, e.g. menus")]
    [SerializeField] private string[] cursorFreeScenes = { "MainMenu" };

    private bool isPaused;

    private bool IsCursorFreeScene(string sceneName)
    {
        foreach (var name in cursorFreeScenes)
        {
            if (name == sceneName) return true;
        }
        return false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Always start a freshly loaded scene unpaused,
        // so pausing in one scene can't carry into the next.
        isPaused = false;
        Time.timeScale = 1f;
        if (pauseCanvas != null) pauseCanvas.SetActive(false);

        bool cursorFree = IsCursorFreeScene(scene.name);
        Cursor.lockState = cursorFree ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = cursorFree;
    }

    private void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            SetPaused(!isPaused);
        }
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (pauseCanvas != null) pauseCanvas.SetActive(paused);

        // Adjust this if CameraOrbit doesn't actually lock the cursor
        // during normal gameplay — harmless either way if it doesn't.
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    // Wire these to your three buttons' OnClick events
    public void OnResumePressed() => SetPaused(false);

    public void OnReturnHomePressed()
    {
        SetPaused(false);
        GameManager.Instance.ReturnToHub();
    }

    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}