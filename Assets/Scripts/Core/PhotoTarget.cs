using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Anything the player can photograph in Camera Mode. Put it on any
// object - a clue, the body, a landmark, a dinosaur - and type a Note.
// When a photo frames it, the note pops up on screen (once).
//
// Mission-specific clues (ClueDiscovery, BodyDiscoveryZone, ...)
// build on this and add their own rules for when a shot counts.
public class PhotoTarget : MonoBehaviour
{
    public static readonly List<PhotoTarget> All = new List<PhotoTarget>();

    [Header("Photo")]
    [Tooltip("Shown on screen the moment this is photographed")]
    [TextArea(2, 5)] public string note;

    [Tooltip("How far away the camera can be and still count the shot")]
    public float maxDistance = 30f;

    [Tooltip("What the camera must aim at. Empty = the center of this object's model")]
    public Transform focusPoint;

    [Tooltip("Printed when the player walks into this object's trigger (if it has one). Leave empty for none.")]
    public string proximityHint = "Something here is worth a photo... (press {key} for camera)";

    [Tooltip("Optional extra things to happen when photographed")]
    public UnityEvent onPhotographed;

    public bool Photographed { get; private set; }

    protected virtual void OnEnable() => All.Add(this);
    protected virtual void OnDisable() => All.Remove(this);

    // The exact point the camera checks against
    public Vector3 FocusPosition
    {
        get
        {
            if (focusPoint != null) return focusPoint.position;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return transform.position;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.center;
        }
    }

    // Override: can this shot count right now? (e.g. only during a mission phase)
    protected virtual bool CanBePhotographed() => true;

    // Override: what happens when it counts
    protected virtual void HandlePhotographed() { }

    // Called by CameraMode. Returns true if the shot counted.
    public bool TryPhotograph()
    {
        if (Photographed || !CanBePhotographed()) return false;

        Photographed = true;
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
        string key = CameraMode.Instance != null ? CameraMode.Instance.ToggleKey.ToString() : "C";
        Debug.Log(proximityHint.Replace("{key}", key).Replace("press C ", $"press {key} "));
    }
}