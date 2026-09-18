using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scene Names")]
    public string hubSceneName = "Hub";
    public string mainMenuSceneName = "MainMenu";

    [Header("Progression")]
    [Tooltip("Set true by Mission5Manager once the bomb is successfully defused - persists across scenes for this play session")]
    public bool isCar2Unlocked = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load progress that survives closing and reopening the game
        isCar2Unlocked = PlayerPrefs.GetInt("Car2Unlocked", 0) == 1;
    }

    // Call this from Mission5Manager on a successful defuse
    public void UnlockCar2()
    {
        isCar2Unlocked = true;
        PlayerPrefs.SetInt("Car2Unlocked", 1);
        PlayerPrefs.Save();
    }

    // Auto-spawns the managers prefab before any scene loads,
    // so GameManager.Instance always exists even if you hit
    // Play directly inside a mission scene during testing.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExists()
    {
        if (Instance != null) return;

        GameObject prefab = Resources.Load<GameObject>("Managers");
        if (prefab != null)
        {
            Instantiate(prefab);
        }
        else
        {
            Debug.LogWarning("GameManager: no 'Managers' prefab found in a Resources folder.");
        }
    }

    public void LoadMission(string missionSceneName)
    {
        SceneManager.LoadScene(missionSceneName);
    }

    public void ReturnToHub()
    {
        SceneManager.LoadScene(hubSceneName);
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}