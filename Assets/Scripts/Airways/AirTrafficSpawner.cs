using System.Collections;
using UnityEngine;

// Put this on an empty GameObject at the CENTER of the map, at ground
// level. Every so often it launches ONE random aircraft from a random
// point on the big circle around it; the aircraft crosses the sky to
// roughly the opposite side and disappears there.
//
// Make the radius big (at or beyond the camera's far clip distance) so
// aircraft appear and vanish far away, like the train in its tunnels.
// Make this a prefab and drop it only into the scenes that should have
// air traffic (e.g. not the stormy/snowy ones).
public class AirTrafficSpawner : MonoBehaviour
{
    [Tooltip("Aircraft prefabs - each needs an AircraftFlight on its root")]
    [SerializeField] private AircraftFlight[] aircraftPrefabs;

    [Tooltip("Spawn/despawn circle radius around this object")]
    [SerializeField] private float radius = 1200f;

    [Header("Auto placement")]
    [Tooltip("On Play, moves the center to the middle of all terrains in the scene (height stays this object's Y)")]
    [SerializeField] private bool centerOnTerrain = true;

    [Tooltip("On Play, sets the radius to cover every terrain corner, times this margin. 0 = keep the radius above.")]
    [SerializeField] private float autoRadiusMargin = 1.3f;

    [Tooltip("Seconds before the first aircraft appears")]
    [SerializeField] private float firstFlightDelay = 5f;

    [Tooltip("Random wait between flights (min, max seconds)")]
    [SerializeField] private Vector2 waitBetweenFlights = new Vector2(20f, 60f);

    [Tooltip("How far (degrees) the exit point can be from straight across")]
    [Range(0f, 90f)]
    [SerializeField] private float exitSpread = 50f;

    private IEnumerator Start()
    {
        if (aircraftPrefabs == null || aircraftPrefabs.Length == 0)
        {
            Debug.LogWarning($"{name}: no aircraft prefabs assigned.");
            yield break;
        }

        if (centerOnTerrain) FitToTerrains();

        yield return new WaitForSeconds(firstFlightDelay);

        while (true)
        {
            AircraftFlight prefab = aircraftPrefabs[Random.Range(0, aircraftPrefabs.Length)];

            if (prefab != null)
            {
                bool done = false;
                Launch(prefab, () => done = true);
                yield return new WaitUntil(() => done); // one aircraft at a time
            }

            yield return new WaitForSeconds(Random.Range(waitBetweenFlights.x, waitBetweenFlights.y));
        }
    }

    private void Launch(AircraftFlight prefab, System.Action onDone)
    {
        Vector3 center = transform.position;
        float altitude = Random.Range(prefab.altitudeRange.x, prefab.altitudeRange.y);

        float startAngle = Random.Range(0f, 360f);
        float endAngle = startAngle + 180f + Random.Range(-exitSpread, exitSpread);

        Vector3 start = center + Direction(startAngle) * radius + Vector3.up * altitude;
        Vector3 end = center + Direction(endAngle) * radius + Vector3.up * altitude;

        // Push the curve's middle point sideways for a gentle arc
        Vector3 mid = (start + end) * 0.5f;
        Vector3 side = Vector3.Cross(Vector3.up, (end - start).normalized);
        Vector3 control = mid + side * Random.Range(-1f, 1f) * prefab.curviness * radius;

        AircraftFlight aircraft = Instantiate(prefab, start, Quaternion.identity, transform);
        aircraft.Fly(start, control, end, onDone);
    }

    // Finds the combined area of every terrain in the scene, centers on
    // it, and (optionally) sizes the circle to enclose all of it.
    private void FitToTerrains()
    {
        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0) return;

        Vector3 min = new Vector3(float.MaxValue, 0f, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, 0f, float.MinValue);

        foreach (Terrain terrain in terrains)
        {
            Vector3 pos = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            min.x = Mathf.Min(min.x, pos.x);
            min.z = Mathf.Min(min.z, pos.z);
            max.x = Mathf.Max(max.x, pos.x + size.x);
            max.z = Mathf.Max(max.z, pos.z + size.z);
        }

        Vector3 center = (min + max) * 0.5f;
        transform.position = new Vector3(center.x, transform.position.y, center.z);

        if (autoRadiusMargin > 0f)
        {
            float halfDiagonal = Vector3.Distance(min, max) * 0.5f;
            radius = halfDiagonal * autoRadiusMargin;
        }

        Debug.Log($"{name}: centered on terrain at {transform.position}, radius {radius:F0}");
    }

    private static Vector3 Direction(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(r), 0f, Mathf.Sin(r));
    }

    // Shows the spawn circle in the Scene view when selected
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 prev = transform.position + Direction(0f) * radius;
        for (int i = 1; i <= 64; i++)
        {
            Vector3 next = transform.position + Direction(i * 360f / 64f) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}