using UnityEngine;

// Put this on the mob leader AND on the guards. Press the key up close
// for a knife takedown. Guards (anything with a GuardAI) can only be
// taken down from behind. Check "Is Mob Leader" only on the actual
// target - guards just need to go down as obstacles.
public class EliminateTarget : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Tooltip("Check this only on the actual mob leader - leave unchecked on guards")]
    [SerializeField] private bool isMobLeader = true;

    [Tooltip("Suspicion added by the takedown itself (a struggle makes some noise)")]
    [SerializeField] private float takedownSuspicion = 15f;

    private bool playerInRange;
    private Transform player;
    private GuardAI guard;

    private void Awake()
    {
        guard = GetComponent<GuardAI>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        player = other.transform;
        Debug.Log($"Press {interactKey} to take him down");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }

    private void Update()
    {
        if (!playerInRange || !Input.GetKeyDown(interactKey)) return;

        if (guard != null && player != null && !guard.CanBeTakenDownBy(player.position))
        {
            NotePopup.Show("He'll see you coming. Get behind him first.", 3f);
            return;
        }

        if (SuspicionManager.Instance != null) SuspicionManager.Instance.AddSuspicion(takedownSuspicion);

        if (isMobLeader) Mission2Manager.Instance.OnTargetEliminated();

        if (guard != null) guard.TakeDown();
        else gameObject.SetActive(false);
    }
}