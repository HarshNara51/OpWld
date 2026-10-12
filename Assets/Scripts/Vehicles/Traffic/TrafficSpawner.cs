using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

// Ambient town traffic. Lives on the "Traffic" object (make it a prefab
// and drop it in every scene - all scenes share the same roads).
//
// Routes: every SplineContainer under "Routes" is one lane route from a
// map edge to another. Build them with Tools > Traffic > Build Lanes From
// Roads (re-run it whenever the roads change).
//
// Cars: each Car Model is a plain imported car + its colour versions; a
// random model and colour is picked for every car, and TrafficCar is added
// at runtime (no setup on the models). Tools > Traffic > Fill Car Models
// From "Trf_Crs" fills the list from the cars placed in the scene.
//
// In a mission scene there's no traffic until that mission is passed;
// it starts the moment it's passed.
public class TrafficSpawner : MonoBehaviour
{
    [Serializable]
    public class CarModel
    {
        public string name;
        [Tooltip("The same car in different colours - one is picked at random")]
        public GameObject[] colours = new GameObject[0];
        [Tooltip("Size of the car (the scale it had in the scene)")]
        public float scale = 1f;
        [Tooltip("Higher = this model shows up more often")]
        public float weight = 1f;
    }

    [Header("Cars")]
    public CarModel[] carModels = new CarModel[0];

    [Tooltip("Most traffic cars on the whole map at once")]
    [Min(0)] public int maxCars = 12;

    [Tooltip("Random wait between new cars arriving at the map edge (seconds)")]
    public Vector2 spawnInterval = new Vector2(3f, 8f);

    [Tooltip("Cars already driving around when the scene starts (some of Max Cars)")]
    [Min(0)] public int carsAtStart = 6;

    [Header("Routes")]
    [Tooltip("Parent of the lane route splines (built by Tools > Traffic > Build Lanes From Roads)")]
    public Transform routesRoot;
    [Tooltip("Map-edge spots where cars appear and disappear (used when building lanes)")]
    public Transform spawnPointsRoot;

    [Header("Driving")]
    [Tooltip("Cruising speed range in km/h - each car gets a random one")]
    public Vector2 speedKmh = new Vector2(35f, 50f);
    public float acceleration = 3f;
    public float braking = 6f;
    [Tooltip("Lower = cars take bends slower")]
    public float cornerGrip = 2.5f;
    [Tooltip("Gap (m) a car leaves in front when queueing")]
    public float stopGap = 3f;
    [Tooltip("Seconds before the front wheels turn into a bend")]
    public float steerLeadTime = 1.2f;

    [Header("Lane building (Tools > Traffic > Build Lanes From Roads)")]
    [Tooltip("Parent of the road splines")]
    public Transform roadsRoot;
    [Tooltip("Lane centre, metres right of the road's centre line (asphalt half-width is 5 m)")]
    public float laneOffset = 2.5f;
    [Tooltip("How round the corners at junctions are (metres)")]
    public float cornerRounding = 9f;
    [Tooltip("A road end this close to another road joins it (metres)")]
    public float joinDistance = 12f;

    [Header("Testing")]
    [Tooltip("Editor only: run traffic even if this scene's mission isn't passed yet")]
    public bool ignoreMissionInEditor = false;

    private readonly List<TrafficCar> cars = new List<TrafficCar>();
    private SplineContainer[] routes = new SplineContainer[0];
    private bool filledAtStart;

    private void Start()
    {
        if (routesRoot == null) routesRoot = transform.Find("Routes");
        routes = routesRoot != null ? routesRoot.GetComponentsInChildren<SplineContainer>() : new SplineContainer[0];

        if (routes.Length == 0) Debug.LogWarning($"{name}: no traffic routes - run Tools > Traffic > Build Lanes From Roads.");
        else if (carModels.Length == 0) Debug.LogWarning($"{name}: no car models - run Tools > Traffic > Fill Car Models.");
        else StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        while (true)
        {
            if (TrafficAllowed())
            {
                if (!filledAtStart)
                {
                    filledAtStart = true;
                    for (int i = 0; i < carsAtStart && cars.Count < maxCars; i++) SpawnMidRoute();
                }
                else if (cars.Count < maxCars)
                {
                    SpawnAtEdge();
                }
                yield return new WaitForSeconds(UnityEngine.Random.Range(spawnInterval.x, spawnInterval.y));
            }
            else
            {
                yield return new WaitForSeconds(1f);
            }
        }
    }

    // No traffic in a mission scene until its mission is passed
    private bool TrafficAllowed()
    {
#if UNITY_EDITOR
        if (ignoreMissionInEditor) return true;
#endif
        GameManager gm = GameManager.Instance;
        return gm == null || !gm.IsInUnfinishedMission(gameObject.scene.name);
    }

    private void SpawnAtEdge()
    {
        // Routes whose start isn't blocked by a car that just arrived
        var free = new List<SplineContainer>();
        foreach (SplineContainer r in routes)
        {
            TrafficPath p = TrafficPath.For(r);
            if (p != null && IsClear(p.Start, 14f)) free.Add(r);
        }
        if (free.Count == 0) return;
        Spawn(TrafficPath.For(free[UnityEngine.Random.Range(0, free.Count)]), 0f);
    }

    // Somewhere along a route, away from the player and other cars
    private void SpawnMidRoute()
    {
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            TrafficPath p = TrafficPath.For(routes[UnityEngine.Random.Range(0, routes.Length)]);
            if (p == null || p.Length < 60f) continue;
            float d = UnityEngine.Random.Range(20f, p.Length - 30f);
            Vector3 at = p.PointAt(d);
            if (player != null && Vector3.Distance(player.position, at) < 50f) continue;
            if (!IsClear(at, 25f)) continue;
            Spawn(p, d);
            return;
        }
    }

    private bool IsClear(Vector3 point, float radius)
    {
        foreach (TrafficCar c in TrafficCar.All)
            if (Vector3.Distance(c.transform.position, point) < radius) return false;
        return true;
    }

    private void Spawn(TrafficPath path, float distance)
    {
        CarModel model = PickModel();
        if (model == null) return;
        GameObject prefab = model.colours[UnityEngine.Random.Range(0, model.colours.Length)];
        if (prefab == null) return;

        GameObject go = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
        go.name = $"{model.name} (traffic)";
        go.transform.localScale = prefab.transform.localScale * model.scale;

        TrafficCar car = go.AddComponent<TrafficCar>();
        car.cruiseSpeed = UnityEngine.Random.Range(speedKmh.x, speedKmh.y) / 3.6f;
        car.acceleration = acceleration;
        car.braking = braking;
        car.cornerGrip = cornerGrip;
        car.stopGap = stopGap;
        car.steerLeadTime = steerLeadTime;
        car.Setup(path, distance);

        cars.Add(car);
        car.Finished += c => cars.Remove(c);
    }

    private CarModel PickModel()
    {
        float total = 0f;
        foreach (CarModel m in carModels)
            if (m.colours != null && m.colours.Length > 0) total += Mathf.Max(0f, m.weight);
        float pick = UnityEngine.Random.value * total;
        foreach (CarModel m in carModels)
        {
            if (m.colours == null || m.colours.Length == 0) continue;
            pick -= Mathf.Max(0f, m.weight);
            if (pick <= 0f) return m;
        }
        return null;
    }

    private void OnDestroy()
    {
        foreach (TrafficCar c in cars) if (c != null) Destroy(c.gameObject);
        cars.Clear();
    }
}
