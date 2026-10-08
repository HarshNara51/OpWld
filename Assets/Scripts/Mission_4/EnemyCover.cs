using System.Collections;
using UnityEngine;

// Put this on each enemy. Starts idle wherever you place it (the
// "deal" formation) and does nothing until CombatActive turns true -
// call TriggerCombat() from this same enemy's Health.OnDeath so
// whichever one gets shot first alerts the whole group. Once
// triggered, runs once to Hidden Point, then cycles Hidden <-> Peek
// indefinitely, firing whenever his shot can reach the player - from
// the peek spot, or from the hidden spot if the player has flanked him.
public class EnemyCover : MonoBehaviour
{
    // Shared by every enemy - one enemy's death flips this for all.
    public static bool CombatActive = false;

    [Header("Positions (place these by hand per enemy)")]
    [SerializeField] private Transform hiddenPoint;
    [SerializeField] private Transform peekPoint;
    [Tooltip("Running to cover - matches the Rifle Run animation (~4 m/s)")]
    [SerializeField] private float moveSpeed = 4f;
    [Tooltip("Side-stepping between Hidden and Peek - slow, so the side step animation doesn't slide")]
    [SerializeField] private float peekMoveSpeed = 0.7f;
    [SerializeField] private float turnSpeed = 540f;
    [Tooltip("The rifle aiming pose holds the barrel ~15 deg left of where the body faces - turn this much extra so the barrel, not the body, points at the player")]
    [SerializeField] private float aimYawOffset = 15f;

    [Header("Timing")]
    [SerializeField] private float hiddenDuration = 2f;
    [SerializeField] private float peekDuration = 1.5f;
    [Tooltip("Random delay before this enemy's first peek, once it reaches cover - keeps the group from peeking in unison")]
    [SerializeField] private float peekStagger = 1f;

    [Header("Firing at the player")]
    [Tooltip("Match the player's WeaponFire range, so they can't snipe from beyond reach")]
    [SerializeField] private float fireRange = 100f;
    [SerializeField] private float fireInterval = 1f;
    [SerializeField] private float damage = 3f;
    [Tooltip("Chance a shot hits within Close Range - drops to Long Range Hit Chance at Fire Range")]
    [Range(0f, 1f)][SerializeField] private float closeRangeHitChance = 0.6f;
    [Range(0f, 1f)][SerializeField] private float longRangeHitChance = 0.05f;
    [SerializeField] private float closeRange = 10f;
    [SerializeField] private LayerMask hitMask = ~0;
    [Tooltip("Aim at this height above the player's base position, matching their CharacterController's Center Y - without this, enemies aim at ground level (the player's feet pivot) instead of their body")]
    [SerializeField] private float aimHeightOffset = 0.9f;

    [Header("Ground snapping")]
    [Tooltip("Fixes enemies sinking underground if HiddenPoint/PeekPoint weren't placed at exactly the right height")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundSearchHeight = 50f;
    [Tooltip("Pushes the pivot up after snapping, since a Capsule's pivot is at its center, not its feet - default 1 matches Unity's default Capsule Collider (Height 2)")]
    [SerializeField] private float groundOffset = 1f;

    [Header("Animation (character model child with an Enemy_Rifle Animator)")]
    [Tooltip("Natural speeds of the clips in the Enemy_Rifle blend tree - used to speed the animation up/down to match the movement")]
    [SerializeField] private float runClipSpeed = 3.98f;
    [SerializeField] private float stepRightClipSpeed = 0.40f;
    [SerializeField] private float stepLeftClipSpeed = 0.24f;

    private Transform player;
    private Health playerHealth;
    private bool facePlayer;   // false while running to cover (faces where he's running)
    private float nextFireTime;

    private Animator animator;
    private Vector3 lastPosition;
    private Vector3 smoothedVelocity;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<Health>();
        }

        transform.position = SnapToGround(transform.position); // feet on the ground in the deal formation too
        animator = GetComponentInChildren<Animator>();
        lastPosition = transform.position;

        StartCoroutine(WaitThenEngage());
    }

    // Wire this to this same enemy's own Health > On Death.
    public void TriggerCombat()
    {
        CombatActive = true;
    }

    // Wire this too, as a second listener on the same Health > On Death,
    // alongside Mission4Manager.OnEnemyDown - stops the peek/hide loop
    // and firing. With an NPCDeath he plays his death animation (the
    // headshot one if the killing shot hit the head) before disappearing.
    public void OnDeath()
    {
        StopAllCoroutines();
        enabled = false;

        Health health = GetComponent<Health>();
        if (TryGetComponent(out NPCDeath death)) death.Die(health != null && health.KilledByHeadshot);
        else gameObject.SetActive(false);
    }

    private IEnumerator WaitThenEngage()
    {
        // Stand idle in the starting formation until someone gets shot.
        while (!CombatActive) yield return null;

        // Run to cover for the first time
        yield return MoveTo(hiddenPoint.position, 0f, moveSpeed);
        facePlayer = true;
        yield return new WaitForSeconds(Random.Range(0f, peekStagger));

        // Then peek/hide indefinitely
        while (true)
        {
            yield return MoveTo(hiddenPoint.position, hiddenDuration, peekMoveSpeed);
            yield return MoveTo(peekPoint.position, peekDuration, peekMoveSpeed);
        }
    }

    private IEnumerator MoveTo(Vector3 target, float holdDuration, float speed)
    {
        target = SnapToGround(target);

        while (Vector3.Distance(transform.position, target) > 0.05f)
        {
            if (!facePlayer) TurnTowards(target);
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            yield return null;
        }
        if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);
    }

    private void TurnTowards(Vector3 point, float yawOffset = 0f)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, yawOffset, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    // Runs after the movement coroutine each frame: turns him toward the
    // player and tells the Animator how he's moving, in his own local
    // space (MoveX = sideways, MoveZ = forwards).
    private void LateUpdate()
    {
        if (facePlayer && player != null) TurnTowards(player.position, aimYawOffset);

        if (animator == null || Time.deltaTime <= 0f) return;

        Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, 10f * Time.deltaTime);
        Vector3 local = transform.InverseTransformDirection(smoothedVelocity);

        // Peeking is always a side step. The peek point is rarely exactly
        // sideways, and the blend tree has no backwards clip - a little
        // backwards drift would mix "step left" with "step right" and the
        // legs cancel out (sliding). So only the sideways part counts here.
        if (facePlayer) local.z = 0f;

        animator.SetBool("Alert", CombatActive);
        animator.SetFloat("MoveX", local.x);
        animator.SetFloat("MoveZ", local.z);

        // Speed the clip up/down so the feet match how fast he really moves
        float animSpeed = 1f;
        if (Mathf.Abs(local.z) > 0.5f) animSpeed = Mathf.Abs(local.z) / runClipSpeed;
        else if (local.x > 0.1f) animSpeed = local.x / stepRightClipSpeed;
        else if (local.x < -0.1f) animSpeed = -local.x / stepLeftClipSpeed;
        animator.SetFloat("MoveAnimSpeed", Mathf.Clamp(animSpeed, 0.6f, 2.5f));
    }

    private Vector3 SnapToGround(Vector3 pos)
    {
        Vector3 rayOrigin = pos + Vector3.up * groundSearchHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundSearchHeight * 2f, groundMask))
        {
            pos.y = hit.point.y + groundOffset;
        }
        return pos;
    }

    // Fires while peeking - and also while "hidden" if his shot can still
    // reach the player (the player flanked him, so his cover is useless).
    // Real cover blocks the raycast below, so he stays quiet behind it.
    private void Update()
    {
        if (!CombatActive || !facePlayer || player == null) return; // facePlayer = reached cover
        if (Time.time < nextFireTime) return;

        Vector3 aimPoint = player.position + Vector3.up * aimHeightOffset;
        Vector3 toPlayer = aimPoint - transform.position;
        float dist = toPlayer.magnitude;
        if (dist > fireRange) return;

        if (Physics.Raycast(transform.position, toPlayer.normalized, out RaycastHit hit, fireRange, hitMask))
        {
            // Scene-view only, not a real effect - red if it hit something
            // else (blocked), green if it actually reached the player.
            bool hitPlayer = hit.collider.transform == player || hit.collider.transform.IsChildOf(player);
            Debug.DrawLine(transform.position, hit.point, hitPlayer ? Color.green : Color.red, 0.5f);

            if (hitPlayer)
            {
                nextFireTime = Time.time + fireInterval;

                // Accuracy falls off with distance - long range is safer, not free
                float t = Mathf.InverseLerp(closeRange, fireRange, dist);
                bool lands = Random.value < Mathf.Lerp(closeRangeHitChance, longRangeHitChance, t);
                Debug.Log($"{name} shoots at player ({dist:F0} m) - {(lands ? "hit" : "miss")}");
                if (lands && playerHealth != null) playerHealth.TakeDamage(damage);
            }
        }
    }
}