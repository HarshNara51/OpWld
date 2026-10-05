using System;
using UnityEngine;
using TMPro;

// Skyrim-style lockpicking for locked cars.
// Move the mouse to angle the pick, HOLD the turn key to try turning the
// lock. The closer the pick is to the hidden sweet spot, the further the
// lock turns. Straining against a wrong angle wears the pick down until
// it snaps. Turn the lock all the way = unlocked.
//
// Put it on the puzzle panel's parent (or any object) and assign it to
// the car's VehicleInteraction -> Unlock Puzzle.
public class LockpickPuzzle : MonoBehaviour, ICarUnlockPuzzle
{
    [Header("UI")]
    [SerializeField] private GameObject panel;
    [Tooltip("The lock cylinder image - rotates as the lock turns")]
    [SerializeField] private RectTransform lockCylinder;
    [Tooltip("The pick image - pivot at its base, pointing up from the lock's center")]
    [SerializeField] private RectTransform pick;
    [Tooltip("Instructions / picks left / status")]
    [SerializeField] private TMP_Text infoText;

    [Header("Controls")]
    [SerializeField] private KeyCode turnKey = KeyCode.D;
    [SerializeField] private KeyCode cancelKey = KeyCode.Q;
    [SerializeField] private float mouseSensitivity = 3f;

    [Header("Difficulty")]
    [Tooltip("Within this many degrees of the sweet spot, the lock turns all the way")]
    [SerializeField] private float sweetSpotSize = 8f;
    [Tooltip("How far past the sweet spot the lock still turns a little")]
    [SerializeField] private float falloff = 40f;
    [SerializeField] private float lockTurnSpeed = 120f;
    [SerializeField] private float lockReturnSpeed = 240f;
    [Tooltip("Seconds of straining at a wrong angle before the pick snaps")]
    [SerializeField] private float pickStrength = 1f;
    [SerializeField] private int picks = 3;

    [Header("Sounds (all optional)")]
    [Tooltip("Plays when the pick moves INTO the sweet spot - a hint that makes it easier")]
    [SerializeField] private AudioClip sweetSpotClick;
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioClip pickBreakSound;

    public bool IsOpen { get; private set; }

    private Action onSolved;
    private float sweetSpot;
    private float pickAngle;
    private float lockAngle;
    private float strain;
    private int picksLeft;
    private float savedTimeScale = 1f;
    private string status = "";
    private bool wasInSweetSpot;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void Open(Action solvedCallback)
    {
        onSolved = solvedCallback;
        IsOpen = true;
        picksLeft = picks;
        sweetSpot = UnityEngine.Random.Range(-80f, 80f); // new lock, new sweet spot
        ResetPick();
        status = "";

        if (panel != null) panel.SetActive(true);

        savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f; // freeze the world while picking
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        UpdateVisuals(0f);
    }

    private static void PlaySound(AudioClip clip)
    {
        // UI channel keeps playing even while the world is frozen
        if (clip != null && AudioManager.Instance != null) AudioManager.Instance.PlayUI(clip);
    }

    private void ResetPick()
    {
        wasInSweetSpot = false;
        pickAngle = 0f;
        lockAngle = 0f;
        strain = 0f;
    }

    private void Update()
    {
        if (!IsOpen) return;

        float dt = Time.unscaledDeltaTime; // world is frozen, so use real time

        if (Input.GetKeyDown(cancelKey))
        {
            Close(false, "You back off from the car.");
            return;
        }

        bool turning = Input.GetKey(turnKey);
        float jiggle = 0f;

        if (!turning)
        {
            // Free to move the pick; the lock springs back
            pickAngle = Mathf.Clamp(pickAngle + Input.GetAxis("Mouse X") * mouseSensitivity, -90f, 90f);

            // Little "click" the moment the pick slides into the sweet spot
            bool inSweetSpot = Mathf.Abs(pickAngle - sweetSpot) <= sweetSpotSize;
            if (inSweetSpot && !wasInSweetSpot) PlaySound(sweetSpotClick);
            wasInSweetSpot = inSweetSpot;

            lockAngle = Mathf.MoveTowards(lockAngle, 0f, lockReturnSpeed * dt);
            strain = 0f;
        }
        else
        {
            float off = Mathf.Abs(pickAngle - sweetSpot);
            float allowed = off <= sweetSpotSize
                ? 90f
                : 90f * Mathf.Clamp01(1f - (off - sweetSpotSize) / falloff);

            lockAngle = Mathf.MoveTowards(lockAngle, allowed, lockTurnSpeed * dt);

            if (allowed >= 90f && lockAngle >= 89.5f)
            {
                Close(true, "");
                return;
            }

            // Lock is stuck - the pick is straining
            if (lockAngle >= allowed - 0.5f)
            {
                strain += dt;
                jiggle = UnityEngine.Random.Range(-4f, 4f);

                if (strain >= pickStrength) BreakPick();
            }
        }

        UpdateVisuals(jiggle);
    }

    private void BreakPick()
    {
        PlaySound(pickBreakSound);
        picksLeft--;
        ResetPick();

        if (picksLeft <= 0)
        {
            Close(false, "Out of lockpicks... take a breath and try again.");
            return;
        }

        status = $"<color=#ff6666>Snap! The pick broke.</color>";
    }

    private void UpdateVisuals(float jiggle)
    {
        if (lockCylinder != null) lockCylinder.localEulerAngles = new Vector3(0f, 0f, -lockAngle);
        if (pick != null) pick.localEulerAngles = new Vector3(0f, 0f, -pickAngle + jiggle);

        if (infoText != null)
        {
            infoText.text = $"Mouse: move pick   |   Hold {turnKey}: turn lock   |   {cancelKey}: give up\n" +
                            $"Lockpicks: {picksLeft}   {status}";
        }
    }

    private void Close(bool solved, string message)
    {
        IsOpen = false;
        if (panel != null) panel.SetActive(false);

        Time.timeScale = savedTimeScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (solved)
        {
            PlaySound(unlockSound);
            NotePopup.Show("Click. You're in.", 2f);
            onSolved?.Invoke();
        }
        else if (!string.IsNullOrEmpty(message))
        {
            NotePopup.Show(message, 3f);
        }
    }
}