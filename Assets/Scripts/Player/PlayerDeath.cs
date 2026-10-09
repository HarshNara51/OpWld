using UnityEngine;

// Put this on the player root (next to Health). When health hits zero:
// plays the death animation, puts the weapon away and switches off
// movement, shooting and weapon keys. The camera keeps orbiting so the
// player can watch. The mission's own fail screen comes from Health's
// On Death event as before.
public class PlayerDeath : MonoBehaviour
{
    // Other scripts (cars etc.) check this instead of finding the player's Health
    public static bool IsDead { get; private set; }

    // How long the death animation takes - the fail screen waits this long
    // on top of its usual time (full animation, then the usual 5 s)
    public static float DeathAnimSeconds { get; private set; }

    [SerializeField] private string dieTrigger = "Die";
    [Tooltip("Length of the death clip in the Player controller (Dying Backwards = 4.6 s)")]
    [SerializeField] private float deathAnimSeconds = 4.6f;

    private Health health;

    private void Awake()
    {
        IsDead = false; // static - reset for every scene/replay
        DeathAnimSeconds = deathAnimSeconds;
        health = GetComponent<Health>();
        if (health != null) health.Died += OnDied;
    }

    private void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
    }

    private void OnDied()
    {
        IsDead = true;

        // Weapon away first (rifle can't float around the dead body)
        WeaponHolster holster = GetComponent<WeaponHolster>();
        if (holster != null) holster.HolsterAll();

        // No more controls
        foreach (MonoBehaviour b in new MonoBehaviour[] { GetComponent<PlayerLocomotion>(), GetComponent<WeaponFire>(), GetComponent<PlayerKnife>(), holster })
            if (b != null) b.enabled = false;

        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            for (int i = 1; i < animator.layerCount; i++) animator.SetLayerWeight(i, 0f); // drop the aim/reload upper body
            animator.SetTrigger(dieTrigger);
        }
    }
}
