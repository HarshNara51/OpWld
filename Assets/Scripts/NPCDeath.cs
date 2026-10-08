using System.Collections;
using UnityEngine;

// Put this on the ROOT of a guard / mob leader (next to GuardAI). The model
// child's Animator controller needs a "Die" trigger leading to a "Death"
// state (NPC_Locomotion has one). Which death clip plays is decided by
// the controller - use an Animator Override Controller to give a
// character a different one.
//
// Die(): the character stops walking, noticing and colliding, plays the
// whole death animation, stays down for a few seconds, then disappears.
public class NPCDeath : MonoBehaviour
{
    [Tooltip("Seconds the body stays on the floor after the death animation finishes")]
    [SerializeField] private float disappearDelay = 5f;

    [SerializeField] private string dieTrigger = "Die";
    [Tooltip("Death states are found by this name, or by this Tag when a controller has several death states")]
    [SerializeField] private string deathState = "Death";

    [Header("Several death clips (optional)")]
    [Tooltip("Only used if the controller has an Int parameter with this name (e.g. Enemy_Rifle)")]
    [SerializeField] private string deathTypeParameter = "DeathType";
    [Tooltip("Which death this character plays normally: 0 = Death From The Front, 1 = Dying Backwards")]
    [SerializeField] private int deathType = 0;
    [Tooltip("Played instead when the killing hit was a headshot")]
    [SerializeField] private int headshotDeathType = 2;

    [Header("Floor fix")]
    [Tooltip("Some death clips end with the body partly sunk into the floor on tall characters. The model is raised gradually by this much (m) over the fall")]
    [SerializeField] private float endLift = 0f;
    [SerializeField] private float headshotEndLift = 0.15f;

    public bool IsDying { get; private set; }
    private float lift;

    public void Die() => Die(false);

    public void Die(bool headshot)
    {
        if (IsDying) return;
        IsDying = true;

        // Stop everything that makes him act like he's alive
        if (TryGetComponent(out GuardAI guard)) guard.enabled = false;
        if (TryGetComponent(out SuspicionSource source)) source.enabled = false;
        if (TryGetComponent(out EliminateTarget target)) target.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (animator.TryGetComponent(out NPCAnimatorSpeed speed)) speed.enabled = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // finish the fall even if off-screen
        lift = endLift;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == deathTypeParameter && p.type == AnimatorControllerParameterType.Int)
            {
                animator.SetInteger(deathTypeParameter, headshot ? headshotDeathType : deathType);
                if (headshot) lift = headshotEndLift;
            }
        }
        animator.SetTrigger(dieTrigger);
        StartCoroutine(DisappearAfterDeath(animator));
    }

    private IEnumerator DisappearAfterDeath(Animator animator)
    {
        // Wait until the Death state has played all the way through.
        // The timeout only matters if the controller has no Death state.
        float timeout = 15f;
        Transform model = animator.transform;
        Vector3 startLocal = model.localPosition;
        while (timeout > 0f)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool inDeath = state.IsName(deathState) || state.IsTag(deathState);
            if (inDeath && lift != 0f)
                model.localPosition = startLocal + Vector3.up * lift * Mathf.SmoothStep(0f, 1f, state.normalizedTime);
            if (inDeath && !animator.IsInTransition(0) && state.normalizedTime >= 1f) break;
            timeout -= Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(disappearDelay);
        gameObject.SetActive(false);
    }
}
