using UnityEngine;

// Put this on each of the 5 clue gizmos. Order doesn't matter - all
// 5 are active from the start, unlike Mission 1's sequential cargo.
public class ClueDiscovery : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.I;

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log($"Press {interactKey} to examine this clue");
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
            Mission3Manager.Instance.OnClueFound();
            gameObject.SetActive(false);
        }
    }
}
