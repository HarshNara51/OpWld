using UnityEngine;

// Put this a bit away from where the trucks permanently stop, so the
// player can photograph them unloading. Works from on foot or from
// the car - no need to get out to take the shot.
public class PhotoCaptureZone : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.I;

    private bool playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") && !other.CompareTag("PlayerCar")) return;
        playerInRange = true;
        Debug.Log($"Press {interactKey} to take a photo");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") && !other.CompareTag("PlayerCar")) return;
        playerInRange = false;
    }

    private void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            Mission3Manager.Instance.OnPhotoTaken();
        }
    }
}
