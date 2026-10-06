using UnityEngine;

// TEMPORARY - just for testing the health bar before real combat
// exists. Put this on the player, press the key to apply damage.
// Delete this component (or the whole script) once real gunfire
// calls TakeDamage() for you instead.
// Only works in the Unity Editor, so it can never fire in a real build.
public class HealthDebugKey : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private KeyCode damageKey = KeyCode.F9;
    [SerializeField] private float damageAmount = 10f;

    private void Update()
    {
        if (!Application.isEditor) return;

        if (health != null && Input.GetKeyDown(damageKey))
        {
            health.TakeDamage(damageAmount);
        }
    }
}
