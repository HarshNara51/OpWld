using UnityEngine;

// A gizmo script can say when its VFX marker should be visible
// (e.g. clues only once the body is photographed). No interface = always.
public interface IGizmoVisibility
{
    bool ShowGizmo { get; }
}

// On the VFX marker of a gizmo (a child of the trigger). Installed by
// Tools > Gizmos > Install Gizmo VFX (this scene).
//
// Turning the gizmo off (SetActive) hides the effect with it. On top of
// that:
//  - the effect only shows while the gizmo says it can be used
//    (IGizmoVisibility - clues, the body, the hiding spot...)
//  - it fades out once the gizmo is done for good: a photo target that
//    was photographed, or a ReachZone that was reached.
public class GizmoVFX : MonoBehaviour
{
    private PhotoTarget photo;
    private IGizmoVisibility visibility;
    private bool finished;
    private bool shown = true;

    private void Start()
    {
        photo = GetComponentInParent<PhotoTarget>();
        visibility = GetComponentInParent<IGizmoVisibility>();
        var reach = GetComponentInParent<ReachZone>();
        if (reach != null) reach.onReached.AddListener(Finish);
        Refresh();
    }

    private void Update()
    {
        if (finished) return;
        if (photo != null && photo.Photographed) { Finish(); return; }
        Refresh();
    }

    private void Refresh()
    {
        bool show = visibility == null || visibility.ShowGizmo;
        if (show == shown) return;
        shown = show;
        foreach (Transform child in transform) child.gameObject.SetActive(show);
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
