using System.Collections;
using UnityEngine;
using TMPro;

// Put this on an empty GameObject in Mission_ShoppingComplex.
public class BombSystem : MonoBehaviour
{
    public static BombSystem Instance { get; private set; }

    [SerializeField] private float revealDelay = 10f;
    [SerializeField] private float bombDuration = 120f;
    [SerializeField] private float warningThreshold = 10f;

    [Header("Warning UI - only shown in the final stretch")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private TMP_Text warningCountdownText;

    [SerializeField] private CameraShake cameraShake;

    [Tooltip("Whichever mission manager is in this scene - must implement IFailableMission")]
    [SerializeField] private MonoBehaviour missionManagerSource;
    private IFailableMission missionManager;

    private float remaining;
    private bool armed;
    private bool defused;

    private void Awake()
    {
        Instance = this;
        missionManager = missionManagerSource as IFailableMission;
        if (warningPanel != null) warningPanel.SetActive(false);
    }

    // Call this once, the moment the player first enters the stolen car
    public void StartRevealTimer()
    {
        StartCoroutine(RevealThenArm());
    }

    private IEnumerator RevealThenArm()
    {
        yield return new WaitForSeconds(revealDelay);

        armed = true;
        remaining = bombDuration;
        Debug.Log("It's rigged! Get to a secluded spot and defuse it - or run.");
    }

    private void Update()
    {
        if (!armed || defused) return;

        remaining -= Time.deltaTime;

        bool inWarningZone = remaining <= warningThreshold;
        if (warningPanel != null) warningPanel.SetActive(inWarningZone);
        if (inWarningZone && warningCountdownText != null)
        {
            warningCountdownText.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
        }

        if (remaining <= 0f)
        {
            armed = false;
            Explode();
        }
    }

    // Call this once the defuse puzzle is solved
    public void Defuse()
    {
        defused = true;
        armed = false;
        if (warningPanel != null) warningPanel.SetActive(false);
    }

    private void Explode()
    {
        Debug.Log("The bomb went off!");
        if (cameraShake != null) cameraShake.Shake();
        missionManager?.FailMission("The bomb went off");
    }
}
