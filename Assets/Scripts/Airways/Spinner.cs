using UnityEngine;

// Spins an object around one of its own axes - helicopter rotors,
// tail rotors, airplane propellers, windmills, fans...
// Put it directly on the rotor/propeller object (pivot at its center).
public class Spinner : MonoBehaviour
{
    [Tooltip("Local axis to spin around. Main rotor: (0,1,0). Tail rotor: usually (1,0,0). Propeller: usually (0,0,1).")]
    public Vector3 axis = Vector3.up;

    [Tooltip("Degrees per second")]
    public float speed = 1500f;

    private void Update()
    {
        transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
    }
}
