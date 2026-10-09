using System.Collections;
using UnityEngine;

// Put this on the player root, next to WeaponHolster. With the knife out,
// left click stabs. Standing next to a takedown target (EliminateTarget -
// guards from behind, the mob leader) it's a takedown: the player steps up
// behind him, he freezes, and he goes down when the blade lands. Anywhere
// else it's a plain stab that hurts whatever Health is in front.
public class PlayerKnife : MonoBehaviour
{
    // Other scripts (camera, cars...) can check this
    public static bool IsStabbing { get; private set; }

    [SerializeField] private KeyCode stabKey = KeyCode.Mouse0;
    [SerializeField] private string stabTrigger = "Stab";

    [Header("Timing (Stabbing clip, 2.13 s)")]
    [Tooltip("When the blade is fully out in the clip")]
    [SerializeField] private float hitTime = 0.45f;
    [Tooltip("When the player can move again (the clip is pulling back by then)")]
    [SerializeField] private float recoverTime = 1.5f;

    [Header("Takedown")]
    [Tooltip("How far behind the target the player stands for the stab (root to root)")]
    [SerializeField] private float takedownDistance = 0.7f;
    [Tooltip("Time to step into position before the stab")]
    [SerializeField] private float stepInTime = 0.15f;

    [Header("Plain stab")]
    [SerializeField] private float damage = 50f;
    [Tooltip("How far in front of the player the blade reaches")]
    [SerializeField] private float reach = 1.0f;
    [SerializeField] private float hitRadius = 0.4f;
    [SerializeField] private float hitHeight = 1.4f;

    private WeaponHolster holster;
    private PlayerLocomotion locomotion;
    private CharacterController controller;
    private Animator animator;
    private Health ownHealth;

    private void Awake()
    {
        IsStabbing = false; // static - reset for every scene
        holster = GetComponent<WeaponHolster>();
        locomotion = GetComponent<PlayerLocomotion>();
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        ownHealth = GetComponent<Health>();
    }

    private void OnDisable()
    {
        // e.g. the player died mid-stab
        StopAllCoroutines();
        IsStabbing = false;
        if (locomotion != null) locomotion.MovementLocked = false;
    }

    private void Update()
    {
        if (IsStabbing || holster == null || !holster.IsKnifeEquipped) return;
        if (CameraMode.IsActive || Time.timeScale == 0f) return; // photo mode / briefing / pause
        if (!Input.GetKeyDown(stabKey)) return;

        EliminateTarget target = EliminateTarget.Nearest(transform.position);
        if (target != null)
        {
            if (!target.CanBeTakenDownBy(transform.position))
            {
                NotePopup.Show("He'll see you coming. Get behind him first.", 3f);
                return;
            }
            StartCoroutine(Takedown(target));
        }
        else
        {
            StartCoroutine(PlainStab());
        }
    }

    private IEnumerator Takedown(EliminateTarget target)
    {
        BeginStab();
        target.BeginTakedown();

        // Step up behind him, facing the same way he does
        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = toTarget.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toTarget) : startRotation;
        Vector3 standAt = target.transform.position - endRotation * Vector3.forward * takedownDistance;

        if (animator != null) animator.SetTrigger(stabTrigger);

        float t = 0f;
        while (t < stepInTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / stepInTime);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, k);
            Vector3 offset = standAt - transform.position;
            offset.y = 0f;
            controller.Move(offset * k); // the controller keeps us out of walls
            yield return null;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, hitTime - stepInTime));
        target.CompleteTakedown();

        yield return new WaitForSeconds(recoverTime - hitTime);
        EndStab();
    }

    private IEnumerator PlainStab()
    {
        BeginStab();
        if (animator != null) animator.SetTrigger(stabTrigger);

        yield return new WaitForSeconds(hitTime);

        Vector3 point = transform.position + Vector3.up * hitHeight + transform.forward * reach;
        foreach (Collider c in Physics.OverlapSphere(point, hitRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            Health health = c.GetComponentInParent<Health>();
            if (health == null || health == ownHealth) continue;
            health.TakeDamage(damage);
            break; // one victim per stab
        }

        yield return new WaitForSeconds(recoverTime - hitTime);
        EndStab();
    }

    private void BeginStab()
    {
        IsStabbing = true;
        if (locomotion != null) locomotion.MovementLocked = true;
    }

    private void EndStab()
    {
        IsStabbing = false;
        if (locomotion != null) locomotion.MovementLocked = false;
    }
}
