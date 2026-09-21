using UnityEngine;

// Put this on the mob leader AND on the guards (a placeholder capsule
// works fine for testing - swap the model later, script doesn't
// change). Press the key up close for a knife takedown - no
// animation required, same functional-first approach as everything
// else. Check "Is Mob Leader" only on the actual target - guards just
// need to go down as obstacles, they don't count toward the mission.
public class EliminateTarget : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.I;
    [Tooltip("Check this only on the actual mob leader - leave unchecked on guards")]
    [SerializeField] private bool isMobLeader = true;

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log($"Press {interactKey} to eliminate the target");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }

    private void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            if (isMobLeader) Mission2Manager.Instance.OnTargetEliminated();
            gameObject.SetActive(false);
        }
    }
}
