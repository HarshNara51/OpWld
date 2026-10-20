using System.Collections.Generic;
using UnityEngine;

// An indoor area (hotel lobby, mob room...). While the player is inside the box,
// AudioManager swaps the outdoor ambience for the indoor one, the wind goes
// quiet and footsteps use the indoor (hard floor) sound.
// Size it with the yellow box in the Scene view.
public class AmbienceZone : MonoBehaviour
{
    private static readonly List<AmbienceZone> all = new List<AmbienceZone>();

    [Tooltip("Size of the indoor area (metres), centred on this object and turned with it")]
    public Vector3 size = new Vector3(10f, 4f, 10f);

    [Tooltip("Play the indoor ambience (people talking) here. Off = just quiet indoors (wind off, hard-floor footsteps)")]
    public bool chatter = true;

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    public bool Inside(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        Vector3 half = new Vector3(size.x / Mathf.Max(0.0001f, transform.lossyScale.x),
                                   size.y / Mathf.Max(0.0001f, transform.lossyScale.y),
                                   size.z / Mathf.Max(0.0001f, transform.lossyScale.z)) * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    public static bool Contains(Vector3 point) => Contains(point, out _);

    // Inside any zone? chatter = one of the zones it's in wants the indoor ambience
    public static bool Contains(Vector3 point, out bool chatter)
    {
        bool inside = false; chatter = false;
        foreach (AmbienceZone z in all)
            if (z != null && z.Inside(point)) { inside = true; chatter |= z.chatter; }
        return inside;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
