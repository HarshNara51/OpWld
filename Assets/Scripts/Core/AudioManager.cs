using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// One AudioManager for the whole game. It lives on the Managers prefab, so
// it exists in every scene (an extra copy deletes itself).
//
// ADDING A NEW SOUND LATER:
//   1. Add one line under the right header, e.g.  public AudioClip trainHorn;
//   2. Drag the clip into its new slot on the Managers prefab
//   3. Play it from any script:
//        AudioManager.Instance.PlaySFX(AudioManager.Instance.trainHorn);
//      or, for a sound that comes from a spot in the world:
//        AudioManager.Instance.PlaySFXAt(AudioManager.Instance.trainHorn, transform.position);
//      or, for something that keeps going while a condition is true (call every frame):
//        AudioManager.Instance.Loop(this, clip, isCharging);
//
// Empty slots are fine - playing an unassigned clip just does nothing.
//
// It also runs the background sound on its own:
//   - Music: the title track in the main menu; the car radio playlist only while
//     driving outside missions (Hub, or after a mission is passed/failed), fading
//     in/out as you get in and out of a car.
//   - Ambience: one bed per scene picked by its SceneTimeOfDay, swapped for the
//     indoor bed inside an AmbienceZone, plus a light wind and a speed wind.
//   - A click for any mouse click while the cursor is free (menus, map...).
//   - A chime when the player walks/drives into a gizmo.
//   - Engine/rotor loops on traffic, trucks, the train and aircraft, and
//     footsteps on the player (see LoopingSound, VehicleEngineAudio, PlayerFootsteps).
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
    [Tooltip("Loops under the last seconds of a mission timer")]
    public AudioClip timerTick;
    public AudioClip bombBeeps;
    public AudioClip bombDefused;
    public AudioClip explosion;
    [Tooltip("Spotted / busted")]
    public AudioClip alertWhoosh;
    public AudioClip empCharge;
    public AudioClip empZap;
    [Tooltip("A photo-hunt item was photographed")]
    public AudioClip photoCollected;
    [Tooltip("Reward cars unlocked (Blaze, bonus taxi)")]
    public AudioClip unlockJingle;

    [Header("Player")]
    public AudioClip gunShot;
    public AudioClip gunReload;
    [Tooltip("Pulling the trigger with no ammo left")]
    public AudioClip gunEmpty;
    public AudioClip knifeStab;
    public AudioClip playerHurt;
    public AudioClip npcHurt;
    [Tooltip("Walking loop outside (grass). Empty = the indoor one is used everywhere")]
    public AudioClip footstepsOutdoor;
    public AudioClip footstepsIndoor;

    [Header("Vehicles & world")]
    public AudioClip carEngine;
    public AudioClip tyreScreech;
    public AudioClip carHorn;
    public AudioClip carDoorOpen;
    public AudioClip carDoorClose;
    [Tooltip("Hitting something hard while driving (louder the harder the hit)")]
    public AudioClip carCrash;
    public AudioClip truckLoop;
    public AudioClip trainLoop;
    public AudioClip trainHorn;
    public AudioClip helicopterLoop;
    [Tooltip("Plane fly-by, played once per flight (slowed to fit)")]
    public AudioClip planeFlyBy;
    public AudioClip policeSiren;
    public AudioClip fuelPump;
    public AudioClip spray;

    [Header("UI")]
    public AudioClip buttonClick;
    public AudioClip buttonHover;
    public AudioClip pauseOpen;

    [Header("Music")]
    [Tooltip("Main menu")]
    public AudioClip titleMusic;
    [Tooltip("Car radio: plays (shuffled) only while driving outside missions")]
    public AudioClip[] carPlaylist;
    [SerializeField] private float musicFadeSeconds = 1.5f;

    [Header("Ambience (one per time of day, see SceneTimeOfDay)")]
    public AudioClip ambienceMorning;
    public AudioClip ambienceDay;
    public AudioClip ambienceEvening;
    public AudioClip ambienceNight;
    public AudioClip ambienceDawn;
    public AudioClip ambienceOvercast;
    [Tooltip("Inside an AmbienceZone (hotel lobby...)")]
    public AudioClip indoorAmbience;
    [Tooltip("Quiet wind + leaves outdoors")]
    public AudioClip windLight;
    [Tooltip("Rushing air that grows with driving speed")]
    public AudioClip windSpeed;
    [Tooltip("Waves - comes from the nearest water surface (any renderer with a 'water' material)")]
    public AudioClip waterLoop;
    [Range(0f, 1f)] [SerializeField] private float ambienceLevel = 0.7f;

    private AudioSource musicSource;
    private AudioSource ambienceSource;
    private AudioSource indoorSource;
    private AudioSource windLightSource;
    private AudioSource windSpeedSource;
    private AudioSource waterSource;
    private readonly List<Vector3> water = new List<Vector3>();
    private AudioSource sfxSource;
    private AudioSource uiSource;

    private float musicGain;
    private bool musicPaused;
    private int lastTrack = -1;
    private readonly Dictionary<Object, AudioSource> loops = new Dictionary<Object, AudioSource>();
    private readonly HashSet<GizmoLabel> gizmosInside = new HashSet<GizmoLabel>();
    private Transform player;
    private float nextScan, nextGizmoCheck;
    private bool isMenu;

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
        indoorSource = CreateSource("Indoor", ambienceGroup, true);
        windLightSource = CreateSource("Wind", ambienceGroup, true);
        windSpeedSource = CreateSource("Wind (speed)", ambienceGroup, true);
        waterSource = CreateSource("Water", ambienceGroup, true);
        waterSource.spatialBlend = 1f;
        waterSource.rolloffMode = AudioRolloffMode.Linear;
        waterSource.minDistance = 8f;
        waterSource.maxDistance = 70f;
        waterSource.dopplerLevel = 0f;
        sfxSource = CreateSource("SFX", sfxGroup, false);
        uiSource = CreateSource("UI", uiGroup, false);

        // Music and UI sounds keep playing while the game is paused (timeScale 0)
        uiSource.ignoreListenerPause = true;
        musicSource.ignoreListenerPause = true;
        musicSource.volume = 0f;
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void Start()
    {
        // Unity ignores mixer changes made in Awake, so saved volumes load here
        LoadVolume(MasterVolume);
        LoadVolume(MusicVolume);
        LoadVolume(SFXVolume);
        LoadVolume(AmbienceVolume);
        LoadVolume(UIVolume);
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != this) return;
        isMenu = scene.name == "MainMenu";

        foreach (var kv in loops) if (kv.Value != null) Destroy(kv.Value.gameObject);
        loops.Clear();
        gizmosInside.Clear();
        player = null;
        nextScan = 0f;

        StartBed(ambienceSource, isMenu ? null : AmbienceForScene());
        StartBed(indoorSource, isMenu ? null : indoorAmbience);
        StartBed(windLightSource, isMenu ? null : windLight);
        StartBed(windSpeedSource, isMenu ? null : windSpeed);
        indoorSource.volume = 0f;
        windSpeedSource.volume = 0f;

        // Water in this scene - the waves sound sits on the nearest bit of it that you can SEE.
        // Water sheets often run on under the terrain; those hidden parts are skipped.
        water.Clear();
        if (!isMenu && waterLoop != null)
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Material m = r.sharedMaterial;
                if (m != null && m.name.ToLowerInvariant().Contains("water") && !r.name.ToLowerInvariant().Contains("rock")) AddVisibleWater(r.bounds);
            }
        StartBed(waterSource, water.Count > 0 ? waterLoop : null);
        waterSource.volume = ambienceLevel;
    }

    private AudioClip AmbienceForScene()
    {
        switch (SceneTimeOfDay.Current)
        {
            case SceneTimeOfDay.TimeOfDay.Morning: return ambienceMorning;
            case SceneTimeOfDay.TimeOfDay.Evening: return ambienceEvening;
            case SceneTimeOfDay.TimeOfDay.Night: return ambienceNight;
            case SceneTimeOfDay.TimeOfDay.Dawn: return ambienceDawn;
            case SceneTimeOfDay.TimeOfDay.Overcast: return ambienceOvercast;
            default: return ambienceDay;
        }
    }

    private static void StartBed(AudioSource source, AudioClip clip)
    {
        if (source.clip == clip && (clip == null || source.isPlaying)) return;
        source.Stop();
        source.clip = clip;
        if (clip == null) return;
        source.time = Random.Range(0f, clip.length * 0.8f); // don't start every scene at the same second
        source.Play();
    }

    private void Update()
    {
        // A click for every mouse click while the cursor is free (menus, map, pause...)
        if (Cursor.lockState != CursorLockMode.Locked && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
            PlayUI(buttonClick);

        UpdateMusic();
        StopOrphanLoops();
        if (isMenu) return;

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            ScanWorld();
        }
        UpdateAmbience();
        if (Time.unscaledTime >= nextGizmoCheck)
        {
            nextGizmoCheck = Time.unscaledTime + 0.1f;
            UpdateGizmos();
        }
    }

    // ---------- Music ----------

    private void UpdateMusic()
    {
        bool want = false;
        if (isMenu)
        {
            if (titleMusic != null)
            {
                if (musicSource.clip != titleMusic && musicGain <= 0.001f) SetTrack(titleMusic, true);
                want = musicSource.clip == titleMusic;
            }
        }
        else if (carPlaylist != null && carPlaylist.Length > 0 && VehicleInteraction.Current != null && Time.timeScale > 0f
                 && (!MissionStatus.HasMissionManager() || MissionStatus.HasMissionEnded()))
        {
            bool onRadio = System.Array.IndexOf(carPlaylist, musicSource.clip) >= 0;
            if (!onRadio && musicGain <= 0.001f) NextTrack();
            want = System.Array.IndexOf(carPlaylist, musicSource.clip) >= 0;
            // song finished - next one
            if (want && !musicPaused && !musicSource.isPlaying) NextTrack();
        }

        float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, musicFadeSeconds);
        musicGain = Mathf.MoveTowards(musicGain, want ? 1f : 0f, step);
        musicSource.volume = musicGain;

        if (!want && musicGain <= 0f && musicSource.isPlaying) { musicSource.Pause(); musicPaused = true; }
        else if (want && musicPaused) { musicSource.UnPause(); musicPaused = false; }
    }

    private void SetTrack(AudioClip clip, bool loop)
    {
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicPaused = false;
        if (clip != null) musicSource.Play();
    }

    private void NextTrack()
    {
        int n = carPlaylist.Length, pick = Random.Range(0, n);
        if (n > 1 && pick == lastTrack) pick = (pick + 1) % n;
        lastTrack = pick;
        SetTrack(carPlaylist[pick], false);
    }

    // ---------- Ambience ----------

    private void UpdateAmbience()
    {
        Vector3 at = ListenerPosition();
        bool indoors = AmbienceZone.Contains(at, out bool chatter);
        float dt = Time.unscaledDeltaTime;
        ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, ambienceLevel * (indoors ? 0.15f : 1f), dt * 0.7f);
        indoorSource.volume = Mathf.MoveTowards(indoorSource.volume, chatter ? ambienceLevel : 0f, dt * 0.7f);
        UpdateWater(at);
        windLightSource.volume = Mathf.MoveTowards(windLightSource.volume, indoors ? 0f : ambienceLevel * 0.35f, dt * 0.7f);

        float speed = 0f;
        VehicleInteraction car = VehicleInteraction.Current;
        if (car != null && car.TryGetComponent(out Rigidbody rb)) speed = rb.linearVelocity.magnitude;
        float rush = Mathf.InverseLerp(8f, 38f, speed);
        windSpeedSource.volume = Mathf.MoveTowards(windSpeedSource.volume, rush * 0.8f, dt * 1.5f);
        windSpeedSource.pitch = 0.85f + rush * 0.35f;
    }

    public bool PlayerIndoors => AmbienceZone.Contains(ListenerPosition());

    private Vector3 ListenerPosition()
    {
        if (VehicleInteraction.Current != null) return VehicleInteraction.Current.transform.position;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        return player != null ? player.position : Vector3.one * 99999f;
    }

    // Samples the water surface every few metres and keeps only the spots where the
    // ground is lower than the water - the water you can actually see
    private const float WaterStep = 10f;
    private void AddVisibleWater(Bounds b)
    {
        float y = b.max.y;
        for (float x = b.min.x; x <= b.max.x; x += WaterStep)
            for (float z = b.min.z; z <= b.max.z; z += WaterStep)
            {
                Vector3 p = new Vector3(x, y, z);
                if (GroundHeight(p) < y - 0.05f) water.Add(p);
            }
    }

    private static float GroundHeight(Vector3 p)
    {
        foreach (Terrain t in Terrain.activeTerrains)
        {
            Vector3 o = t.transform.position, s = t.terrainData.size;
            if (p.x >= o.x && p.x <= o.x + s.x && p.z >= o.z && p.z <= o.z + s.z) return t.SampleHeight(p) + o.y;
        }
        return float.MinValue; // off the terrain = open water
    }

    private float nextWaterCheck;
    private void UpdateWater(Vector3 at)
    {
        if (water.Count == 0 || Time.unscaledTime < nextWaterCheck) return;
        nextWaterCheck = Time.unscaledTime + 0.25f;
        Vector3 best = water[0];
        float bestSqr = float.MaxValue;
        foreach (Vector3 p in water)
        {
            float d = (p - at).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = p; }
        }
        waterSource.transform.position = best;
    }

    // ---------- World loops (traffic, trucks, train, aircraft, footsteps) ----------

    private void ScanWorld()
    {
        foreach (TrafficCar car in TrafficCar.All)
            if (car != null && car.GetComponent<LoopingSound>() == null)
            {
                TrafficCar c = car;
                LoopingSound.Add(car.gameObject, carEngine, sfxGroup, 0.3f, 4f, 35f, () => c.Speed, 14f, 0.75f, 1.25f);
            }

        foreach (SplineVehicle v in FindObjectsByType<SplineVehicle>(FindObjectsSortMode.None))
        {
            if (v.GetComponent<LoopingSound>() != null) continue;
            bool train = SceneManager.GetActiveScene().name == "Mission_Railway";
            if (train && v.leader != null) { v.gameObject.AddComponent<LoopingSound>().enabled = false; continue; } // carriages: the engine covers them
            SplineVehicle sv = v;
            if (train) LoopingSound.Add(v.gameObject, trainLoop, sfxGroup, 1f, 15f, 160f, () => sv.CurrentSpeed, 12f, 0.85f, 1.1f, 0.25f);
            else LoopingSound.Add(v.gameObject, truckLoop, sfxGroup, 0.9f, 8f, 90f, () => sv.CurrentSpeed, 12f, 0.8f, 1.15f, 0.35f);
        }

        foreach (AircraftFlight a in FindObjectsByType<AircraftFlight>(FindObjectsSortMode.None))
        {
            if (a.GetComponent<LoopingSound>() != null || a.GetComponent<FlyBySound>() != null || a.RemainingSeconds <= 0f) continue;
            string n = a.name.ToLowerInvariant();
            bool heli = n.Contains("heli") || n.Contains("chopper") || a.speed < 50f;
            if (heli) LoopingSound.Add(a.gameObject, helicopterLoop, sfxGroup, 1f, 60f, 650f, null, 1f, 1f, 1f);
            else FlyBySound.Add(a, planeFlyBy, sfxGroup); // planes: the whole fly-by once, stretched over the flight
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null && p.GetComponent<PlayerFootsteps>() == null) p.AddComponent<PlayerFootsteps>();
    }

    // ---------- Gizmos ----------

    private void UpdateGizmos()
    {
        Vector3 from = ListenerPosition();
        foreach (GizmoLabel label in GizmoLabel.All)
        {
            if (label == null) continue;
            Vector3 o = label.transform.position - from; o.y = 0f;
            bool inside = label.Visible && o.magnitude <= label.useDistance + 0.5f;
            if (inside && gizmosInside.Add(label)) PlayUI(gizmoEnter);
            else if (!inside && gizmosInside.Remove(label)) PlayUI(gizmoExit);
        }
        gizmosInside.RemoveWhere(l => l == null);
    }

    // ---------- Playing sounds ----------

    // 2D sound effect (same loudness everywhere) - pickups, deliveries, stingers
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, volume);
    }

    // 3D sound effect at a world position - gets quieter with distance
    public void PlaySFXAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 80f)
    {
        if (clip == null) return;

        GameObject temp = new GameObject($"SFX_{clip.name}");
        temp.transform.position = position;

        AudioSource source = temp.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.outputAudioMixerGroup = sfxGroup;
        source.spatialBlend = 1f; // fully 3D
        source.minDistance = Mathf.Min(5f, maxDistance * 0.1f);
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.dopplerLevel = 0f;
        source.Play();

        Destroy(temp, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }

    public void PlayUI(AudioClip clip)
    {
        if (clip != null) uiSource.PlayOneShot(clip);
    }

    // Keeps a looping sound going while 'on' is true - call it every frame from the owner.
    // at = null: 2D (same everywhere). Stops by itself when the owner is destroyed or the scene changes.
    public void Loop(Object owner, AudioClip clip, bool on, float volume = 1f, float pitch = 1f, Transform at = null, float maxDistance = 60f)
    {
        loops.TryGetValue(owner, out AudioSource src);
        if (!on || clip == null)
        {
            if (src != null) Destroy(src.gameObject);
            loops.Remove(owner);
            return;
        }
        if (src == null)
        {
            var go = new GameObject("Loop_" + clip.name);
            if (at != null) go.transform.SetParent(at, false);
            else go.transform.SetParent(transform, false);
            src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.outputAudioMixerGroup = sfxGroup;
            src.spatialBlend = at != null ? 1f : 0f;
            src.minDistance = Mathf.Min(5f, maxDistance * 0.1f);
            src.maxDistance = maxDistance;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.dopplerLevel = 0f;
            src.Play();
            loops[owner] = src;
        }
        src.volume = volume;
        src.pitch = pitch;
    }

    // A loop whose owner was destroyed or switched off stops with it
    private readonly List<Object> orphans = new List<Object>();
    private void StopOrphanLoops()
    {
        if (loops.Count == 0) return;
        orphans.Clear();
        foreach (var kv in loops)
            if (kv.Key == null || (kv.Key is Behaviour b && !b.isActiveAndEnabled) || kv.Value == null) orphans.Add(kv.Key);
        foreach (Object o in orphans)
        {
            if (loops.TryGetValue(o, out AudioSource s) && s != null) Destroy(s.gameObject);
            loops.Remove(o);
        }
    }

    // Old API, kept for anything that still calls it: plays a looping music track now
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource.clip == clip) return;
        SetTrack(clip, true);
    }

    public void StopMusic()
    {
        musicSource.Stop();
        musicSource.clip = null;
    }

    // ---------- Volume (settings sliders) ----------

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

    public AudioMixerGroup SfxGroup => sfxGroup;

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
