using UnityEngine;

// Put this on the secluded defuse-spot trigger. Works from on foot
// or from the car - no need to get out to defuse it.
public class DefuseTrigger : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.I;
    [SerializeField] private PuzzleMinigame defusePuzzle;

    private bool inRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") && !other.CompareTag("PlayerCar")) return;
        inRange = true;
        Debug.Log($"Press {interactKey} to defuse the bomb");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") && !other.CompareTag("PlayerCar")) return;
        inRange = false;
    }

    private void Update()
    {
        if (inRange && Input.GetKeyDown(interactKey))
        {
            defusePuzzle.Open();
        }
    }
}
