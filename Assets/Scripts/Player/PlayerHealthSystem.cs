using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// On the Managers prefab (every scene). The player's health rules outside the
// Gas Station gunfight:
//  - Hit by a car on foot: damage by the car's speed (a fast car kills), plus a shove.
//  - Hard crashes while driving: hurt the driver, but never kill (stops at crashFloor).
//  - Very slow healing after a long while without damage. The Hospital gizmo heals fully.
//  - Zero health: in a running mission -> "You Died!" and the mission fails;
//    in free roam -> "You Died!", then you wake up outside the hospital.
public class PlayerHealthSystem : MonoBehaviour
{
    [Header("Slow healing")]
    [Tooltip("Seconds without damage before healing starts")]
    [SerializeField] private float regenDelay = 20f;
    [Tooltip("Health per second (100 health = 0.15 -> about 11 minutes from empty to full)")]
    [SerializeField] private float regenPerSecond = 0.15f;

    [Header("Hit by a car (on foot)")]
    [Tooltip("Slower than this (km/h) does nothing")]
    [SerializeField] private float hitMinKmh = 10f;
    [Tooltip("Damage per km/h above the minimum (2.2: 30 km/h = 44, 55 km/h = dead)")]
    [SerializeField] private float hitDamagePerKmh = 2.2f;
    [SerializeField] private float hitCooldown = 1.2f;
    [SerializeField] private float knockbackSpeed = 8f;

    [Header("Hard crashes (driving)")]
    [Tooltip("Impact speed (m/s) before the driver gets hurt (9 = about 32 km/h)")]
    [SerializeField] private float crashMinSpeed = 9f;
    [SerializeField] private float crashDamagePerSpeed = 2.2f;
    [Tooltip("Crashes never take health below this - they can't kill")]
    [SerializeField] private float crashFloor = 5f;

    public static Health PlayerHealth { get; private set; }
    public static Transform PlayerTransform { get; private set; }

    private CharacterController controller;
    private float nextHitAllowed;
    private readonly Dictionary<Transform, Vector3> lastPositions = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> velocities = new Dictionary<Transform, Vector3>();
    private readonly Collider[] nearby = new Collider[64];
    private readonly HashSet<Transform> seen = new HashSet<Transform>();
    private readonly List<Transform> stale = new List<Transform>();

    private void OnEnable() => VehicleHealth.Crashed += OnCrash;
    private void OnDisable()
    {
        VehicleHealth.Crashed -= OnCrash;
        if (PlayerHealth != null) PlayerHealth.Died -= OnDied;
    }

    // The Player is found by tag (it is inactive while driving, so keep the last one found)
    private bool FindPlayer()
    {
        if (PlayerHealth != null) return true;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return false;
        PlayerHealth = p.GetComponent<Health>();
        if (PlayerHealth == null) return false;
        PlayerTransform = p.transform;
        controller = p.GetComponent<CharacterController>();
        PlayerHealth.Died += OnDied;
        lastPositions.Clear(); velocities.Clear();
        return true;
    }

    private void Update()
    {
        if (!FindPlayer() || PlayerHealth.IsDead) return;

        // Very slow healing
        if (PlayerHealth.Current < PlayerHealth.Max && Time.time - PlayerHealth.LastDamageTime > regenDelay)
            PlayerHealth.Heal(regenPerSecond * Time.deltaTime);

        if (PlayerTransform.gameObject.activeInHierarchy && VehicleInteraction.Current == null)
            CheckCarHits();
        else { lastPositions.Clear(); velocities.Clear(); }
    }

    // ---------------- hit by a car ----------------

    private static Transform VehicleRoot(Collider c)
    {
        TrafficCar traffic = c.GetComponentInParent<TrafficCar>();
        if (traffic != null) return traffic.transform;
        SplineVehicle spline = c.GetComponentInParent<SplineVehicle>();
        if (spline != null) return spline.transform;
        VehicleInteraction drivable = c.GetComponentInParent<VehicleInteraction>();
        return drivable != null ? drivable.transform : null;
    }

    private void CheckCarHits()
    {
        Vector3 me = PlayerTransform.position;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // Track the speed of every vehicle nearby (traffic and mission trucks move their transform)
        seen.Clear();
        int n = Physics.OverlapSphereNonAlloc(me, 14f, nearby, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Transform root = VehicleRoot(nearby[i]);
            if (root == null || !seen.Add(root)) continue;
            Rigidbody rb = root.GetComponent<Rigidbody>();
            Vector3 v;
            if (rb != null && !rb.isKinematic) v = rb.linearVelocity;
            else v = lastPositions.TryGetValue(root, out Vector3 last) ? (root.position - last) / dt : Vector3.zero;
            velocities[root] = v;
            lastPositions[root] = root.position;
        }
        stale.Clear();
        foreach (var kv in lastPositions) if (!seen.Contains(kv.Key)) stale.Add(kv.Key);
        foreach (Transform t in stale) { lastPositions.Remove(t); velocities.Remove(t); }

        if (Time.time < nextHitAllowed || controller == null) return;

        // Touching the player's capsule (a little bigger, so a passing bumper counts)
        float r = controller.radius + 0.25f;
        Vector3 c = PlayerTransform.TransformPoint(controller.center);
        float half = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        n = Physics.OverlapCapsuleNonAlloc(c - Vector3.up * half, c + Vector3.up * half, r, nearby, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Transform root = VehicleRoot(nearby[i]);
            if (root == null || !velocities.TryGetValue(root, out Vector3 v)) continue;
            Vector3 toMe = me - root.position; toMe.y = 0f;
            if (toMe.sqrMagnitude < 0.0001f) toMe = -v;
            toMe.Normalize();
            float kmh = v.magnitude * 3.6f;
            if (kmh < hitMinKmh || Vector3.Dot(v, toMe) < 1.5f) continue; // must be driving INTO the player

            nextHitAllowed = Time.time + hitCooldown;
            AudioManager am = AudioManager.Instance;
            if (am != null) am.PlaySFXAt(am.carCrash, me + Vector3.up, 0.8f, Random.Range(0.95f, 1.1f), 40f);
            PlayerHealth.TakeDamage((kmh - hitMinKmh) * hitDamagePerKmh);
            Vector3 push = (v.normalized * 0.6f + toMe * 0.4f); push.y = 0f;
            StartCoroutine(Knockback(push.normalized, Mathf.Min(knockbackSpeed, v.magnitude * 0.7f)));
            break;
        }
    }

    private IEnumerator Knockback(Vector3 dir, float speed)
    {
        for (float t = 0f; t < 0.3f && controller != null; t += Time.deltaTime)
        {
            if (controller.enabled) controller.Move(dir * speed * (1f - t / 0.3f) * Time.deltaTime);
            yield return null;
        }
    }

    // ---------------- hard crashes ----------------

    private void OnCrash(VehicleHealth car, float impact)
    {
        VehicleInteraction driving = VehicleInteraction.Current;
        if (driving == null || driving.gameObject != car.gameObject || !FindPlayer() || PlayerHealth.IsDead) return;
        float damage = (impact - crashMinSpeed) * crashDamagePerSpeed;
        damage = Mathf.Min(damage, PlayerHealth.Current - crashFloor);
        if (damage > 0f) PlayerHealth.TakeDamage(damage);
    }

    // ---------------- zero health ----------------

    private void OnDied()
    {
        bool inMission = MissionStatus.IsMissionInProgress(); // decided now, before any fail screen
        StartCoroutine(DeathRoutine(inMission));
    }

    private IEnumerator DeathRoutine(bool inMission)
    {
        // Full death animation first
        yield return new WaitForSecondsRealtime(PlayerDeath.DeathAnimSeconds > 0f ? PlayerDeath.DeathAnimSeconds : 4.6f);

        if (inMission)
        {
            FailCurrentMission("You Died!"); // a mission that already failed itself ignores this
            yield break;
        }

        bool shown = false;
        if (MissionResultUI.Instance != null) MissionResultUI.Instance.ShowFailureStay("You Died!", () => shown = true);
        else shown = true;
        while (!shown) yield return null;

        if (ScreenFader.Instance != null) ScreenFader.Instance.FadeOutIn(Respawn, null, 0.6f, 1.2f);
        else Respawn();
    }

    private static void FailCurrentMission(string reason)
    {
        if (Mission1Manager.Instance != null) Mission1Manager.Instance.FailMission(reason);
        else if (Mission2Manager.Instance != null) Mission2Manager.Instance.FailMission(reason);
        else if (Mission3Manager.Instance != null) Mission3Manager.Instance.FailMission(reason);
        else if (Mission4Manager.Instance != null) Mission4Manager.Instance.FailMission(reason);
        else if (Mission5Manager.Instance != null) Mission5Manager.Instance.FailMission(reason);
    }

    // Wake up outside the hospital, full health
    private void Respawn()
    {
        if (PlayerTransform == null || PlayerHealth == null) return;
        HospitalHeal hospital = HospitalHeal.Instance;
        if (hospital != null)
        {
            if (controller != null) controller.enabled = false;
            PlayerTransform.SetPositionAndRotation(hospital.RespawnPosition, hospital.RespawnRotation);
            if (controller != null) controller.enabled = true;
            Physics.SyncTransforms();
        }
        PlayerHealth.Revive();
        PlayerDeath death = PlayerTransform.GetComponent<PlayerDeath>();
        if (death != null) death.Revive();
        NotePopup.Show(hospital != null ? "You woke up at the hospital." : "You're back on your feet.", 4f);
    }
}
