using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Ambient life for NPCs who just stand (or sit) around: idling,
// breathing, chatting. No Animator Controller needed - put this next to
// the character's Animator and drop a few clips into the list.
//
// Each NPC picks a random clip, starts it at a random point, plays at a
// slightly random speed, and every so often crossfades to another clip.
// So a group of friends never moves in sync.
//
// Standing NPCs: give them standing clips (idles + talks).
// Sitting NPCs: give them ONLY sitting clips.
[RequireComponent(typeof(Animator))]
public class AmbientNPC : MonoBehaviour
{
    [Tooltip("Looping clips this NPC can play (e.g. Breathing Idle + Talking 1-4)")]
    [SerializeField] private AnimationClip[] clips;

    [Tooltip("Switch to a different clip every so often")]
    [SerializeField] private bool switchClips = true;

    [Tooltip("Random seconds between switches (min, max)")]
    [SerializeField] private Vector2 switchEvery = new Vector2(8f, 16f);

    [Tooltip("Seconds to blend from one clip to the next")]
    [SerializeField] private float crossfadeTime = 0.6f;

    [Tooltip("Random playback speed (min, max) - small differences keep groups natural")]
    [SerializeField] private Vector2 speedRange = new Vector2(0.92f, 1.08f);

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private int activeInput;
    private int currentClip = -1;
    private float switchTimer;
    private float fade = 1f;

    private void Start()
    {
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"{name}: AmbientNPC has no clips assigned.");
            return;
        }

        Animator animator = GetComponent<Animator>();
        animator.applyRootMotion = false;                               // stay exactly where placed
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms; // cheaper when off-screen

        graph = PlayableGraph.Create($"{name}_Ambient");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        output.SetSourcePlayable(mixer);

        currentClip = Random.Range(0, clips.Length);
        activeInput = 0;
        ConnectClip(activeInput, currentClip);
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);

        switchTimer = Random.Range(switchEvery.x, switchEvery.y);
        graph.Play();
    }

    private void Update()
    {
        if (!graph.IsValid()) return;

        // Crossfade in progress
        if (fade < 1f)
        {
            fade = Mathf.Min(1f, fade + Time.deltaTime / Mathf.Max(0.01f, crossfadeTime));
            mixer.SetInputWeight(activeInput, fade);
            mixer.SetInputWeight(1 - activeInput, 1f - fade);
        }

        if (!switchClips || clips.Length < 2) return;

        switchTimer -= Time.deltaTime;
        if (switchTimer > 0f) return;

        switchTimer = Random.Range(switchEvery.x, switchEvery.y);

        int next;
        do { next = Random.Range(0, clips.Length); } while (next == currentClip);

        int newInput = 1 - activeInput;
        ConnectClip(newInput, next);
        activeInput = newInput;
        currentClip = next;
        fade = 0f;
    }

    private void ConnectClip(int input, int clipIndex)
    {
        // Throw away whatever was playing on this input before
        Playable old = mixer.GetInput(input);
        if (old.IsValid())
        {
            graph.Disconnect(mixer, input);
            old.Destroy();
        }

        AnimationClip clip = clips[clipIndex];
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetSpeed(Random.Range(speedRange.x, speedRange.y));
        playable.SetTime(Random.Range(0f, clip.length)); // start somewhere in the loop

        graph.Connect(playable, 0, mixer, input);
    }

    private void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }
}
