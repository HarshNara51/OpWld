using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Anything the player can photograph in Camera Mode. Put it on any
// object - a clue, the body, a landmark, a dinosaur - and type a Note.
// When a photo frames it, the note pops up on screen (once).
//
// Mission-specific clues (ClueDiscovery, BodyDiscoveryZone, ...)
// build on this and add their own rules for when a shot counts.
public class PhotoTarget : MonoBehaviour, IGizmoVisibility
{
    // Its VFX marker shows only while this can actually be photographed
    // (e.g. clues only after the body, the trucks only while following)
    public bool ShowGizmo => !Photographed && CanBePhotographed();

    public static readonly List<PhotoTarget> All = new List<PhotoTarget>();

    [Header("Photo")]
    [Tooltip("Shown on screen the moment this is photographed")]
    [TextArea(2, 5)] public string note;

    [Tooltip("How far away the camera can be and still count the shot")]
    public float maxDistance = 30f;

    [Tooltip("What the camera must aim at (the middle of its model). Empty = the center of this object's model")]
    public Transform focusPoint;

    [Tooltip("Other objects that count as part of the subject, so they never 'block' the shot (e.g. the second truck of a convoy)")]
    public Transform[] alsoPartOfSubject = new Transform[0];

    [Tooltip("Shown as a prompt while the player is inside this object's trigger (if it has one). {key} = the camera key. Leave empty for none.")]
    public string proximityHint = "Something here is worth a photo... (press {key} for camera)";

    [Tooltip("Optional extra things to happen when photographed")]
    public UnityEvent onPhotographed;

    public bool Photographed { get; private set; }

    protected virtual void OnEnable() => All.Add(this);
    protected virtual void OnDisable()
    {
        All.Remove(this);
        NotePopup.ClearPrompt(this);
    }

    // The exact point the camera checks against
    // (the middle of the model, not its pivot - a vehicle's pivot can sit at or
    // even under the road, where every line of sight hits the ground first)
    public Vector3 FocusPosition
    {
        get
        {
            Transform model = focusPoint != null ? focusPoint : transform;

            // Meshes only - not the gizmo's VFX marker or other particles
            bool found = false;
            Bounds bounds = default;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found ? bounds.center : model.position;
        }
    }

    // True if this collider belongs to the subject (so it can't block the shot)
    public bool IsPartOfSubject(Transform t)
    {
        if (t.IsChildOf(transform)) return true;
        if (focusPoint != null && t.IsChildOf(focusPoint)) return true;
        foreach (Transform extra in alsoPartOfSubject)
            if (extra != null && t.IsChildOf(extra)) return true;
        return false;
    }

    // Override: can this shot count right now? (e.g. only during a mission phase)
    protected virtual bool CanBePhotographed() => true;

    // Override: what happens when it counts
    protected virtual void HandlePhotographed() { }

    // Override: why the shot can't count YET, told to the player (e.g. "park
    // your car first"). Null = nothing in the way.
    protected virtual string NotYetReason() => null;

    // Called by CameraMode. Returns true if the shot counted.
    public bool TryPhotograph()
    {
        if (Photographed || !CanBePhotographed()) return false;
        string notYet = NotYetReason();
        if (notYet != null) { NotePopup.Show(notYet, 4f); return false; }

        Photographed = true;
        NotePopup.ClearPrompt(this);
        NotePopup.Show(note);   // the clue's own note first...
        HandlePhotographed();   // ...then any mission messages queue after it
        onPhotographed?.Invoke();
        return true;
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (Photographed || string.IsNullOrEmpty(proximityHint)) return;
        if (!other.CompareTag("Player") && !other.CompareTag("PlayerCar")) return;
        if (!CanBePhotographed()) return;

        // {key} (or an old "press C") always shows the real camera key
        string notYet = NotYetReason();
        if (notYet != null) { NotePopup.Prompt(this, notYet); return; }
        string key = NotePopup.Key(GameAction.PhotoMode);
        NotePopup.Prompt(this, proximityHint.Replace("{key}", key).Replace("press C ", $"press {key} "));
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") && !other.CompareTag("PlayerCar")) return;
        NotePopup.ClearPrompt(this);
    }
}