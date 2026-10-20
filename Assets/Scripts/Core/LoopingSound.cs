using System;
using UnityEngine;
using UnityEngine.Audio;

// A 3D looping sound on a moving thing (traffic car, truck, train, aircraft).
// AudioManager adds these at runtime - nothing to set up in the scenes.
// With a speed source, it gets louder and higher as the thing speeds up
// (idleVolume of the full volume when standing still).
public class LoopingSound : MonoBehaviour
{
    private AudioSource source;
    private Func<float> speed;
    private float topSpeed = 1f, lowPitch = 1f, highPitch = 1f, volume = 1f, idleVolume = 1f;

    public static LoopingSound Add(GameObject go, AudioClip clip, AudioMixerGroup group, float volume, float minDistance, float maxDistance,
                                   Func<float> speed, float topSpeed, float lowPitch, float highPitch, float idleVolume = 0.6f)
    {
        var ls = go.AddComponent<LoopingSound>();
        if (clip == null) { ls.enabled = false; return ls; }
        ls.speed = speed; ls.topSpeed = Mathf.Max(0.1f, topSpeed);
        ls.lowPitch = lowPitch; ls.highPitch = highPitch; ls.volume = volume; ls.idleVolume = idleVolume;

        var s = ls.source = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.outputAudioMixerGroup = group;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = minDistance;
        s.maxDistance = maxDistance;
        s.dopplerLevel = 0.3f;
        s.volume = speed == null ? volume : volume * idleVolume;
        s.time = UnityEngine.Random.Range(0f, clip.length); // cars side by side don't sound identical
        s.Play();
        return ls;
    }

    private void Update()
    {
        if (source == null || speed == null) return;
        float k = Mathf.Clamp01(Mathf.Abs(speed()) / topSpeed);
        source.pitch = Mathf.Lerp(lowPitch, highPitch, k);
        source.volume = Mathf.MoveTowards(source.volume, volume * Mathf.Lerp(idleVolume, 1f, k), Time.deltaTime);
    }
}
