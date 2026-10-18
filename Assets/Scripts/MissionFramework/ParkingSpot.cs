using UnityEngine;

// Put this on a parking spot's trigger box (next to its "Park Here"
// gizmo). It knows whether the player's car is parked in it: inside the
// box, and nobody driving it. Missions ask CarParked before moving on.
[RequireComponent(typeof(BoxCollider))]
public class ParkingSpot : MonoBehaviour
{
    [Tooltip("Shown once, the first time the car is parked here and the player gets out. Empty = nothing.")]
    [TextArea(2, 4)]
    [SerializeField] private string parkedMessage = "Car parked. Continue on foot.";

    private BoxCollider box;
    private VehicleInteraction car;
    private bool wasParked;
    private bool messageShown;

    public bool CarParked
    {
        get
        {
            FindCar();
            if (car == null || car.IsDriving) return false;
            if (box == null) box = GetComponent<BoxCollider>();
            // The car's middle inside the (rotated) box; a bit of height slack for slopes
            Vector3 p = transform.InverseTransformPoint(car.transform.position) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.z) <= half.z && Mathf.Abs(p.y) <= half.y + 2f;
        }
    }

    private void Update()
    {
        bool parked = CarParked;
        if (parked && !wasParked && !messageShown && !string.IsNullOrEmpty(parkedMessage))
        {
            messageShown = true;
            NotePopup.Show(parkedMessage, 4f);
        }
        wasParked = parked;
    }

    // The real car tagged PlayerCar (not a look-alike dummy, e.g. on the tow truck)
    private void FindCar()
    {
        if (car != null) return;
        foreach (GameObject carObj in GameObject.FindGameObjectsWithTag("PlayerCar"))
            if (carObj.TryGetComponent(out car)) return;
    }
}
