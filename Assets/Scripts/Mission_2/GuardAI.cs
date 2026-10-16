using System.Collections.Generic;
using UnityEngine;

// Put this on each guard. The guard stands at his post (wherever he is
// placed in the scene) until a NoiseMaker calls Investigate(). He then
// walks his Route (waypoints, in order) to the last point, looks around
// for a while, and walks back the same way to his post.
//
// The walk back is the takedown window: EliminateTarget only allows a
// takedown from behind. If the player is standing near the investigate
// spot when he arrives, he questions them (on-screen line + suspicion).
//
// Guards watch each other: if he SEES another guard being taken down
// (close enough, in his view, no wall between), he raises the alarm and
// the mission fails like being caught.
public class GuardAI : MonoBehaviour
{
    public enum State { OnPost, Investigating, Looking, Returning, Caught, Down }

    [Header("Investigate route")]
    [Tooltip("Empty GameObjects from the post to the investigate spot, in order. The LAST one is where he stops. Must NOT be children of the guard.")]
    [SerializeField] private Transform[] route;
    [Tooltip("Optional - what he faces while looking around (e.g. a point in the lobby below)")]
    [SerializeField] private Transform lookTarget;
    [SerializeField] private float walkSpeed = 2.2f;
    [SerializeField] private float turnSpeed = 300f;
    [Tooltip("How long he looks around before heading back")]
    [SerializeField] private float lookSeconds = 5f;

    [Header("Roaming (no distraction needed)")]
    [Tooltip("Walks his route on his own, over and over - e.g. the mob leader pacing his room")]
    [SerializeField] private bool roamOnHisOwn = false;
    [Tooltip("Random pause at his starting spot before each walk (min, max seconds)")]
    [SerializeField] private Vector2 pauseAtPost = new Vector2(3f, 6f);

    [Header("Questioning the player")]
    [Tooltip("If the player is this close when he arrives, he questions them")]
    [SerializeField] private float questionRadius = 5f;
    [TextArea(2, 4)]
    [SerializeField] private string questionLine = "Hey! Guests aren't allowed up here. Get lost.";
    [Tooltip("Suspicion added when he catches you standing there")]
    [SerializeField] private float questionSuspicion = 30f;

    [Header("Spotting the player")]
    [Tooltip("Notice the player anywhere along his walk (inside his view cone + Question Radius), not only at the end of his route")]
    [SerializeField] private bool noticeAnywhere = false;
    [Tooltip("Width of his view in degrees (used by Notice Anywhere)")]
    [Range(10f, 360f)][SerializeField] private float viewAngle = 110f;
    [Tooltip("Being questioned = caught: he freezes, says his line, and you're BUSTED after the delay")]
    [SerializeField] private bool caughtMeansBusted = false;
    [SerializeField] private float bustDelay = 4f;

    [Header("Witnessing a takedown")]
    [Tooltip("If he sees another guard being taken down, he raises the alarm (mission fails like being caught)")]
    [SerializeField] private bool reactToTakedowns = true;
    [Tooltip("How far away he can see it happen")]
    [SerializeField] private float witnessDistance = 15f;
    [Tooltip("Width of his view for this, in degrees (wider than his normal view - a struggle catches the eye)")]
    [Range(10f, 360f)][SerializeField] private float witnessViewAngle = 150f;
    [Tooltip("This close he hears the struggle - notices it whichever way he's facing (walls still block)")]
    [SerializeField] private float hearDistance = 6f;
    [TextArea(2, 4)]
    [SerializeField] private string alarmLine = "Hey! Man down! Don't move!";
    [Tooltip("Seconds between his shout and the fail screen")]
    [SerializeField] private float alarmDelay = 2.5f;

    private static readonly List<GuardAI> All = new List<GuardAI>();

    [Header("Takedown")]
    [SerializeField] private float takedownRange = 2.2f;
    [Tooltip("Width of the 'behind him' zone in degrees")]
    [Range(30f, 180f)][SerializeField] private float behindAngle = 100f;

    public State CurrentState { get; private set; } = State.OnPost;

    private Vector3 postPosition;
    private Quaternion postRotation;
    private int targetIndex;   // index into the path: 0 = post, 1..n = route points
    private float lookTimer;
    private float postTimer;
    private float caughtTimer;
    private float questionCooldown;
    private Transform player;

    private int LastIndex => route != null ? route.Length : 0;

    private void Awake()
    {
        postPosition = transform.position;
        postRotation = transform.rotation;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        postTimer = Random.Range(pauseAtPost.x, pauseAtPost.y);
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    // Called by NoiseMaker
    public void Investigate()
    {
        if (CurrentState == State.Down || route == null || route.Length == 0) return;
        if (CurrentState == State.Investigating || CurrentState == State.Looking) return;

        // From the post, head to the first point; if he was on his way
        // back, turn around toward the point he just came from.
        targetIndex = CurrentState == State.Returning ? Mathf.Min(targetIndex + 1, LastIndex) : 1;
        CurrentState = State.Investigating;
    }

    private void Update()
    {
        if (questionCooldown > 0f) questionCooldown -= Time.deltaTime;

        if (noticeAnywhere && CurrentState != State.Down && CurrentState != State.Caught &&
            questionCooldown <= 0f && PlayerInView())
        {
            Question();
        }

        switch (CurrentState)
        {
            case State.OnPost:
                transform.rotation = Quaternion.RotateTowards(transform.rotation, postRotation, turnSpeed * Time.deltaTime);

                if (roamOnHisOwn)
                {
                    postTimer -= Time.deltaTime;
                    if (postTimer <= 0f) Investigate(); // start the next lap
                }
                break;

            case State.Investigating:
                if (MoveTowards(PathPoint(targetIndex)))
                {
                    if (targetIndex >= LastIndex) ArriveAtSpot();
                    else targetIndex++;
                }
                break;

            case State.Looking:
                if (lookTarget != null) FaceTowards(lookTarget.position);
                lookTimer -= Time.deltaTime;
                if (lookTimer <= 0f)
                {
                    targetIndex = LastIndex - 1;
                    CurrentState = State.Returning;
                }
                break;

            case State.Caught:
                if (player != null) FaceTowards(player.position);
                caughtTimer -= Time.deltaTime;
                if (caughtTimer <= 0f)
                {
                    CurrentState = State.Looking; // stays put; the mission is failing anyway
                    lookTimer = float.MaxValue;
                    if (SuspicionManager.Instance != null) SuspicionManager.Instance.ForceBust();
                }
                break;

            case State.Returning:
                if (MoveTowards(PathPoint(targetIndex)))
                {
                    if (targetIndex <= 0)
                    {
                        CurrentState = State.OnPost;
                        postTimer = Random.Range(pauseAtPost.x, pauseAtPost.y);
                    }
                    else targetIndex--;
                }
                break;
        }
    }

    private void ArriveAtSpot()
    {
        CurrentState = State.Looking;
        lookTimer = lookSeconds;

        // Caught you standing around?
        if (player != null && player.gameObject.activeInHierarchy &&
            Vector3.Distance(player.position, transform.position) <= questionRadius &&
            questionCooldown <= 0f)
        {
            Question();
        }
    }

    // He's spotted the player: either shoo them off (+suspicion), or -
    // if "Caught Means Busted" - freeze, say the line, and bust them.
    private void Question()
    {
        if (player == null) return;

        FaceTowards(player.position, instant: true);
        questionCooldown = 6f;

        if (caughtMeansBusted)
        {
            CurrentState = State.Caught;
            caughtTimer = bustDelay;
            NotePopup.Show(questionLine, bustDelay);
            return;
        }

        NotePopup.Show(questionLine, 4f);
        if (SuspicionManager.Instance != null) SuspicionManager.Instance.AddSuspicion(questionSuspicion);
        if (CurrentState == State.Looking) lookTimer = Mathf.Min(lookTimer, 2.5f); // heads back sooner
    }

    // Player within Question Radius and inside his view cone
    private bool PlayerInView()
    {
        if (player == null || !player.gameObject.activeInHierarchy) return false;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude > questionRadius) return false;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return Vector3.Angle(forward, toPlayer) <= viewAngle * 0.5f;
    }

    // True if the player is close behind him
    public bool CanBeTakenDownBy(Vector3 playerPosition)
    {
        if (CurrentState == State.Down || CurrentState == State.Caught) return false;

        Vector3 toPlayer = playerPosition - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude > takedownRange) return false;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        float angle = Vector3.Angle(forward, toPlayer);
        return angle >= 180f - behindAngle * 0.5f;
    }

    public void TakeDown()
    {
        // Anyone watching? (before he goes down - dying switches this script off)
        foreach (GuardAI other in All.ToArray())
            if (other != this) other.WitnessTakedown(this);

        CurrentState = State.Down;

        // With an NPCDeath he plays his death animation first, then disappears
        if (TryGetComponent(out NPCDeath death)) death.Die();
        else gameObject.SetActive(false); // also switches off his SuspicionSource
    }

    // Did he just see that? Close enough, inside his view, and nothing solid in between.
    private void WitnessTakedown(GuardAI victim)
    {
        if (!reactToTakedowns || CurrentState == State.Down || CurrentState == State.Caught) return;

        Vector3 eye = BodyCentre(this) + Vector3.up * 0.5f;
        Vector3 target = BodyCentre(victim);
        Vector3 toVictim = target - eye;
        Vector3 flat = new Vector3(toVictim.x, 0f, toVictim.z);
        if (flat.magnitude > witnessDistance) return;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (flat.magnitude > hearDistance && Vector3.Angle(forward, flat) > witnessViewAngle * 0.5f) return;

        // Walls, floors, doors block the view; the two guards and the player don't
        // (back faces count too - many interior walls are single-sided)
        Transform playerRoot = player != null ? player.root : null;
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        RaycastHit[] hits = Physics.RaycastAll(eye, toVictim.normalized, toVictim.magnitude, ~0, QueryTriggerInteraction.Ignore);
        Physics.queriesHitBackfaces = backfaces;
        foreach (RaycastHit hit in hits)
        {
            Transform t = hit.transform;
            if (t.IsChildOf(transform) || t.IsChildOf(victim.transform)) continue;
            if (playerRoot != null && t.root == playerRoot) continue;
            return;
        }

        // Raise the alarm: he freezes, faces the player, shouts - then it's over
        CurrentState = State.Caught;
        caughtTimer = alarmDelay;
        questionCooldown = float.MaxValue;
        NotePopup.Show(alarmLine, alarmDelay);
    }

    // Middle of his body (his pivot can be at his feet or his hips depending on the model)
    private static Vector3 BodyCentre(GuardAI guard)
    {
        bool found = false;
        Bounds b = default;
        foreach (Renderer r in guard.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!found) { b = r.bounds; found = true; } else b.Encapsulate(r.bounds);
        }
        return found ? b.center : guard.transform.position + Vector3.up;
    }

    private Vector3 PathPoint(int index)
    {
        return index <= 0 ? postPosition : route[index - 1].position;
    }

    // Walks toward a point at his own height. Returns true on arrival.
    private bool MoveTowards(Vector3 point)
    {
        Vector3 target = new Vector3(point.x, transform.position.y, point.z);
        FaceTowards(target);
        transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime);
        return (transform.position - target).sqrMagnitude < 0.01f;
    }

    private void FaceTowards(Vector3 point, bool instant = false)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = instant ? look : Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (route == null) return;
        Gizmos.color = Color.yellow;
        Vector3 prev = transform.position;
        foreach (Transform t in route)
        {
            if (t == null) continue;
            Gizmos.DrawLine(prev, t.position);
            Gizmos.DrawWireSphere(t.position, 0.3f);
            prev = t.position;
        }
    }
}