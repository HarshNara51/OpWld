using UnityEngine;

// On the Hospital gizmo (Assets/Prefabs/Hospital.prefab, at the hospital's north entrance in
// every scene). Walk in on foot while hurt: the screen fades to black and you come back with
// full health. It is also where the player wakes up after dying in free roam
// (PlayerHealthSystem) - a few metres in front of the gizmo, facing away from the building.
public class HospitalHeal : MonoBehaviour
{
    public static HospitalHeal Instance { get; private set; }

    [SerializeField] private float radius = 2.6f;
    [Tooltip("How far in front of the gizmo (its blue Z axis) the player wakes up - outside the radius")]
    [SerializeField] private float respawnDistance = 4f;

    private bool inside, working;

    public Vector3 RespawnPosition => transform.position + transform.forward * respawnDistance + Vector3.up * 0.1f;
    public Quaternion RespawnRotation => Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.up);

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        if (working) return;
        Health h = PlayerHealthSystem.PlayerHealth;
        Transform p = PlayerHealthSystem.PlayerTransform;
        if (h == null || p == null || h.IsDead || !p.gameObject.activeInHierarchy) { inside = false; return; }

        Vector3 d = p.position - transform.position; d.y = 0f;
        bool now = d.magnitude < radius;
        if (now && !inside) Enter(h);
        inside = now;
    }

    private void Enter(Health h)
    {
        AudioManager am = AudioManager.Instance;
        if (h.Current >= h.Max - 0.5f)
        {
            NotePopup.Show("<b>Nurse:</b> \"You look perfectly healthy to me!\"", 3f);
            return;
        }

        working = true;
        if (am != null) am.PlayUI(am.gizmoInteract);
        if (ScreenFader.Instance == null) { Healed(h); return; }
        ScreenFader.Instance.FadeOutIn(() => h.Heal(h.Max), () => Healed(h), 0.6f, 1.2f);
    }

    private void Healed(Health h)
    {
        h.Heal(h.Max);
        working = false;
        NotePopup.Show("<b>Nurse:</b> \"All patched up - you're good as new.\"", 4f);
    }
}
