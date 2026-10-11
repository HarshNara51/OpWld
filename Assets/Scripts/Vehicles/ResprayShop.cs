using UnityEngine;

// GTA Vice City style Pay 'n' Spray. Put it on an empty GameObject on the
// garage floor. Drive in and stop - no key needed: the screen goes black
// for a moment and the car comes out with a new colour (and repaired).
// Works with every drivable car (see CarPaint). No collider needed - it
// checks distance. Drive out and back in for another colour.
public class ResprayShop : MonoBehaviour
{
    [SerializeField] private float radius = 3.5f;

    [Tooltip("Car counts as stopped below this speed (m/s)")]
    [SerializeField] private float stoppedSpeed = 0.5f;

    [Tooltip("How long the car has to stand still before the respray starts")]
    [SerializeField] private float stopTime = 0.5f;

    [Tooltip("Also fix any damage")]
    [SerializeField] private bool alsoRepair = true;

    [Header("Fade")]
    [SerializeField] private float fadeTime = 0.4f;
    [Tooltip("Seconds of full black")]
    [SerializeField] private float blackTime = 1f;

    private VehicleInteraction trackedCar;
    private bool armed;     // car has come in from outside since the last respray
    private float stillTimer;
    private bool working;

    private void Update()
    {
        if (working) return;

        VehicleInteraction car = VehicleInteraction.Current;
        if (car != trackedCar)
        {
            // Getting into a car that's already parked inside doesn't count - drive in
            trackedCar = car;
            armed = car != null && !InRange(car.transform);
            stillTimer = 0f;
        }
        if (car == null) return;

        if (!InRange(car.transform))
        {
            armed = true;
            stillTimer = 0f;
            return;
        }
        if (!armed) return;

        Rigidbody rb = car.GetComponent<Rigidbody>();
        bool stopped = rb == null || rb.linearVelocity.magnitude < stoppedSpeed;
        stillTimer = stopped ? stillTimer + Time.deltaTime : 0f;
        if (stillTimer >= stopTime) Respray(car, rb);
    }

    private void Respray(VehicleInteraction car, Rigidbody rb)
    {
        armed = false;

        CarPaint paint = CarPaint.For(car.gameObject);
        if (!paint.AllowRespray)
        {
            NotePopup.Show("<b>Mechanic:</b> \"Sorry pal, can't paint this one.\"", 3f);
            return;
        }

        working = true;

        // Hold the car still while the screen is black
        bool wasKinematic = rb != null && rb.isKinematic;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        bool painted = false;
        ScreenFader.Instance.FadeOutIn(
            onBlack: () =>
            {
                if (car == null) return;
                painted = paint.Respray();
                if (alsoRepair && car.TryGetComponent(out VehicleHealth health)) health.Repair();
            },
            onDone: () =>
            {
                working = false;
                if (rb != null) rb.isKinematic = wasKinematic;

                if (!painted)
                    NotePopup.Show("<b>Mechanic:</b> \"Sorry pal, can't paint this one.\"", 3f);
                else
                    NotePopup.Show(alsoRepair
                        ? "<b>Mechanic:</b> \"New paint, all fixed up. Nobody'll recognise you!\""
                        : "<b>Mechanic:</b> \"New paint. Nobody'll recognise you!\"", 3f);
            },
            fadeTime: fadeTime,
            holdTime: blackTime);
    }

    private bool InRange(Transform t)
    {
        Vector3 o = t.position - transform.position;
        o.y = 0f;
        return o.magnitude <= radius;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
