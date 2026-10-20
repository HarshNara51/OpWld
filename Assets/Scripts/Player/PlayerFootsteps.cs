using UnityEngine;

// Footsteps for the player. AudioManager adds this to the Player at runtime.
// Plays the walking loop while moving on the ground (sped up when running),
// using the indoor sound inside an AmbienceZone and the outdoor one elsewhere.
[RequireComponent(typeof(CharacterController))]
public class PlayerFootsteps : MonoBehaviour
{
    [SerializeField] private float volume = 0.45f;
    [Tooltip("Faster than this (m/s) counts as running")]
    [SerializeField] private float runSpeed = 3.2f;
    [SerializeField] private float runPitch = 1.35f;
    [Header("Outdoors (grass)")]
    [Tooltip("Grass steps are louder and slower in the recording than the indoor ones")]
    [SerializeField] private float outdoorVolume = 0.5f;
    [SerializeField] private float outdoorRunPitch = 1.75f;

    private CharacterController controller;
    private AudioSource source;
    private Vector3 lastPos;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        lastPos = transform.position;
    }

    private void Update()
    {
        AudioManager am = AudioManager.Instance;
        if (am == null || Time.deltaTime <= 0f) return;
        if (source.outputAudioMixerGroup == null) source.outputAudioMixerGroup = am.SfxGroup;

        Vector3 delta = transform.position - lastPos; delta.y = 0f;
        lastPos = transform.position;
        float speed = delta.magnitude / Time.deltaTime;

        bool walking = speed > 0.4f && speed < 12f && controller.enabled && controller.isGrounded
                       && VehicleInteraction.Current == null && !PlayerDeath.IsDead;

        AudioClip want = am.PlayerIndoors || am.footstepsOutdoor == null ? am.footstepsIndoor : am.footstepsOutdoor;
        if (source.clip != want) { source.clip = want; if (want != null && walking) source.Play(); }

        bool grass = want == am.footstepsOutdoor && want != am.footstepsIndoor;
        if (walking && want != null && !source.isPlaying) source.Play();
        source.pitch = speed > runSpeed ? (grass ? outdoorRunPitch : runPitch) : 1f;
        float target = walking ? volume * (grass ? outdoorVolume : 1f) : 0f;
        source.volume = Mathf.MoveTowards(source.volume, target, Time.deltaTime * 6f);
        if (!walking && source.volume <= 0f && source.isPlaying) source.Pause();
    }
}
