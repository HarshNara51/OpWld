using UnityEngine;
using UnityEngine.Audio;

// A plane's fly-by sound, played ONCE across its whole flight. AudioManager adds
// it to each plane as it spawns. The recording is slowed down to fit the flight
// (never below minPitch, so it doesn't turn into a groan); if the flight is still
// longer, it starts a little late so the loud "overhead" middle of the recording
// lands in the middle of the flight.
public class FlyBySound : MonoBehaviour
{
    private const float MinPitch = 0.6f, MaxPitch = 1.15f;

    public static void Add(AircraftFlight plane, AudioClip clip, AudioMixerGroup group)
    {
        var fb = plane.gameObject.AddComponent<FlyBySound>();
        if (clip == null) return;

        float seconds = plane.RemainingSeconds;
        if (seconds <= 1f) return; // not flying (yet) - nothing to fit

        float pitch = Mathf.Clamp(clip.length / seconds, MinPitch, MaxPitch);
        float played = clip.length / pitch;              // how long it lasts at that speed
        float delay = Mathf.Max(0f, (seconds - played) * 0.5f);

        var s = plane.gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = false;
        s.playOnAwake = false;
        s.pitch = pitch;
        s.outputAudioMixerGroup = group;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 90f;    // planes fly 120-200 m up: still clearly heard overhead
        s.maxDistance = 1500f;
        s.dopplerLevel = 0f;    // the recording already has its own whoosh
        s.PlayDelayed(delay);
    }
}
