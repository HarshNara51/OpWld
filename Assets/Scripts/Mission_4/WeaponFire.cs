using System.Collections;
using UnityEngine;

// Put this on the player, alongside WeaponHolster. Hold Fire Key for
// full-auto hitscan shooting - only works while a weapon is actually
// equipped. Headshots (HeadHitbox) instantly kill; body shots
// (plain Health) apply normal damage. Includes ammo + reload.
public class WeaponFire : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera fireCamera; // usually Main Camera
    [SerializeField] private WeaponHolster holster;

    [Header("Shooting")]
    [SerializeField] private KeyCode fireKey = KeyCode.Mouse0;
    [SerializeField] private float fireRate = 0.1f; // seconds between shots
    [SerializeField] private float range = 100f;
    [SerializeField] private float bodyDamage = 30f;
    [Tooltip("Headshot damage = Body Damage x this multiplier - not an instant kill, so spamming headshots still takes a few hits")]
    [SerializeField] private float headshotMultiplier = 4f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Ammo")]
    [SerializeField] private int magazineSize = 30;
    [SerializeField] private float reloadSeconds = 2f;
    [SerializeField] private KeyCode reloadKey = KeyCode.R;

    public int CurrentAmmo { get; private set; }
    public int MagazineSize => magazineSize;
    public bool IsReloading { get; private set; }

    private float nextFireTime;

    private void Awake()
    {
        CurrentAmmo = magazineSize;
    }

    private void Update()
    {
        if (holster == null || !holster.IsWeaponEquipped) return;
        if (IsReloading) return;

        if (Input.GetKeyDown(reloadKey) && CurrentAmmo < magazineSize)
        {
            StartCoroutine(Reload());
            return;
        }

        if (Input.GetKey(fireKey) && Time.time >= nextFireTime)
        {
            if (CurrentAmmo <= 0)
            {
                Debug.Log("Out of ammo - press R to reload");
                return;
            }

            nextFireTime = Time.time + fireRate;
            Fire();
        }
    }

    private void Fire()
    {
        CurrentAmmo--;

        // A gunshot alerts everyone regardless of whether it hits -
        // this is the trigger that ends the "deal" formation.
        if (!EnemyCover.CombatActive)
        {
            EnemyCover.CombatActive = true;
            Debug.Log("Shots fired - they're alerted now.");
        }

        Vector3 origin = fireCamera.transform.position;
        Vector3 direction = fireCamera.transform.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask))
        {
            HeadHitbox head = hit.collider.GetComponent<HeadHitbox>();
            if (head != null && head.Health != null)
            {
                float headshotDamage = bodyDamage * headshotMultiplier;
                Debug.Log($"Headshot! ({headshotDamage:F0} damage)");
                head.Health.TakeDamage(headshotDamage);
                return;
            }

            Health health = hit.collider.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(bodyDamage);
            }
        }
    }

    private IEnumerator Reload()
    {
        IsReloading = true;
        Debug.Log("Reloading...");
        yield return new WaitForSeconds(reloadSeconds);
        CurrentAmmo = magazineSize;
        IsReloading = false;
        Debug.Log("Reloaded.");
    }
}