using System.Collections.Generic;
using UnityEngine;

// Put this on the mob leader AND on the guards. With the knife out
// (key 2), left click up close for a stab takedown - PlayerKnife does
// the stab, this decides who can be taken down. Guards (anything with
// a GuardAI) can only be taken down from behind. Check "Is Mob Leader"
// only on the actual target - guards just need to go down as obstacles.
public class EliminateTarget : MonoBehaviour
{
    [Tooltip("Check this only on the actual mob leader - leave unchecked on guards")]
    [SerializeField] private bool isMobLeader = true;

    [Tooltip("Suspicion added by the takedown itself (a struggle makes some noise)")]
    [SerializeField] private float takedownSuspicion = 15f;

    // Targets the player is standing next to right now (PlayerKnife picks one)
    private static readonly List<EliminateTarget> inRange = new List<EliminateTarget>();

    private GuardAI guard;
    private bool takenDown;

    private void Awake()
    {
        guard = GetComponent<GuardAI>();
    }

    private void OnDisable()
    {
        inRange.Remove(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || takenDown) return;
        if (!inRange.Contains(this)) inRange.Add(this);

        WeaponHolster holster = other.GetComponent<WeaponHolster>();
        if (holster == null || !holster.IsKnifeEquipped)
            NotePopup.Show($"Press {GameKeys.Label(GameAction.EquipKnife)} to draw your knife.", 2.5f);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        inRange.Remove(this);
    }

    // The nearest target the player is standing next to, or null
    public static EliminateTarget Nearest(Vector3 playerPosition)
    {
        EliminateTarget best = null;
        float bestDistance = float.MaxValue;
        foreach (EliminateTarget t in inRange)
        {
            if (t == null || t.takenDown) continue;
            float d = (t.transform.position - playerPosition).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = t; }
        }
        return best;
    }

    // Guards have to be approached from behind
    public bool CanBeTakenDownBy(Vector3 playerPosition)
    {
        if (takenDown) return false;
        return guard == null || guard.CanBeTakenDownBy(playerPosition);
    }

    // The stab has started: he stops where he is so the knife lands
    public void BeginTakedown()
    {
        takenDown = true;
        inRange.Remove(this);
        if (guard != null) guard.enabled = false;
    }

    // The knife has hit
    public void CompleteTakedown()
    {
        if (SuspicionManager.Instance != null) SuspicionManager.Instance.AddSuspicion(takedownSuspicion);

        if (isMobLeader) Mission2Manager.Instance.OnTargetEliminated();

        if (guard != null) guard.TakeDown();
        else if (TryGetComponent(out NPCDeath death)) death.Die();
        else gameObject.SetActive(false);
    }
}
