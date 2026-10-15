using UnityEngine;

// Sits on the aimable uplight prefab (Tools > Lighting > Build Aimable
// Uplight). Drag a dinosaur (or anything) into Target and the lamp turns
// on its base and tilts its head to light it - in the editor too, so you
// see it while placing. No target: aim it by hand with Turn and Tilt.
[ExecuteAlways]
public class UplightAim : MonoBehaviour
{
    [Tooltip("What to light up. The lamp turns and tilts to point at it. Empty = use Turn / Tilt below.")]
    public Transform target;
    [Tooltip("Where on the target to aim: 0 = its feet, 1 = the top of its model")]
    [Range(0f, 1f)] public float aimHeight = 0.55f;

    [Header("Manual aim (when there's no target)")]
    [Range(-180f, 180f)] public float turn = 0f;
    [Range(-20f, 85f)] public float tilt = 35f;

    [Header("Parts (set by the builder)")]
    [Tooltip("Turns left/right: the yoke and head")]
    [SerializeField] private Transform swivel;
    [Tooltip("Tilts up/down on the axle. Its +X is the way the lamp points.")]
    [SerializeField] private Transform head;

    public Transform Head => head;

    private void OnValidate() => Aim();
    private void LateUpdate() => Aim();

    public void Aim()
    {
        if (swivel == null || head == null) return;

        float yaw = turn, pitch = tilt;
        if (target != null)
        {
            Vector3 point = AimPoint();
            // In this lamp's own space, so it works however the lamp itself is placed/rotated
            Vector3 local = transform.InverseTransformPoint(point) - transform.InverseTransformPoint(head.position);
            float flat = new Vector2(local.x, local.z).magnitude;
            yaw = Mathf.Atan2(-local.z, local.x) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(Mathf.Atan2(local.y, flat) * Mathf.Rad2Deg, -20f, 85f);
            // Remember the angles: a copy pasted into another scene loses its Target
            // (Unity drops links between scenes) but keeps pointing the same way
            turn = yaw;
            tilt = pitch;
        }

        // Only when it actually changed, so an untouched scene isn't marked as edited every frame
        Quaternion swivelRot = Quaternion.Euler(0f, yaw, 0f), headRot = Quaternion.Euler(0f, 0f, pitch);
        if (Quaternion.Angle(swivel.localRotation, swivelRot) > 0.01f) swivel.localRotation = swivelRot;
        if (Quaternion.Angle(head.localRotation, headRot) > 0.01f) head.localRotation = headRot;
    }

    // A point on the target's model (not its pivot, which is usually at its feet)
    private Vector3 AimPoint()
    {
        bool found = false;
        Bounds b = default;
        foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!found) { b = r.bounds; found = true; } else b.Encapsulate(r.bounds);
        }
        if (!found) return target.position;
        return new Vector3(b.center.x, Mathf.Lerp(b.min.y, b.max.y, aimHeight), b.center.z);
    }
}
