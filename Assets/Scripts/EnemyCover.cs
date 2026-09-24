using System.Collections;
using UnityEngine;

// Put this on each enemy. Starts idle wherever you place it (the
// "deal" formation) and does nothing until CombatActive turns true -
// call TriggerCombat() from this same enemy's Health.OnDeath so
// whichever one gets shot first alerts the whole group. Once
// triggered, runs once to Hidden Point, then cycles Hidden <-> Peek
// indefinitely, only firing back while peeking.
public class EnemyCover : MonoBehaviour
{
    // Shared by every enemy - one enemy's death flips this for all.
    public static bool CombatActive = false;

    [Header("Positions (place these by hand per enemy)")]
    [SerializeField] private Transform hiddenPoint;
    [SerializeField] private Transform peekPoint;
    [SerializeField] private float moveSpeed = 4f;

    [Header("Timing")]
    [SerializeField] private float hiddenDuration = 2f;
    [SerializeField] private float peekDuration = 1.5f;
    [Tooltip("Random delay before this enemy's first peek, once it reaches cover - keeps the group from peeking in unison")]
    [SerializeField] private float peekStagger = 1f;

    [Header("Firing at the player")]
    [SerializeField] private float fireRange = 30f;
    [SerializeField] private float fireInterval = 0.6f;
    [SerializeField] private float damage = 5f;
    [SerializeField] private LayerMask hitMask = ~0;
    [Tooltip("Aim at this height above the player's base position, matching their CharacterController's Center Y - without this, enemies aim at ground level (the player's feet pivot) instead of their body")]
    [SerializeField] private float aimHeightOffset = 0.9f;

    [Header("Ground snapping")]
    [Tooltip("Fixes enemies sinking underground if HiddenPoint/PeekPoint weren't placed at exactly the right height")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundSearchHeight = 50f;
    [Tooltip("Pushes the pivot up after snapping, since a Capsule's pivot is at its center, not its feet - default 1 matches Unity's default Capsule Collider (Height 2)")]
    [SerializeField] private float groundOffset = 1f;

    private Transform player;
    private Health playerHealth;
    private bool isPeeking;
    private float nextFireTime;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<Health>();
        }

        StartCoroutine(WaitThenEngage());
    }

    // Wire this to this same enemy's own Health > On Death.
    public void TriggerCombat()
    {
        CombatActive = true;
    }

    // Wire this too, as a second listener on the same Health > On Death,
    // alongside Mission4Manager.OnEnemyDown - stops the peek/hide loop
    // and firing dead instead of continuing forever.
    public void OnDeath()
    {
        gameObject.SetActive(false);
    }

    private IEnumerator WaitThenEngage()
    {
        // Stand idle in the starting formation until someone gets shot.
        while (!CombatActive) yield return null;

        // Run to cover for the first time
        yield return MoveTo(hiddenPoint.position, 0f);
        yield return new WaitForSeconds(Random.Range(0f, peekStagger));

        // Then peek/hide indefinitely
        while (true)
        {
            isPeeking = false;
            yield return MoveTo(hiddenPoint.position, hiddenDuration);

            isPeeking = true;
            yield return MoveTo(peekPoint.position, peekDuration);
        }
    }

    private IEnumerator MoveTo(Vector3 target, float holdDuration)
    {
        target = SnapToGround(target);

        while (Vector3.Distance(transform.position, target) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }
        if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);
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

    private void Update()
    {
        if (!isPeeking || !CombatActive || player == null) return;
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
                Debug.Log($"{name} shoots at player");
                if (playerHealth != null) playerHealth.TakeDamage(damage);
            }
        }
    }
}