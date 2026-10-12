using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// The floating hologram name tag of a gizmo ("RETURN HOME", "CARGO 1"...).
// Put on the gizmo itself - Tools > Gizmos > Install Gizmo VFX adds one to
// every gizmo with a title and colour that match its marker. Edit the text
// here; the installer never overwrites a title you've changed.
//
// Drawn by GizmoLabelHUD (made automatically). The label follows the same
// rules as the VFX marker: hidden while the gizmo can't be used, gone once
// it's done (photographed, reached, picked up...).
public class GizmoLabel : MonoBehaviour
{
    public static readonly List<GizmoLabel> All = new List<GizmoLabel>();

    [Tooltip("The big text")]
    public string title = "";
    [Tooltip("Small text under it (the distance is added after it)")]
    public string subtitle = "";
    [Tooltip("Text colour (matches the marker's VFX)")]
    public Color color = new Color(0.45f, 0.8f, 1f);

    [Tooltip("Height of the label above the marker (m)")]
    public float height = 2.5f;
    [Tooltip("The label shows from this far away (m)")]
    public float showDistance = 40f;
    [Tooltip("Closer than this, it lights up (you can use it)")]
    public float useDistance = 2.5f;

    private IGizmoVisibility visibility;
    private PhotoTarget photo;
    private GizmoVFX marker;
    private bool hadMarker;

    public Vector3 Anchor => (marker != null ? marker.transform.position : transform.position) + Vector3.up * height;

    // Should it be drawn at all right now?
    public bool Visible
    {
        get
        {
            if (!isActiveAndEnabled || string.IsNullOrEmpty(title)) return false;
            if (hadMarker && marker == null) return false;          // marker finished (reached...)
            if (photo != null && photo.Photographed) return false;  // clue / body done
            return visibility == null || visibility.ShowGizmo;
        }
    }

    private void Awake()
    {
        visibility = GetComponent<IGizmoVisibility>();
        photo = GetComponent<PhotoTarget>();
        marker = GetComponentInChildren<GizmoVFX>(true);
        hadMarker = marker != null;
    }

    private void OnEnable()
    {
        All.Add(this);
        GizmoLabelHUD.Ensure();
    }

    private void OnDisable() => All.Remove(this);

    // "Mission_GasStation" -> "Gas Station"
    public static string MissionName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return "";
        string n = sceneName.Replace("Mission_", "").Replace("_", " ");
        return Nicify(n);
    }

    // "HotelEntrance" -> "Hotel Entrance", "Cargo1" -> "Cargo 1"
    public static string Nicify(string n)
    {
        n = Regex.Replace(n, @"\s*\(\d+\)$", "");               // "Thing (1)"
        n = Regex.Replace(n, @"(?<=[a-z])(?=[A-Z0-9])", " ");    // camelCase / digits
        n = Regex.Replace(n, @"(?<=[A-Z])(?=[A-Z][a-z])", " ");
        return n.Replace("_", " ").Trim();
    }
}
