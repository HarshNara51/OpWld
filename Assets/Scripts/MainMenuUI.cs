using UnityEngine;

// Put this on any object in the MainMenu scene (e.g. the Canvas itself).
public class MainMenuUI : MonoBehaviour
{
    public void OnPlayPressed()
    {
        GameManager.Instance.ReturnToHub(); // Hub doubles as the "start" destination
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
