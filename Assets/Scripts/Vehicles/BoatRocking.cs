using UnityEngine;

// Gentle idle rocking for a moored boat: roll side to side, a little pitch
// fore/aft and a slow bob up and down. Added by Tools > Boats > Add Lights +
// Rocking. Works around wherever the boat was placed in the scene (it
// remembers the start pose), so move the boat freely in edit mode.
public class BoatRocking : MonoBehaviour
{
    [Tooltip("Which local axis points to the bow (Scout_Boat's bow is -X)")]
    [SerializeField] private Vector3 forwardAxis = Vector3.left;

    [Tooltip("Side-to-side lean, degrees")]
    [SerializeField] private float rollAngle = 2.5f;
    [SerializeField] private float rollPeriod = 5f;
    [Tooltip("Bow up/down, degrees")]
    [SerializeField] private float pitchAngle = 1f;
    [SerializeField] private float pitchPeriod = 7f;
    [Tooltip("Up/down bob, metres")]
    [SerializeField] private float bob = 0.12f;
    [SerializeField] private float bobPeriod = 4f;

    private Vector3 basePos;
    private Quaternion baseRot;
    private float phase;

    private void OnEnable()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        phase = Random.value * 100f; // several boats don't rock in sync
    }

    private void OnDisable()
    {
        transform.localPosition = basePos;
        transform.localRotation = baseRot;
    }

    private void Update()
    {
        float t = Time.time + phase;
        // Two sines per motion so it never looks like a metronome
        float roll = rollAngle * (Mathf.Sin(t * 2f * Mathf.PI / rollPeriod) * 0.8f + Mathf.Sin(t * 2f * Mathf.PI / (rollPeriod * 0.43f)) * 0.2f);
        float pitch = pitchAngle * Mathf.Sin(t * 2f * Mathf.PI / pitchPeriod + 1.3f);
        float up = bob * (Mathf.Sin(t * 2f * Mathf.PI / bobPeriod) * 0.7f + Mathf.Sin(t * 2f * Mathf.PI / (bobPeriod * 0.61f)) * 0.3f);

        Vector3 side = Vector3.Cross(Vector3.up, forwardAxis).normalized; // the boat's local "right"
        transform.localRotation = baseRot * Quaternion.AngleAxis(roll, forwardAxis) * Quaternion.AngleAxis(pitch, side);
        transform.localPosition = basePos + Vector3.up * up;
    }
}
