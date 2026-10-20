using UnityEngine;
using UnityEngine.SceneManagement;

// Lives on the same Managers prefab as GameManager, so it's
// automatically persistent — no extra DontDestroyOnLoad needed.
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    [Tooltip("The Canvas/Panel GameObject shown while paused")]
    [SerializeField] private GameObject pauseCanvas;

    [Tooltip("Child of the pause canvas holding Resume / Settings / Return Home / Main Menu / Quit")]
    [SerializeField] private GameObject buttonsPanel;

    [Tooltip("Child of the pause canvas holding the volume sliders + a Back button")]
    [SerializeField] private GameObject settingsPanel;

    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

    [Tooltip("Menu scenes: cursor stays free on load, and pausing is disabled")]
    [SerializeField] private string[] cursorFreeScenes = { "MainMenu" };

    private bool isPaused;

    // GameKeys ignores gameplay keys while this is true (e.g. rebinding keys in the pause menu)
    public static bool IsPaused => Instance != null && Instance.isPaused;

    // Set by puzzles (lockpick, bomb) so Esc can't open the pause menu
    // on top of them - both control time and would fight each other
    public static bool InputBlocked;

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
        InputBlocked = false;
        Time.timeScale = 1f;
        if (pauseCanvas != null) pauseCanvas.SetActive(false);

        bool cursorFree = IsCursorFreeScene(scene.name);
        Cursor.lockState = cursorFree ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = cursorFree;
    }

    private void Update()
    {
        // No pause menu on the main menu - Esc does nothing there
        if (IsCursorFreeScene(SceneManager.GetActiveScene().name)) return;
        if (InputBlocked) return;

        if (Input.GetKeyDown(pauseKey))
        {
            // Esc inside Controls steps back to Settings first
            if (isPaused && ControlsMenu.CloseOpenPage()) return;

            // Esc inside Settings steps back to the pause buttons first
            if (isPaused && settingsPanel != null && settingsPanel.activeSelf)
            {
                OnSettingsBackPressed();
                return;
            }

            SetPaused(!isPaused);
        }
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (pauseCanvas != null) pauseCanvas.SetActive(paused);

        // Every pause opens on the main pause buttons, never mid-settings
        if (paused) ShowButtons();

        // Adjust this if CameraOrbit doesn't actually lock the cursor
        // during normal gameplay — harmless either way if it doesn't.
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    private void ShowButtons()
    {
        if (buttonsPanel != null) buttonsPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // Wire these to the pause menu buttons' OnClick events
    public void OnResumePressed() => SetPaused(false);

    public void OnSettingsPressed()
    {
        if (buttonsPanel != null) buttonsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void OnSettingsBackPressed() => ShowButtons();

    public void OnReturnHomePressed()
    {
        SetPaused(false);
        GameManager.Instance.ReturnToHub();
    }

    public void OnMainMenuPressed()
    {
        SetPaused(false);
        GameManager.Instance.LoadMainMenu();
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