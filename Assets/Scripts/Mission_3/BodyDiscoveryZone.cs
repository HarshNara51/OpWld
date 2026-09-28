using UnityEngine;

// Put this on the trigger zone around the body, replacing
// FindBodyTrigger (delete that one - this replaces it).
// Press the key to "photograph" the body - this is what actually
// starts the countdown and the trucks moving, not just walking in.
public class BodyDiscoveryZone : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.I;

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log($"Press {interactKey} to photograph the body");
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
            Mission3Manager.Instance.OnBodyFound();
            if (Mission3Manager.Instance.BodyFound)
            {
                gameObject.SetActive(false);
            }
        }
    }
}