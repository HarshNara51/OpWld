using UnityEngine;

// Cheap ground mist for players with "God Rays" (volumetric fog) switched OFF.
// A few dozen big, faint, soft particles drift low over the ground around the camera,
// so misty scenes (Railway, Farm dawn) still look misty on Low/Medium/High.
// Turns itself off while the volumetric fog is on - AERO already draws real mist then.
public class GroundMistFallback : MonoBehaviour
{
    [Tooltip("Soft particle material (Assets/Settings/VolumetricFog/MistFallback.mat)")]
    [SerializeField] private Material mistMaterial;
    [SerializeField] private Color color = new Color(0.8f, 0.84f, 0.88f, 0.12f);
    [Tooltip("Size of the mist area around the camera (metres)")]
    [SerializeField] private float radius = 45f;
    [Tooltip("Height of the mist above the camera's ground level")]
    [SerializeField] private float height = 0.8f;
    [Tooltip("Drift direction and speed (m/s)")]
    [SerializeField] private Vector3 wind = new Vector3(0.6f, 0f, 0.3f);
    [SerializeField] private int maxParticles = 120;

    private ParticleSystem mist;

    private void Start()
    {
        mist = BuildParticles();
        GraphicsQualityManager.VolumetricFogChanged += OnFogChanged;
        OnFogChanged(GraphicsQualityManager.Instance != null && GraphicsQualityManager.Instance.VolumetricFog);
    }

    private void OnDestroy()
    {
        GraphicsQualityManager.VolumetricFogChanged -= OnFogChanged;
    }

    private void OnFogChanged(bool volumetricOn)
    {
        if (mist == null) return;
        if (volumetricOn) mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        else if (!mist.isPlaying) mist.Play();
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (mist == null || cam == null) return;

        // Follow the camera; particles simulate in world space so they don't slide with it.
        Vector3 p = cam.transform.position;
        Terrain t = Terrain.activeTerrain;
        float ground = t != null ? t.SampleHeight(p) + t.transform.position.y : p.y - 1.7f;
        mist.transform.position = new Vector3(p.x, ground + height, p.z);
    }

    private ParticleSystem BuildParticles()
    {
        var go = new GameObject("GroundMistFallback");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 20f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(10f, 18f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.prewarm = true;

        var emission = ps.emission;
        emission.rateOverTime = maxParticles / 17f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius * 2f, 0.6f, radius * 2f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = wind.x; velocity.y = wind.y; velocity.z = wind.z;

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        // Fade in and out so particles never pop.
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mistMaterial;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 3f;
        r.sortingFudge = 10f;
        return ps;
    }
}
