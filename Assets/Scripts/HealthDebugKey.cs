using UnityEngine;

// TEMPORARY - just for testing the health bar before real combat
// exists. Put this on the player, press the key to apply damage.
// Delete this component (or the whole script) once real gunfire
// calls TakeDamage() for you instead.
public class HealthDebugKey : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private KeyCode damageKey = KeyCode.H;
    [SerializeField] private float damageAmount = 10f;

    private void Update()
    {
        if (health != null && Input.GetKeyDown(damageKey))
        {
            health.TakeDamage(damageAmount);
        }
    }
}
