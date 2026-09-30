using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Put this on an empty GameObject in the MainMenu scene.
// Flow: Title ("Press any key") > Main (Play / Settings / Quit)
//       > Mode (Continue / New Game / Back) > Hub
// All screens are panels under one Canvas - this just switches
// which panel is visible.
public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject modePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Title screen")]
    [Tooltip("The 'Press any key' text - gently pulses")]
    [SerializeField] private TMP_Text pressAnyKeyText;
    [SerializeField] private float pulseSpeed = 2f;

    [Header("Mode screen")]
    [Tooltip("Greyed out when there's no save yet")]
    [SerializeField] private Button continueButton;

    // Static: survives scene loads, so coming back from the game via
    // the pause menu skips the title and opens the main menu directly.
    private static bool titleSeen;

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Show(titleSeen ? mainPanel : titlePanel);
    }

    private void Update()
    {
        if (titlePanel != null && titlePanel.activeSelf)
        {
            // Pulse the prompt between 30% and 100% opacity
            if (pressAnyKeyText != null)
            {
                float a = Mathf.Lerp(0.3f, 1f, (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f);
                pressAnyKeyText.alpha = a;
            }

            if (Input.anyKeyDown)
            {
                titleSeen = true;
                Show(mainPanel);
            }
            return;
        }

        // Esc steps back from sub-screens
        if (Input.GetKeyDown(KeyCode.Escape) && !mainPanel.activeSelf)
        {
            Show(mainPanel);
        }
    }

    // ---------- Main screen ----------

    public void OnPlayPressed()
    {
        if (continueButton != null)
        {
            continueButton.interactable = GameManager.Instance.HasSaveData;
        }
        Show(modePanel);
    }

    public void OnSettingsPressed() => Show(settingsPanel);

    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Mode screen ----------

    public void OnContinuePressed() => GameManager.Instance.ContinueGame();

    public void OnNewGamePressed() => GameManager.Instance.StartNewGame();

    // ---------- Shared ----------

    // Wire every "Back" button (Mode and Settings screens) to this
    public void OnBackPressed() => Show(mainPanel);

    private void Show(GameObject panel)
    {
        if (titlePanel != null) titlePanel.SetActive(panel == titlePanel);
        if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
        if (modePanel != null) modePanel.SetActive(panel == modePanel);
        if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
    }
}
