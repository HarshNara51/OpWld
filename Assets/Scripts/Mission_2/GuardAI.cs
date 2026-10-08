using UnityEngine;

// Put this on each guard. The guard stands at his post (wherever he is
// placed in the scene) until a NoiseMaker calls Investigate(). He then
// walks his Route (waypoints, in order) to the last point, looks around
// for a while, and walks back the same way to his post.
//
// The walk back is the takedown window: EliminateTarget only allows a
// takedown from behind. If the player is standing near the investigate
// spot when he arrives, he questions them (on-screen line + suspicion).
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

    // Called by NoiseMaker
    public void Investigate()
    {
        if (CurrentState == State.Down || route == null || route.Length == 0) return;
        if (CurrentState == State.Investigating || CurrentState == State.Looking) return;

        // From the post, head to the first point; if he was on his way
        // back, turn around toward the point he just came from.
        targetIndex = CurrentState == State.Returning ? Mathf.Min(targetIndex + 1, LastIndex) : 1;
        CurrentState = State.Investigating;
        if (!roamOnHisOwn) Debug.Log($"{name}: heard something, going to check.");
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
                        if (!roamOnHisOwn) Debug.Log($"{name}: back on post.");
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
            Debug.Log($"{name}: caught the player!");
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
        CurrentState = State.Down;

        // With an NPCDeath he plays his death animation first, then disappears
        if (TryGetComponent(out NPCDeath death)) death.Die();
        else gameObject.SetActive(false); // also switches off his SuspicionSource
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