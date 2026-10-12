using UnityEngine;

// On the VFX marker of a gizmo (a child of the trigger). Installed by
// Tools > Gizmos > Install Gizmo VFX (this scene).
//
// Turning the gizmo off (SetActive) hides the effect with it. On top of
// that, the effect fades out once the gizmo is done for good: a photo
// target (clue, body...) that was photographed, or a ReachZone that was
// reached.
public class GizmoVFX : MonoBehaviour
{
    private PhotoTarget photo;
    private bool finished;

    private void Start()
    {
        photo = GetComponentInParent<PhotoTarget>();
        var reach = GetComponentInParent<ReachZone>();
        if (reach != null) reach.onReached.AddListener(Finish);
    }

    private void Update()
    {
        if (!finished && photo != null && photo.Photographed) Finish();
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        Destroy(gameObject, 3f);
    }
}
