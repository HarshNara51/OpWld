using UnityEngine;

// Put this on the character MODEL (the object with the Animator) of any
// NPC that's moved by a script - GuardAI, splines, NavMesh, anything.
// It measures how fast the character is actually moving and sends that
// to the Animator's "Speed" float, so an Idle/Walk blend tree switches
// on its own: standing = breathing idle, moving = walking (looped).
[RequireComponent(typeof(Animator))]
public class NPCAnimatorSpeed : MonoBehaviour
{
    [SerializeField] private string speedParameter = "Speed";

    [Tooltip("Higher = reacts faster to starting/stopping, lower = smoother")]
    [SerializeField] private float smoothing = 8f;

    private Animator animator;
    private Vector3 lastPosition;
    private float smoothedSpeed;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false; // the script moves us, not the animation
        lastPosition = transform.position;
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        smoothedSpeed = 0f;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f) return; // paused

        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;

        float speed = delta.magnitude / Time.deltaTime;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, smoothing * Time.deltaTime);

        animator.SetFloat(speedParameter, smoothedSpeed);
    }
}
