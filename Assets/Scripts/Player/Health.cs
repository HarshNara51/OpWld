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

    // For scripts (e.g. PlayerDeath) - same moment as On Death
    public event System.Action Died;

    // True if the hit that brought health to zero was a headshot -
    // lets the death animation pick the headshot version.
    public bool KilledByHeadshot { get; private set; }

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, false);
    }

    public void TakeDamage(float amount, bool headshot)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log($"{name} took {amount:F0} damage ({currentHealth:F0}/{maxHealth:F0} left)");

        if (currentHealth <= 0f)
        {
            isDead = true;
            KilledByHeadshot = headshot;
            Debug.Log($"{name} is down.");
            Died?.Invoke(); // first, so e.g. PlayerDeath is set before the mission reacts
            onDeath?.Invoke();
        }
    }

    // For instant kills (e.g. headshots) - cleaner than faking it
    // with a huge TakeDamage amount.
    public void Kill()
    {
        if (isDead) return;

        currentHealth = 0f;
        isDead = true;
        Debug.Log($"{name} is down.");
        Died?.Invoke(); // first, so e.g. PlayerDeath is set before the mission reacts
        onDeath?.Invoke();
    }
}