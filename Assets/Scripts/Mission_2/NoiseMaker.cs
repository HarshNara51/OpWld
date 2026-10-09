using UnityEngine;

// A distraction: a vase to knock over, a radio, a service bell, a
// railing to bang on... Put it on an object with a trigger collider.
// The player presses the key nearby and the assigned guard(s) come to
// investigate along their route.
public class NoiseMaker : MonoBehaviour
{

    [Tooltip("Guards who hear this noise and come to check")]
    [SerializeField] private GuardAI[] guardsToAlert;

    [Tooltip("Shown on screen when the noise is made")]
    [TextArea(2, 4)]
    [SerializeField] private string note = "CRASH! That should get someone's attention...";

    [Tooltip("Optional sound (plays at this spot)")]
    [SerializeField] private AudioClip noiseSound;

    [Tooltip("Seconds before it can be used again")]
    [SerializeField] private float cooldown = 8f;

    [SerializeField] private bool singleUse = false;

    private bool playerInRange;
    private bool used;
    private float nextAllowedTime;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || (singleUse && used)) return;
        playerInRange = true;
        Debug.Log($"Press {GameKeys.Label(GameAction.Interact)} to make a distraction");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }

    private void Update()
    {
        if (!playerInRange || !GameKeys.Down(GameAction.Interact)) return;
        if (singleUse && used) return;
        if (Time.time < nextAllowedTime) return;

        used = true;
        nextAllowedTime = Time.time + cooldown;

        foreach (GuardAI guard in guardsToAlert)
        {
            if (guard != null && guard.isActiveAndEnabled) guard.Investigate();
        }

        NotePopup.Show(note, 3f);
        if (noiseSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXAt(noiseSound, transform.position);
        }
    }
}
