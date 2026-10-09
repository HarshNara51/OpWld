using System;
using UnityEngine;

// Put this on the ROOT of every drivable car (the object with the
// Rigidbody + VehicleInteraction). Crashes and bullets damage it; smoke
// starts when it's badly hurt; at 0 it's wrecked (can't be driven) until
// it's repaired or towed.
public class VehicleHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    [Header("Crash damage")]
    [Tooltip("Impacts slower than this (m/s) do no damage - normal bumps are free")]
    [SerializeField] private float minImpactSpeed = 6f;
    [Tooltip("Damage per m/s above the minimum")]
    [SerializeField] private float damagePerImpactSpeed = 2.5f;

    [Header("Mission cars")]
    [Tooltip("Untick for cars a mission depends on (taxi, bomb car): they smoke but never wreck")]
    [SerializeField] private bool canBeWrecked = true;

    [Header("Smoke (optional particle systems at the engine)")]
    [SerializeField] private ParticleSystem lightSmoke;
    [SerializeField] private ParticleSystem heavySmoke;
    [Tooltip("Light smoke below this fraction of health")]
    [Range(0f, 1f)] [SerializeField] private float lightSmokeBelow = 0.35f;
    [Tooltip("Heavy smoke below this fraction of health")]
    [Range(0f, 1f)] [SerializeField] private float heavySmokeBelow = 0.15f;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public float Health01 => maxHealth > 0f ? Current / maxHealth : 0f;
    public bool IsWrecked { get; private set; }

    // For the speedometer HUD later
    public event Action<float> HealthChanged;

    private VehicleInteraction interaction;

    private void Awake()
    {
        interaction = GetComponent<VehicleInteraction>();
        Current = maxHealth;
        UpdateSmoke();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsWrecked) return;

        // Don't hurt the car for bumping the player on foot
        if (collision.collider.CompareTag("Player")) return;

        float impact = collision.relativeVelocity.magnitude;
        if (impact < minImpactSpeed) return;

        TakeDamage((impact - minImpactSpeed) * damagePerImpactSpeed);
    }

    // Bullets, explosions, anything else can call this too
    public void TakeDamage(float amount)
    {
        if (IsWrecked || amount <= 0f) return;

        float floor = canBeWrecked ? 0f : 1f; // mission cars never drop to 0
        Current = Mathf.Max(floor, Current - amount);
        HealthChanged?.Invoke(Health01);
        UpdateSmoke();

        if (Current <= 0f) Wreck();
    }

    private void Wreck()
    {
        IsWrecked = true;
        Debug.Log($"{name} is wrecked!");

        bool playerInside = interaction != null && VehicleInteraction.Current == interaction;
        if (interaction != null)
        {
            if (playerInside) interaction.ForceExit();
            interaction.DisableForGood();
        }

        NotePopup.Show("Your car's wrecked! It won't drive again until it's fixed.", 4f);
    }

    // Repair shop / tow service
    public void Repair()
    {
        IsWrecked = false;
        Current = maxHealth;
        HealthChanged?.Invoke(Health01);
        UpdateSmoke();
        if (interaction != null) interaction.Restore();
    }

    private void UpdateSmoke()
    {
        float h = Health01;
        SetEffect(lightSmoke, h <= lightSmokeBelow && h > heavySmokeBelow);
        SetEffect(heavySmoke, h <= heavySmokeBelow);
    }

    private static void SetEffect(ParticleSystem ps, bool on)
    {
        if (ps == null) return;
        if (on && !ps.isPlaying) ps.Play();
        else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
