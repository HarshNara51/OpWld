using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scene Names")]
    public string hubSceneName = "Hub";
    public string mainMenuSceneName = "MainMenu";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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