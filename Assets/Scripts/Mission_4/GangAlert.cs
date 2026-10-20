using UnityEngine;

// Gas Station: the gang notices the player coming, from ANY side - not only when shot at.
// Walking within Foot Radius of the group, or driving within Vehicle Radius (engines are loud),
// starts the fight exactly like the first gunshot does (EnemyCover.CombatActive).
// The Hide Here cover sits just outside Foot Radius, so the intended plan - park on the road,
// sneak to the trash cans, open fire first - still works.
public class GangAlert : MonoBehaviour
{
    [Tooltip("Centre of the gang's deal formation (leave empty = this object)")]
    [SerializeField] private Transform center;
    [SerializeField] private float footRadius = 25f;
    [SerializeField] private float vehicleRadius = 40f;
    [TextArea(1, 3)]
    [SerializeField] private string spottedNote = "They've spotted you!";

    private Transform player;

    private void Update()
    {
        if (EnemyCover.CombatActive) return;
        if (Mission4Manager.Instance == null || Mission4Manager.Instance.CurrentState != Mission4Manager.MissionState.InProgress) return;

        Vector3 c = (center != null ? center : transform).position;
        VehicleInteraction vehicle = VehicleInteraction.Current;
        bool driving = vehicle != null && vehicle.IsDriving;

        Vector3 pos;
        if (driving) pos = vehicle.transform.position;
        else
        {
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) return;
                player = p.transform;
            }
            pos = player.position;
        }

        float radius = driving ? vehicleRadius : footRadius;
        Vector3 flat = pos - c; flat.y = 0f;
        if (flat.sqrMagnitude > radius * radius) return;

        EnemyCover.CombatActive = true;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUI(AudioManager.Instance.alertWhoosh);
        if (!string.IsNullOrEmpty(spottedNote)) NotePopup.Show(spottedNote, 2.5f);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 c = (center != null ? center : transform).position;
        Gizmos.color = new Color(1f, 0.5f, 0.1f);
        Gizmos.DrawWireSphere(c, footRadius);
        Gizmos.color = new Color(1f, 0.2f, 0.1f);
        Gizmos.DrawWireSphere(c, vehicleRadius);
    }
}
