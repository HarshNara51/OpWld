using System.Collections;
using UnityEngine;
using TMPro;

// Lives on the Managers prefab, so it's persistent and available
// in every mission scene without per-scene setup.
public class MissionResultUI : MonoBehaviour
{
    public static MissionResultUI Instance { get; private set; }

    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private float displaySeconds = 3.5f;

    private void Awake()
    {
        Instance = this;
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    public void ShowMessage(string message)
    {
        StopAllCoroutines();
        StartCoroutine(ShowRoutine(message));
    }

    private IEnumerator ShowRoutine(string message)
    {
        if (resultText != null) resultText.text = message;
        if (resultPanel != null) resultPanel.SetActive(true);

        // Normal scaled time - gameplay keeps running underneath this,
        // nothing freezes.
        yield return new WaitForSeconds(displaySeconds);

        if (resultPanel != null) resultPanel.SetActive(false);
    }
}
