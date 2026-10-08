using UnityEngine;

// Makes an NPC "say" something when the player walks up - e.g. the hotel
// receptionist. No collider needed; it just checks distance. Picks a
// random line each time, with a cooldown so it doesn't spam.
public class TalkTrigger : MonoBehaviour
{
    [Tooltip("Shown before the line, e.g. 'Receptionist'. Leave empty for none.")]
    [SerializeField] private string speakerName = "Receptionist";

    [TextArea(2, 4)]
    [SerializeField] private string[] lines =
    {
        "Welcome to the Grand! Do you have a reservation?",
        "Good evening. The lounge is to your left, the elevators to your right.",
        "Sir, the upper floor is for registered guests only."
    };

    [SerializeField] private float radius = 3f;
    [Tooltip("Seconds before she'll talk again")]
    [SerializeField] private float cooldown = 20f;
    [Tooltip("Turn to face the player while talking")]
    [SerializeField] private bool facePlayer = true;
    [SerializeField] private float turnSpeed = 180f;

    private Transform player;
    private bool playerWasNear;
    private float nextTalkTime;
    private Quaternion startRotation;

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        startRotation = transform.rotation;
    }

    private void Update()
    {
        if (player == null || lines == null || lines.Length == 0) return;

        bool near = player.gameObject.activeInHierarchy &&
                    Vector3.Distance(player.position, transform.position) <= radius;

        // Only on walking up (not every frame), and respecting the cooldown
        if (near && !playerWasNear && Time.time >= nextTalkTime)
        {
            nextTalkTime = Time.time + cooldown;
            string line = lines[Random.Range(0, lines.Length)];
            NotePopup.Show(string.IsNullOrEmpty(speakerName) ? line : $"<b>{speakerName}:</b> \"{line}\"", 4f);
        }
        playerWasNear = near;

        if (!facePlayer) return;

        Quaternion target = startRotation;
        if (near)
        {
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) target = Quaternion.LookRotation(dir);
        }
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
