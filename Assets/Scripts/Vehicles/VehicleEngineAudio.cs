using UnityEngine;

// Engine + tyre screech for a drivable car. VehicleInteraction adds it to every
// drivable car, so there's nothing to set up. Parked cars are silent; the engine
// starts when you get in, rises in pitch with speed, and dies away when you get out.
// Sporty cars (Blaze, racing car, Champion, Outlaw) sit a little higher, the taxi lower.
public class VehicleEngineAudio : MonoBehaviour
{
    private VehicleInteraction car;
    private Rigidbody rb;
    private PrometeoCarController controller;
    private AudioSource engine, screech, horn;
    private float basePitch = 1f;
    private float nextCrash;

    // Crash sound on a hard hit, louder the harder it is (not for landing on the ground or bumping the player)
    private void OnCollisionEnter(Collision collision)
    {
        AudioManager am = AudioManager.Instance;
        if (am == null || am.carCrash == null || Time.time < nextCrash) return;
        if (collision.collider.CompareTag("Player")) return;
        float impact = collision.relativeVelocity.magnitude;
        if (impact < 5f) return;
        if (collision.contactCount > 0 && collision.GetContact(0).normal.y > 0.7f) return; // ground
        nextCrash = Time.time + 0.5f;
        am.PlaySFXAt(am.carCrash, collision.contactCount > 0 ? collision.GetContact(0).point : transform.position,
                     Mathf.Clamp01(impact / 20f), Random.Range(0.92f, 1.08f), 70f);
    }

    private void Start()
    {
        car = GetComponent<VehicleInteraction>();
        rb = GetComponent<Rigidbody>();
        controller = GetComponent<PrometeoCarController>();

        string n = name.ToLowerInvariant();
        if (n.Contains("blaze") || n.Contains("racing") || n.Contains("champion") || n.Contains("outlaw")) basePitch = 1.12f;
        else if (n.Contains("taxi")) basePitch = 0.92f;
        else if (n.Contains("truck") || n.Contains("van")) basePitch = 0.78f;
    }

    private AudioSource MakeSource(AudioClip clip)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.outputAudioMixerGroup = AudioManager.Instance.SfxGroup;
        s.spatialBlend = 0.85f; // mostly 3D, but never vanishes from the driver's seat
        s.minDistance = 6f;
        s.maxDistance = 60f;
        s.dopplerLevel = 0f;
        s.volume = 0f;
        return s;
    }

    private void Update()
    {
        AudioManager am = AudioManager.Instance;
        if (am == null || car == null) return;
        if (engine == null && am.carEngine != null) engine = MakeSource(am.carEngine);
        if (screech == null && am.tyreScreech != null) screech = MakeSource(am.tyreScreech);

        bool driving = VehicleInteraction.Current == car && Time.timeScale > 0f;
        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        float k = Mathf.Clamp01(speed / 30f);

        if (engine != null)
        {
            if (driving && !engine.isPlaying) engine.Play();
            engine.pitch = basePitch * (0.75f + k * 1.0f);
            engine.volume = Mathf.MoveTowards(engine.volume, driving ? 0.45f + k * 0.4f : 0f, Time.unscaledDeltaTime * (driving ? 1.5f : 0.8f));
            if (!driving && engine.volume <= 0f && engine.isPlaying) engine.Stop();
        }

        // Horn (H by default) while held
        if (horn == null && am.carHorn != null) horn = MakeSource(am.carHorn);
        if (horn != null)
        {
            bool honk = driving && GameKeys.Held(GameAction.Horn);
            if (honk && !horn.isPlaying) { horn.volume = 0.9f; horn.Play(); }
            else if (!honk && horn.isPlaying) horn.Stop();
        }

        if (screech != null)
        {
            bool skid = driving && controller != null && (controller.isDrifting || (controller.isTractionLocked && Mathf.Abs(controller.carSpeed) > 12f)) && speed > 5f;
            if (skid && !screech.isPlaying) screech.Play();
            screech.volume = Mathf.MoveTowards(screech.volume, skid ? 0.55f : 0f, Time.unscaledDeltaTime * 4f);
            if (!skid && screech.volume <= 0f && screech.isPlaying) screech.Stop();
        }
    }
}
