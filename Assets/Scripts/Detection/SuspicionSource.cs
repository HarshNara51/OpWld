using UnityEngine;

// Put this on each parked cop/thief placeholder (a cube for now).
public class SuspicionSource : MonoBehaviour
{
    [Tooltip("Distance at which this source starts detecting the player")]
    public float detectRadius = 15f;

    // Draws a red circle in the Scene view when selected, so you can
    // see and place the detection range visually instead of guessing.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}