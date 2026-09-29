using UnityEngine;
using UnityEngine.Events;

// Hides spline vehicles until they pass the Entry point, and hides them
// again once they pass the Exit point - the train rolls out of one
// tunnel and vanishes into the other.
//
// Only renderers/colliders are switched off; the GameObjects stay
// active, so the locomotive keeps simulating while hidden and its
// followers never freeze.
//
// Put on an empty GameObject (e.g. "TrainGates"). Entry/Exit are empty
// GameObjects placed just inside each tunnel mouth, on the rails.
public class SplineVisibilityGates : MonoBehaviour
{
    [Tooltip("Every car, locomotive included")]
    [SerializeField] private SplineVehicle[] vehicles;
    [SerializeField] private Transform entryPoint;
    [SerializeField] private Transform exitPoint;
    [Tooltip("Also disable colliders while hidden, so stacked cars inside the tunnel can't block anything")]
    [SerializeField] private bool toggleColliders = true;

    [Tooltip("Fires once, when every vehicle has passed the Exit point")]
    public UnityEvent onAllPassedExit;

    private float entryDistance;
    private float exitDistance;
    private Renderer[][] renderers;
    private Collider[][] colliders;
    private bool[] visible;
    private bool allPassedFired;

    private void Start()
    {
        if (vehicles == null || vehicles.Length == 0 || entryPoint == null || exitPoint == null)
        {
            Debug.LogWarning($"{name}: assign the vehicles and both entry/exit points.");
            enabled = false;
            return;
        }

        entryDistance = vehicles[0].DistanceAtWorldPoint(entryPoint.position);
        exitDistance = vehicles[0].DistanceAtWorldPoint(exitPoint.position);

        if (entryDistance >= exitDistance)
        {
            Debug.LogWarning($"{name}: Entry is not before Exit along the spline - swap the two points or reverse the spline's direction.");
        }

        int n = vehicles.Length;
        renderers = new Renderer[n][];
        colliders = new Collider[n][];
        visible = new bool[n];

        for (int i = 0; i < n; i++)
        {
            renderers[i] = vehicles[i].GetComponentsInChildren<Renderer>(true);
            colliders[i] = vehicles[i].GetComponentsInChildren<Collider>(true);
            visible[i] = true;
            SetVisible(i, false); // everything starts hidden
        }
    }

    private void Update()
    {
        bool allPassed = true;

        for (int i = 0; i < vehicles.Length; i++)
        {
            float d = vehicles[i].DistanceTravelled;
            bool show = d > entryDistance && d < exitDistance;
            if (show != visible[i]) SetVisible(i, show);
            if (d < exitDistance) allPassed = false;
        }

        if (allPassed && !allPassedFired)
        {
            allPassedFired = true;
            Debug.Log("Whole train has passed the exit.");
            onAllPassedExit.Invoke();
        }
    }

    private void SetVisible(int i, bool show)
    {
        visible[i] = show;
        foreach (Renderer r in renderers[i]) r.enabled = show;

        if (toggleColliders)
        {
            foreach (Collider c in colliders[i]) c.enabled = show;
        }
    }
}