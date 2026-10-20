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

    [Header("Countdown - visible the whole time the bomb is armed")]
    [Tooltip("On-screen bomb timer (TextMeshPro on the mission's Canvas)")]
    [SerializeField] private TMP_Text bombTimerText;
    [SerializeField] private Color timerColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.25f, 0.25f);

    [Header("Warning UI - only shown in the final stretch")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private TMP_Text warningCountdownText;

    [SerializeField] private CameraShake cameraShake;

    [Tooltip("The rigged car (its VehicleInteraction) - wrecked when the bomb goes off")]
    [SerializeField] private VehicleInteraction bombCar;

    [Tooltip("Whichever mission manager is in this scene - must implement IFailableMission")]
    [SerializeField] private MonoBehaviour missionManagerSource;
    private IFailableMission missionManager;

    private float remaining;
    private bool armed;
    private bool defused;
    private bool revealStarted;
    private bool defusing;

    public bool IsArmed => armed;
    public bool RevealStarted => revealStarted; // true once the player first got in the car
    // True once the "It's rigged!" note has had its time on screen - the objective line waits for it
    public bool RevealNoteDone => armed && Time.time >= revealNoteEndsAt;
    private float revealNoteEndsAt = float.MaxValue;
    private const float RevealNoteSeconds = 6f;
    public float Remaining => remaining;

    private void Awake()
    {
        Instance = this;
        missionManager = missionManagerSource as IFailableMission;
        if (warningPanel != null) warningPanel.SetActive(false);
        if (bombTimerText != null) bombTimerText.text = "";
    }

    // Called once, the moment the player first enters the stolen car
    public void StartRevealTimer()
    {
        if (revealStarted) return;
        revealStarted = true;
        StartCoroutine(RevealThenArm());
    }

    private IEnumerator RevealThenArm()
    {
        yield return new WaitForSeconds(revealDelay);

        armed = true;
        remaining = bombDuration;
        NotePopup.Show("Wait... what's that ticking? It's rigged! Get out of the city to a secluded area and defuse it - or run!", RevealNoteSeconds);
        revealNoteEndsAt = Time.time + RevealNoteSeconds;
    }

    private void Update()
    {
        // Beeping while it's armed - faster in the last seconds
        AudioManager am = AudioManager.Instance;
        if (am != null) am.Loop(this, am.bombBeeps, armed && !defused, 0.5f, remaining <= warningThreshold ? 1.3f : 1f);

        if (!armed || defused) return;

        remaining -= defusing ? Time.unscaledDeltaTime : Time.deltaTime;

        bool inWarningZone = remaining <= warningThreshold;
        if (warningPanel != null) warningPanel.SetActive(inWarningZone);
        if (inWarningZone && warningCountdownText != null)
        {
            warningCountdownText.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
        }

        UpdateTimerText(inWarningZone);

        if (remaining <= 0f)
        {
            armed = false;
            Explode();
        }
    }

    private void UpdateTimerText(bool warning)
    {
        if (bombTimerText == null) return;

        float t = Mathf.Max(0f, remaining);
        int minutes = Mathf.FloorToInt(t / 60f);
        int seconds = Mathf.FloorToInt(t % 60f);
        bombTimerText.text = $"BOMB  {minutes:00}:{seconds:00}";

        // Pulses red in the final stretch
        if (warning)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 10f) + 1f) * 0.5f;
            bombTimerText.color = Color.Lerp(timerColor, warningColor, 0.5f + pulse * 0.5f);
        }
        else
        {
            bombTimerText.color = timerColor;
        }
    }

    // While the defuse puzzle is open the world is frozen, but the bomb
    // keeps ticking in real time
    public void SetDefusing(bool value)
    {
        defusing = value;
    }

    // Wrong answers in the defuse puzzle cost time
    public void ApplyPenalty(float seconds)
    {
        if (!armed || defused) return;
        remaining = Mathf.Max(0.5f, remaining - seconds);
    }

    // Call this once the defuse puzzle is solved
    public void Defuse()
    {
        defused = true;
        armed = false;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUI(AudioManager.Instance.bombDefused);
        if (warningPanel != null) warningPanel.SetActive(false);
        if (bombTimerText != null) bombTimerText.text = "";
    }

    // Instant explosion (e.g. cutting the wrong wire)
    public void ExplodeNow()
    {
        if (defused) return;
        armed = false;
        Explode();
    }

    private void Explode()
    {
        if (bombTimerText != null) bombTimerText.text = "";
        if (warningPanel != null) warningPanel.SetActive(false);
        if (cameraShake != null) cameraShake.Shake();
        AudioManager am = AudioManager.Instance;
        if (am != null) am.PlaySFXAt(am.explosion, bombCar != null ? bombCar.transform.position : transform.position, 1f, 1f, 300f);

        bool playerInside = bombCar != null && VehicleInteraction.Current == bombCar;
        if (bombCar != null) bombCar.DisableForGood(); // the car is toast either way

        if (playerInside || Mission5Manager.Instance == null)
        {
            // Still in the car: you go up with it - normal fail, back to the Hub
            missionManager?.FailMission("The bomb went off with you still inside");
        }
        else
        {
            // Got out in time: mission failed, but you're free to roam
            Mission5Manager.Instance.OnBombWentOffOutside();
        }
    }
}