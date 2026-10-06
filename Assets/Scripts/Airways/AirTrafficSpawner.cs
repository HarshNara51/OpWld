using System.Collections;
using UnityEngine;

// Put this on an empty GameObject at the CENTER of the map, at ground
// level. Every so often it launches a random aircraft - sometimes a pair
// flying in formation - from a point on a big circle around the map,
// across the sky to roughly the opposite side, where it disappears.
//
// Each flight picks a path style: straight, a sweeping arc, or a
// mid-flight turn (flies off at an angle, then banks toward its exit).
// Make this a prefab and drop it only into scenes that should have air
// traffic (e.g. not the stormy/snowy ones).
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

    [Header("Timing")]
    [Tooltip("Seconds before the first aircraft appears")]
    [SerializeField] private float firstFlightDelay = 5f;

    [Tooltip("Random wait between flights (min, max seconds)")]
    [SerializeField] private Vector2 waitBetweenFlights = new Vector2(20f, 60f);

    [Header("Path style chances (relative weights)")]
    [SerializeField] private float straightWeight = 1f;
    [SerializeField] private float arcWeight = 1f;
    [SerializeField] private float midTurnWeight = 1.5f;

    [Tooltip("How sharp mid-flight turns are (degrees, min/max)")]
    [SerializeField] private Vector2 turnAngleRange = new Vector2(35f, 75f);

    [Tooltip("How far (degrees) the exit point can be from straight across")]
    [Range(0f, 90f)]
    [SerializeField] private float exitSpread = 40f;

    [Header("Pairs")]
    [Tooltip("Chance that a flight is a pair flying in formation (0-1)")]
    [Range(0f, 1f)][SerializeField] private float pairChance = 0.25f;
    [Tooltip("Sideways gap between the two aircraft")]
    [SerializeField] private Vector2 pairSpacing = new Vector2(25f, 45f);
    [Tooltip("Height difference between the two aircraft")]
    [SerializeField] private float pairHeightOffset = 12f;

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
                int stillFlying = 0;
                bool pair = Random.value < pairChance;

                BuildPath(prefab, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);

                stillFlying++;
                Launch(prefab, a, b, c, d, () => stillFlying--);

                if (pair)
                {
                    // Same aircraft type, offset sideways and a little higher - a formation
                    Vector3 side = Vector3.Cross(Vector3.up, (d - a).normalized);
                    float spacing = Random.Range(pairSpacing.x, pairSpacing.y) * (Random.value < 0.5f ? 1f : -1f);
                    Vector3 offset = side * spacing + Vector3.up * pairHeightOffset - (d - a).normalized * 15f;

                    stillFlying++;
                    Launch(prefab, a + offset, b + offset, c + offset, d + offset, () => stillFlying--);
                }

                yield return new WaitUntil(() => stillFlying <= 0); // one flight (or pair) at a time
            }

            yield return new WaitForSeconds(Random.Range(waitBetweenFlights.x, waitBetweenFlights.y));
        }
    }

    private void Launch(AircraftFlight prefab, Vector3 a, Vector3 b, Vector3 c, Vector3 d, System.Action onDone)
    {
        AircraftFlight aircraft = Instantiate(prefab, a, Quaternion.identity, transform);
        aircraft.Fly(a, b, c, d, onDone);
    }

    // Picks start/end on the circle and shapes the curve between them
    private void BuildPath(AircraftFlight prefab, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
    {
        Vector3 center = transform.position;
        float altitude = Random.Range(prefab.altitudeRange.x, prefab.altitudeRange.y);

        float startAngle = Random.Range(0f, 360f);
        float endAngle = startAngle + 180f + Random.Range(-exitSpread, exitSpread);

        a = center + Direction(startAngle) * radius + Vector3.up * altitude;
        d = center + Direction(endAngle) * radius + Vector3.up * altitude;

        Vector3 across = d - a;
        float length = across.magnitude;
        Vector3 dir = across / length;
        Vector3 side = Vector3.Cross(Vector3.up, dir);

        // Curvier aircraft favour arcs and turns; straight-flyers favour straight lines
        float straightW = straightWeight * (1.5f - prefab.curviness);
        float arcW = arcWeight * (0.5f + prefab.curviness);
        float turnW = midTurnWeight * (0.5f + prefab.curviness);
        float roll = Random.Range(0f, straightW + arcW + turnW);

        if (roll < straightW)
        {
            // Straight across
            b = a + across / 3f;
            c = a + across * 2f / 3f;
        }
        else if (roll < straightW + arcW)
        {
            // Sweeping arc - both handles pushed to the same side
            float bend = Random.Range(0.2f, 0.45f) * length * (Random.value < 0.5f ? 1f : -1f);
            b = a + across / 3f + side * bend;
            c = a + across * 2f / 3f + side * bend;
        }
        else
        {
            // Mid-flight turn: heads off at an angle, then banks toward the exit
            float angle = Random.Range(turnAngleRange.x, turnAngleRange.y) * (Random.value < 0.5f ? 1f : -1f);
            Vector3 offDir = Quaternion.Euler(0f, angle, 0f) * dir;
            b = a + offDir * length * 0.55f;
            c = d - dir * length * 0.25f;
        }
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