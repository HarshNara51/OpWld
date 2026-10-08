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
    [SerializeField] private string deathState = "Death";

    public bool IsDying { get; private set; }

    public void Die()
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
        animator.SetTrigger(dieTrigger);
        StartCoroutine(DisappearAfterDeath(animator));
    }

    private IEnumerator DisappearAfterDeath(Animator animator)
    {
        // Wait until the Death state has played all the way through.
        // The timeout only matters if the controller has no Death state.
        float timeout = 15f;
        while (timeout > 0f)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(deathState) && !animator.IsInTransition(0) && state.normalizedTime >= 1f) break;
            timeout -= Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(disappearDelay);
        gameObject.SetActive(false);
    }
}
