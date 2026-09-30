using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scene Names")]
    public string hubSceneName = "Hub";
    public string mainMenuSceneName = "MainMenu";

    [Tooltip("Every mission scene - used to wipe progress on New Game")]
    public string[] missionSceneNames =
    {
        "Mission_Railway", "Mission_Hotel", "Mission_Farm", "Mission_GasStation", "Mission_ShoppingComplex"
    };

    [Header("Progression")]
    [Tooltip("Set true by Mission5Manager once the bomb is successfully defused - persists across scenes and sessions")]
    public bool isCar2Unlocked = false;

    // Save data lives in PlayerPrefs under these keys. Settings (volume)
    // use their own keys, so New Game never wipes the player's settings.
    private const string SaveExistsKey = "SaveExists";
    private const string Car2Key = "Car2Unlocked";
    private const string MissionDonePrefix = "MissionDone_";

    // True once the player has started a game at least once - enables "Continue"
    public bool HasSaveData => PlayerPrefs.GetInt(SaveExistsKey, 0) == 1;

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
        isCar2Unlocked = PlayerPrefs.GetInt(Car2Key, 0) == 1;
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

    // ---------- Main menu ----------

    // Wipes progress (not settings) and starts fresh in the Hub
    public void StartNewGame()
    {
        ResetProgress();
        PlayerPrefs.SetInt(SaveExistsKey, 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene(hubSceneName);
    }

    // Progress is already loaded in Awake, so continuing = going to the Hub
    public void ContinueGame()
    {
        SceneManager.LoadScene(hubSceneName);
    }

    private void ResetProgress()
    {
        isCar2Unlocked = false;
        PlayerPrefs.DeleteKey(Car2Key);

        foreach (string mission in missionSceneNames)
        {
            PlayerPrefs.DeleteKey(MissionDonePrefix + mission);
        }
    }

    // ---------- Progression ----------

    // Call from each mission manager's CompleteMission()
    public void MarkMissionComplete(string missionSceneName)
    {
        PlayerPrefs.SetInt(MissionDonePrefix + missionSceneName, 1);
        PlayerPrefs.Save();
        Debug.Log($"Saved: {missionSceneName} completed");
    }

    public bool IsMissionComplete(string missionSceneName)
    {
        return PlayerPrefs.GetInt(MissionDonePrefix + missionSceneName, 0) == 1;
    }

    // Call this from Mission5Manager on a successful defuse
    public void UnlockCar2()
    {
        isCar2Unlocked = true;
        PlayerPrefs.SetInt(Car2Key, 1);
        PlayerPrefs.Save();
    }

    // ---------- Scene loading ----------

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