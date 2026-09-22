using UnityEngine;
using UnityEngine.Events;

// Generic reusable health component - put this on the player and on
// each enemy. Percent01 matches SuspicionManager's own shape on
// purpose, so both can eventually drive the same fill-bar UI design
// during polishing (health bar and suspicion bar, same component).
public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Wire this per-object - e.g. an enemy calls Mission4Manager.OnEnemyDown, the player calls a method that fails the mission")]
    [SerializeField] private UnityEvent onDeath;

    private float currentHealth;
    private bool isDead;

    public float Percent01 => currentHealth / maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log($"{name} took {amount:F0} damage ({currentHealth:F0}/{maxHealth:F0} left)");

        if (currentHealth <= 0f)
        {
            isDead = true;
            Debug.Log($"{name} is down.");
            onDeath?.Invoke();
        }
    }
}
