using UnityEngine;

// Railway (rain): water spray kicked up by the tyres of whatever car the player is driving.
// One shared world-space particle system; every frame each grounded wheel emits a few soft
// puffs at its contact point, thrown backwards and up - more and bigger the faster you go,
// nothing when standing still. Put it on the scene's Weather object (next to CameraRain).
public class RainTyreSpray : MonoBehaviour
{
    [Tooltip("Soft particle material (e.g. Assets/Settings/VolumetricFog/MistFallback.mat)")]
    [SerializeField] private Material sprayMaterial;
    [SerializeField] private Color sprayColor = new Color(0.82f, 0.86f, 0.9f, 0.35f);
    [Tooltip("Below this speed (m/s) the tyres throw no spray")]
    [SerializeField] private float minSpeed = 2f;
    [Tooltip("Puffs per second per wheel at Full Speed")]
    [SerializeField] private float puffsPerSecond = 40f;
    [Tooltip("Speed (m/s) where the spray is at its fullest")]
    [SerializeField] private float fullSpeed = 25f;

    private ParticleSystem spray;
    private VehicleInteraction car;
    private WheelCollider[] wheels;
    private Rigidbody body;
    private float[] carry = new float[0];

    private void Start()
    {
        spray = BuildParticles();
    }

    private void LateUpdate()
    {
        VehicleInteraction current = VehicleInteraction.Current;
        if (current == null || !current.IsDriving) { car = null; return; }
        if (current != car)
        {
            car = current;
            wheels = car.GetComponentsInChildren<WheelCollider>();
            body = car.GetComponent<Rigidbody>();
            carry = new float[wheels.Length];
        }
        if (body == null || wheels.Length == 0) return;

        float speed = body.linearVelocity.magnitude;
        if (speed < minSpeed) return;
        float k = Mathf.Clamp01((speed - minSpeed) / (fullSpeed - minSpeed));

        Vector3 back = -car.transform.forward;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (!wheels[i].GetGroundHit(out WheelHit hit)) continue;
            carry[i] += puffsPerSecond * k * Time.deltaTime;
            while (carry[i] >= 1f)
            {
                carry[i] -= 1f;
                var p = new ParticleSystem.EmitParams
                {
                    position = hit.point + Vector3.up * 0.1f + Random.insideUnitSphere * 0.15f,
                    velocity = back * (speed * Random.Range(0.15f, 0.3f)) + Vector3.up * Random.Range(0.8f, 2.2f)
                               + car.transform.right * Random.Range(-1f, 1f),
                    startSize = Mathf.Lerp(0.35f, 1.1f, k) * Random.Range(0.7f, 1.2f),
                    startLifetime = Random.Range(0.35f, 0.7f),
                    startColor = sprayColor,
                    rotation = Random.Range(0f, 360f)
                };
                spray.Emit(p, 1);
            }
        }
    }

    private ParticleSystem BuildParticles()
    {
        var go = new GameObject("RainTyreSpray");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.6f;
        main.maxParticles = 800;
        var emission = ps.emission; emission.enabled = false; // everything comes from Emit()
        var shape = ps.shape; shape.enabled = false;

        // Grow and fade out
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));
        var fade = ps.colorOverLifetime; fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.limit = 100f; drag.drag = 2.5f; // air slows the spray

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = sprayMaterial;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }
}
