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

    [Tooltip("Show a health bar over this one's head (never shown for the player)")]
    [SerializeField] private bool showHealthBar = true;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        bool isPlayer = CompareTag("Player") || GetComponentInParent<PlayerDeath>() != null;
        if (showHealthBar && !isPlayer) EnemyHealthBars.Register(this);
    }

    private float nextHurtSound;

    // Grunt on a hit (at most every half second), a lower one on death
    private void PlayHurt(bool dying)
    {
        AudioManager am = AudioManager.Instance;
        if (am == null || (!dying && Time.time < nextHurtSound)) return;
        nextHurtSound = Time.time + 0.5f;
        bool isPlayer = CompareTag("Player") || GetComponentInParent<PlayerDeath>() != null;
        am.PlaySFXAt(isPlayer ? am.playerHurt : am.npcHurt, transform.position + Vector3.up * 1.5f, isPlayer ? 0.9f : 0.75f,
                     dying ? Random.Range(0.8f, 0.88f) : Random.Range(0.95f, 1.1f), 40f);
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, false);
    }

    public void TakeDamage(float amount, bool headshot)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (amount > 0f) PlayHurt(currentHealth <= 0f);

        if (currentHealth <= 0f)
        {
            isDead = true;
            KilledByHeadshot = headshot;
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
        PlayHurt(true);
        Died?.Invoke(); // first, so e.g. PlayerDeath is set before the mission reacts
        onDeath?.Invoke();
    }
}