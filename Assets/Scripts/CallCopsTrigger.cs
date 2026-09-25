using UnityEngine;

// Put this on the call-cops interact point, near where the truck
// ends up stopped. Starts inactive in the scene - TruckEMP reveals
// it automatically once the truck's actually been disabled, so it
// can't be used early. Hold the interact key for holdDuration
// seconds near the truck to complete the mission - letting go or
// stepping away just pauses progress, doesn't reset it.
public class CallCopsTrigger : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.I;
    [SerializeField] private float holdDuration = 5f;

    private bool playerInRange;
    private float heldTime;
    private int lastLoggedSecond = -1; // TEMP DIAGNOSTIC - remove once a real UI bar exists, if wanted

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log($"Hold {interactKey} to call the cops");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }

    private void Update()
    {
        if (!playerInRange || !Input.GetKey(interactKey)) return;

        heldTime += Time.deltaTime;

        // TEMP DIAGNOSTIC - remove once a real UI bar exists, if wanted
        int second = Mathf.FloorToInt(heldTime);
        if (second > lastLoggedSecond)
        {
            lastLoggedSecond = second;
            Debug.Log($"Calling cops: {second}/{holdDuration}s");
        }

        if (heldTime >= holdDuration)
        {
            Mission4Manager.Instance.CompleteMission();
            gameObject.SetActive(false);
        }
    }
}
