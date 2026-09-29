using UnityEngine;
using UnityEngine.Audio;

// One AudioManager for the whole game. Put the AudioManager prefab in
// EVERY scene (hub + all missions) - if one already exists from a
// previous scene, the extra copy deletes itself, so it's always safe.
//
// ADDING A NEW SOUND LATER:
//   1. Add one line under the right header, e.g.  public AudioClip trainHorn;
//   2. Drag the clip into its new slot on the prefab
//   3. Play it from any script:
//        AudioManager.Instance.PlaySFX(AudioManager.Instance.trainHorn);
//      or, for a sound that comes from a spot in the world:
//        AudioManager.Instance.PlaySFXAt(AudioManager.Instance.trainHorn, transform.position);
//
// Empty slots are fine - playing an unassigned clip just does nothing.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // Exposed parameter names in the Audio Mixer - must match exactly
    public const string MasterVolume = "MasterVolume";
    public const string MusicVolume = "MusicVolume";
    public const string SFXVolume = "SFXVolume";
    public const string AmbienceVolume = "AmbienceVolume";
    public const string UIVolume = "UIVolume";

    [Header("Mixer (set once)")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioMixerGroup ambienceGroup;
    [SerializeField] private AudioMixerGroup uiGroup;

    [Header("Gizmos")]
    public AudioClip gizmoEnter;
    public AudioClip gizmoExit;
    public AudioClip gizmoInteract;

    [Header("Mission")]
    public AudioClip cargoPickup;
    public AudioClip cargoDeliver;
    public AudioClip actionDenied;      // e.g. "Train is moving, not able to deliver"
    public AudioClip countdownTick;     // 3, 2, 1
    public AudioClip countdownGo;       // GO!
    public AudioClip missionComplete;
    public AudioClip missionFailed;

    [Header("World")]
    public AudioClip trainHorn;

    [Header("UI")]
    public AudioClip buttonClick;
    public AudioClip buttonHover;

    [Header("Music")]
    public AudioClip hubMusic;
    public AudioClip missionMusic;

    private AudioSource musicSource;
    private AudioSource ambienceSource;
    private AudioSource sfxSource;
    private AudioSource uiSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // one already carried over from another scene
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = CreateSource("Music", musicGroup, true);
        ambienceSource = CreateSource("Ambience", ambienceGroup, true);
        sfxSource = CreateSource("SFX", sfxGroup, false);
        uiSource = CreateSource("UI", uiGroup, false);

        // UI sounds must keep playing while the game is paused (timeScale 0)
        uiSource.ignoreListenerPause = true;
    }

    private void Start()
    {
        // Unity ignores mixer changes made in Awake, so saved volumes load here
        LoadVolume(MasterVolume);
        LoadVolume(MusicVolume);
        LoadVolume(SFXVolume);
        LoadVolume(AmbienceVolume);
        LoadVolume(UIVolume);
    }

    // ---------- Playing sounds ----------

    // 2D sound effect (same loudness everywhere) - pickups, deliveries, stingers
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, volume);
    }

    // 3D sound effect at a world position - gets quieter with distance
    public void PlaySFXAt(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        GameObject temp = new GameObject($"SFX_{clip.name}");
        temp.transform.position = position;

        AudioSource source = temp.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.outputAudioMixerGroup = sfxGroup;
        source.spatialBlend = 1f; // fully 3D
        source.Play();

        Destroy(temp, clip.length + 0.1f);
    }

    public void PlayUI(AudioClip clip)
    {
        if (clip != null) uiSource.PlayOneShot(clip);
    }

    // Loops a music track. Calling it again with the same track does nothing,
    // so music doesn't restart when it's already playing.
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource.clip == clip) return;
        musicSource.clip = clip;
        musicSource.Play();
    }

    public void StopMusic()
    {
        musicSource.Stop();
        musicSource.clip = null;
    }

    // Loops a background ambience bed (wind, birds, rain, city hum)
    public void PlayAmbience(AudioClip clip)
    {
        if (clip == null || ambienceSource.clip == clip) return;
        ambienceSource.clip = clip;
        ambienceSource.Play();
    }

    public void StopAmbience()
    {
        ambienceSource.Stop();
        ambienceSource.clip = null;
    }

    // ---------- Volume (ready for settings sliders later) ----------

    // value: 0 = silent, 1 = full. Saved automatically between sessions.
    public void SetVolume(string parameter, float value)
    {
        value = Mathf.Clamp01(value);
        if (mixer != null) mixer.SetFloat(parameter, ToDecibels(value));
        PlayerPrefs.SetFloat(parameter, value);
    }

    public float GetVolume(string parameter)
    {
        return PlayerPrefs.GetFloat(parameter, 1f);
    }

    private void LoadVolume(string parameter)
    {
        SetVolume(parameter, GetVolume(parameter));
    }

    // Mixer volume is in decibels; sliders feel natural on a 0-1 scale
    private static float ToDecibels(float value)
    {
        return Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f;
    }

    private AudioSource CreateSource(string label, AudioMixerGroup group, bool loop)
    {
        GameObject child = new GameObject(label);
        child.transform.SetParent(transform);

        AudioSource source = child.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = group;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D
        return source;
    }
}
