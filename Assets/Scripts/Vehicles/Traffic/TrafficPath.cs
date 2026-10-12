using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

// A traffic route (one lane spline) baked into evenly spaced points, so
// cars can look up "where is the road X metres along" cheaply every frame.
// Baked once per route, the first time a car uses it.
public class TrafficPath
{
    private const float Step = 1f; // metres between baked points

    private static readonly Dictionary<SplineContainer, TrafficPath> Cache = new Dictionary<SplineContainer, TrafficPath>();

    public readonly SplineContainer Spline;
    public readonly float Length;
    private readonly Vector3[] points;

    public Vector3 Start => points[0];
    public Vector3 StartDirection => DirectionAt(0f);

    public static TrafficPath For(SplineContainer spline)
    {
        if (spline == null) return null;
        if (!Cache.TryGetValue(spline, out TrafficPath path))
        {
            path = new TrafficPath(spline);
            Cache[spline] = path;
        }
        return path;
    }

    // Routes are rebuilt in the editor - forget the old bakes when play starts
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearCache() => Cache.Clear();

    private TrafficPath(SplineContainer spline)
    {
        Spline = spline;

        // Sample densely by spline 't', then re-space by real distance
        float rough = Mathf.Max(1f, spline.CalculateLength());
        int dense = Mathf.CeilToInt(rough * 4f);
        var raw = new Vector3[dense + 1];
        var dist = new float[dense + 1];
        for (int i = 0; i <= dense; i++)
        {
            raw[i] = spline.EvaluatePosition((float)i / dense);
            if (i > 0) dist[i] = dist[i - 1] + Vector3.Distance(raw[i - 1], raw[i]);
        }
        Length = dist[dense];

        int count = Mathf.Max(2, Mathf.CeilToInt(Length / Step) + 1);
        points = new Vector3[count];
        int j = 0;
        for (int i = 0; i < count; i++)
        {
            float d = Mathf.Min(i * Step, Length);
            while (j < dense - 1 && dist[j + 1] < d) j++;
            float span = dist[j + 1] - dist[j];
            points[i] = Vector3.Lerp(raw[j], raw[j + 1], span > 0f ? (d - dist[j]) / span : 0f);
        }
    }

    // Point this many metres along the route. Past either end it carries
    // on in a straight line (so a car can overhang the ends).
    public Vector3 PointAt(float d)
    {
        float clamped = Mathf.Clamp(d, 0f, Length);
        float f = clamped / Step;
        int i = Mathf.Min((int)f, points.Length - 2);
        Vector3 p = Vector3.Lerp(points[i], points[i + 1], f - i);
        if (d != clamped) p += DirectionAt(clamped) * (d - clamped);
        return p;
    }

    // Flat (no up/down) travel direction here
    public Vector3 DirectionAt(float d)
    {
        float clamped = Mathf.Clamp(d, 0f, Length);
        int i = Mathf.Min((int)(clamped / Step), points.Length - 2);
        Vector3 dir = points[i + 1] - points[i];
        dir.y = 0f;
        return dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
    }

    // How sharply the route bends here: 1 / turning radius, positive = right
    public float CurvatureAt(float d)
    {
        const float window = 2f;
        float yaw = Vector3.SignedAngle(DirectionAt(d - window), DirectionAt(d + window), Vector3.up);
        return yaw * Mathf.Deg2Rad / (2f * window);
    }
}
