using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// The player's own waypoint, set on the big map (M): an icon (a gizmo or a
// place) or any spot (right click). Shown as a pink pin on the minimap and
// followed by the on-screen marker. Cleared when you get there, when its
// gizmo goes away, or on a scene change.
//
// With no waypoint set, the marker follows the nearest mission objective
// instead (see Minimap).
public static class Waypoint
{
    private const float ReachedDistance = 12f;

    private static GizmoLabel gizmo;
    private static Minimap.Place place;
    private static Vector3 point;
    private static bool active;

    public static event Action Changed;

    public static bool Active
    {
        get
        {
            if (active && gizmo == null && place == null && float.IsNaN(point.x)) active = false;
            if (active && gizmo != null && !gizmo.Visible) Clear();       // done / gone
            return active;
        }
    }

    public static GizmoLabel Gizmo => Active ? gizmo : null;
    public static Minimap.Place Place => Active ? place : null;

    public static Vector3 Position
    {
        get
        {
            if (gizmo != null) return gizmo.Anchor;
            if (place != null) return new Vector3(place.position.x, GroundY(place.position) + 2f, place.position.y);
            return point;
        }
    }

    public static string Name
    {
        get
        {
            if (gizmo != null) return gizmo.title;
            if (place != null) return place.name;
            return "Waypoint";
        }
    }

    public static bool Is(GizmoLabel l) => Active && gizmo != null && gizmo == l;
    public static bool Is(Minimap.Place p) => Active && place != null && place == p;

    public static void Set(GizmoLabel label)
    {
        if (label == null) return;
        Reset();
        gizmo = label;
        active = true;
        Changed?.Invoke();
    }

    public static void Set(Minimap.Place p)
    {
        if (p == null) return;
        Reset();
        place = p;
        active = true;
        Changed?.Invoke();
    }

    // Any spot on the map (world X/Z)
    public static void Set(Vector2 xz)
    {
        Reset();
        point = new Vector3(xz.x, GroundY(xz) + 2f, xz.y);
        active = true;
        Changed?.Invoke();
    }

    public static void Clear()
    {
        if (!active) return;
        Reset();
        Changed?.Invoke();
    }

    // Called every frame by the Minimap with where you are
    public static void CheckReached(Vector3 me)
    {
        if (!Active) return;
        Vector3 d = Position - me;
        d.y = 0f;
        if (d.magnitude <= ReachedDistance) Clear();
    }

    private static void Reset()
    {
        gizmo = null;
        place = null;
        point = new Vector3(float.NaN, 0f, 0f);
        active = false;
    }

    private static float GroundY(Vector2 xz)
    {
        Terrain t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + t.transform.position.y : 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        Reset();
        Changed = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        Reset();
        Changed?.Invoke();
    }
}
