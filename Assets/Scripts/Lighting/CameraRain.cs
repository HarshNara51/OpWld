using UnityEngine;
using UnityEngine.Audio;

// NFS Most Wanted style rain: it only rains in a box around the camera, not over the whole map.
// The box follows the camera (and leads ahead of it while driving), drops simulate in world
// space so they don't slide around when you turn, and they die when they hit anything -
// so roofs, bridges and the tunnel keep you dry. Some drops leave a small ripple splash.
//
// Drop count follows the graphics preset (Low -> Ultra). An optional looping rain sound
// plays through the Ambience mixer group and gets quieter under cover.
public class CameraRain : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private Material dropMaterial;
    [SerializeField] private Material splashMaterial;
    [SerializeField] private Color dropColor = new Color(0.78f, 0.82f, 0.9f, 0.45f);
    [SerializeField] private Color splashColor = new Color(0.8f, 0.85f, 0.9f, 0.35f);

    [Header("Area & motion")]
    [Tooltip("Half-size of the rain box around the camera (metres)")]
    [SerializeField] private float radius = 22f;
    [Tooltip("How high above the camera drops start")]
    [SerializeField] private float spawnHeight = 14f;
    [SerializeField] private float fallSpeed = 18f;
    [Tooltip("Sideways push (m/s) - makes the rain fall at a slant")]
    [SerializeField] private Vector2 wind = new Vector2(1.5f, 0.8f);
    [Tooltip("Seconds of camera movement to lead the box by, so you don't drive out of the rain")]
    [SerializeField] private float leadTime = 0.7f;

    [Header("Drops per second for Low / Medium / High / Ultra")]
    [SerializeField] private float[] dropsPerSecond = { 2000f, 3000f, 5000f, 7000f };
    [Tooltip("Share of drops that leave a splash")]
    [Range(0f, 1f)] [SerializeField] private float splashChance = 0.25f;
    [Tooltip("What drops hit (leave out layers like the player if they should pass through)")]
    [SerializeField] private LayerMask collidesWith = ~0;

    [Header("Sound (optional)")]
    [Tooltip("A looping rain sound - leave empty for silent rain")]
    [SerializeField] private AudioClip rainLoop;
    [SerializeField] private AudioMixerGroup ambienceGroup;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.6f;
    [Tooltip("Volume multiplier while something is overhead (tunnel, roof)")]
    [Range(0f, 1f)] [SerializeField] private float coveredVolume = 0.3f;

    private ParticleSystem drops;
    private ParticleSystem splashes;
    private AudioSource audioSource;
    private Vector3 lastCamPos;
    private Vector3 camVelocity;
    private float coverCheckTimer;
    private bool covered;

    private void Start()
    {
        splashes = BuildSplashes();
        drops = BuildDrops(splashes);
        if (rainLoop != null) audioSource = BuildAudio();

        GraphicsQualityManager.PresetChanged += ApplyPreset;
        ApplyPreset(GraphicsQualityManager.Instance != null ? GraphicsQualityManager.Instance.Current : GraphicsQualityManager.Preset.High);

        Camera cam = Camera.main;
        if (cam != null) lastCamPos = cam.transform.position;
        drops.Play();
    }

    private void OnDestroy()
    {
        GraphicsQualityManager.PresetChanged -= ApplyPreset;
    }

    private void ApplyPreset(GraphicsQualityManager.Preset preset)
    {
        if (drops == null) return;
        float rate = dropsPerSecond[Mathf.Clamp((int)preset, 0, dropsPerSecond.Length - 1)];
        var emission = drops.emission;
        emission.rateOverTime = rate;
        var main = drops.main;
        main.maxParticles = Mathf.CeilToInt(rate * main.startLifetime.constantMax * 1.2f);
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || drops == null) return;

        Vector3 p = cam.transform.position;
        if (Time.deltaTime > 0f)
            camVelocity = Vector3.Lerp(camVelocity, (p - lastCamPos) / Time.deltaTime, 5f * Time.deltaTime);
        lastCamPos = p;

        // Lead the box in the direction we're moving (flat), so driving fast stays inside the rain.
        Vector3 lead = Vector3.ClampMagnitude(new Vector3(camVelocity.x, 0f, camVelocity.z) * leadTime, radius);
        drops.transform.position = p + lead + Vector3.up * spawnHeight;

        coverCheckTimer -= Time.deltaTime;
        if (coverCheckTimer <= 0f)
        {
            coverCheckTimer = 0.25f;
            covered = Physics.Raycast(p, Vector3.up, out RaycastHit hit, 40f, collidesWith, QueryTriggerInteraction.Ignore);
            // Ground overhead = inside a tunnel: the rain box could start inside the hill, so stop it.
            bool underground = covered && hit.collider is TerrainCollider;
            var emission = drops.emission;
            if (emission.enabled == underground) emission.enabled = !underground;
        }

        if (audioSource != null)
        {
            float want = volume * (covered ? coveredVolume : 1f);
            audioSource.volume = Mathf.MoveTowards(audioSource.volume, want, Time.deltaTime);
        }
    }

    private ParticleSystem BuildDrops(ParticleSystem splash)
    {
        var go = new GameObject("RainDrops");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float life = (spawnHeight + 4f) / fallSpeed;
        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = life;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.035f);
        main.startColor = dropColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;
        main.maxParticles = 8000;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius * 2f, 0.5f, radius * 2f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = wind.x; velocity.y = -fallSpeed; velocity.z = wind.y;

        // Die on contact with anything solid - this is what keeps tunnels and roofs dry.
        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.collidesWith = collidesWith;
        collision.lifetimeLoss = 1f;
        collision.bounce = 0f;
        collision.radiusScale = 0.1f;
        collision.enableDynamicColliders = false;
        collision.maxCollisionShapes = 128;

        if (splash != null)
        {
            var sub = ps.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing, splashChance);
        }

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = dropMaterial;
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.035f;
        r.lengthScale = 1f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    private ParticleSystem BuildSplashes()
    {
        var go = new GameObject("RainSplashes");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.startColor = splashColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1500;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

        // Splash crown pops up, grows and fades (camera-facing, so it never flickers in the ground).
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f));
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = splashMaterial;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.pivot = new Vector3(0f, -0.5f, 0f); // sit on the ground instead of half inside it
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    private AudioSource BuildAudio()
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.clip = rainLoop;
        src.loop = true;
        src.spatialBlend = 0f;
        src.volume = volume;
        src.outputAudioMixerGroup = ambienceGroup;
        src.Play();
        return src;
    }
}
