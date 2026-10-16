using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Blends one mission NPC between their ambient action and the shared idle.
/// Looping ambient actions finish their current loop before the blend begins.
/// Lawrence holds the final sit-to-stand pose while talking, then plays that
/// same clip backwards to sit down again.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class NpcMissionAnimator : MonoBehaviour
{
    public enum AmbientBehaviour { Idle, LoopingAction, LawrenceSitting }

    [SerializeField] private AmbientBehaviour behaviour;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip ambientClip;
    [SerializeField] private AnimationClip transitionClip;
    [SerializeField, Min(0.05f)] private float blendDuration = 0.28f;

    private Animator animator;
    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationClipPlayable ambientPlayable;
    private AnimationClipPlayable foregroundPlayable;
    private AnimationClipPlayable lawrencePosePlayable;
    private Coroutine transitionRoutine;
    private bool playerPresent;
    private bool initialized;

    public void Configure(AmbientBehaviour newBehaviour, AnimationClip idle, AnimationClip ambient, AnimationClip transition)
    {
        behaviour = newBehaviour;
        idleClip = idle;
        ambientClip = ambient;
        transitionClip = transition;
        TryInitialize();
    }

    private void OnEnable() => TryInitialize();

    private void TryInitialize()
    {
        if (initialized || !isActiveAndEnabled) return;
        animator = GetComponent<Animator>();
        if (idleClip == null) return;

        // These are stationary NPC performances. Root motion and IK authored for the
        // source skeleton can badly pull or twist a differently-proportioned target rig.
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        AnimationClip startingClip = behaviour == AmbientBehaviour.Idle ? idleClip : ambientClip;
        if (startingClip == null) startingClip = idleClip;

        graph = PlayableGraph.Create($"NPC animation - {name}");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        ambientPlayable = CreateClipPlayable(startingClip);
        foregroundPlayable = CreateClipPlayable(idleClip);
        graph.Connect(ambientPlayable, 0, mixer, 0);
        graph.Connect(foregroundPlayable, 0, mixer, 1);
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        AnimationPlayableOutput.Create(graph, "NPC output", animator).SetSourcePlayable(mixer);
        graph.Play();
        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;
        Loop(ambientPlayable);
        Loop(foregroundPlayable);
    }

    private static void Loop(AnimationClipPlayable playable)
    {
        if (!playable.IsValid() || playable.GetSpeed() <= 0d) return;
        AnimationClip clip = playable.GetAnimationClip();
        if (clip == null || clip.length <= 0f || playable.GetTime() < clip.length) return;
        playable.SetTime(playable.GetTime() % clip.length);
    }

    private AnimationClipPlayable CreateClipPlayable(AnimationClip clip)
    {
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        return playable;
    }

    private void OnDisable()
    {
        initialized = false;
        if (graph.IsValid()) graph.Destroy();
    }

    public void SetPlayerPresent(bool present)
    {
        if (playerPresent == present) return;
        playerPresent = present;
        if (!initialized || behaviour == AmbientBehaviour.Idle) return;

        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(behaviour == AmbientBehaviour.LawrenceSitting
            ? ChangeLawrencePose(present)
            : ChangeLoopingAction(present));
    }

    private IEnumerator ChangeLoopingAction(bool toIdle)
    {
        if (toIdle)
        {
            // Do not cut Carraway, Eric, or the monk off mid-action.
            double length = Mathf.Max(0.01f, ambientClip.length);
            double remainder = length - (ambientPlayable.GetTime() % length);
            if (remainder < length - 0.02d)
                yield return WaitRealtime((float)remainder);

            foregroundPlayable.SetTime(0d);
            foregroundPlayable.SetSpeed(1d);
            foregroundPlayable.Play();
            yield return Blend(0, 1);
        }
        else
        {
            ambientPlayable.SetTime(0d);
            ambientPlayable.SetSpeed(1d);
            ambientPlayable.Play();
            yield return Blend(1, 0);
        }

        transitionRoutine = null;
    }

    private IEnumerator ChangeLawrencePose(bool standUp)
    {
        if (transitionClip == null)
        {
            yield return Blend(standUp ? 0 : 1, standUp ? 1 : 0);
            transitionRoutine = null;
            yield break;
        }

        graph.Disconnect(mixer, 1);
        if (lawrencePosePlayable.IsValid()) lawrencePosePlayable.Destroy();
        if (foregroundPlayable.IsValid())
        {
            foregroundPlayable.Destroy();
            foregroundPlayable = default;
        }

        AnimationClipPlayable poseChange = CreateClipPlayable(transitionClip);
        graph.Connect(poseChange, 0, mixer, 1);
        poseChange.SetTime(standUp ? 0d : transitionClip.length);
        poseChange.SetSpeed(standUp ? 1d : -1d);
        poseChange.Play();
        yield return Blend(0, 1);

        float middleTime = Mathf.Max(0f, transitionClip.length - blendDuration);
        yield return WaitRealtime(middleTime);

        if (standUp)
        {
            // Do not switch to the shared idle: its authored root pose differs
            // from this clip's last frame and makes Lawrence visibly slide.
            poseChange.SetTime(transitionClip.length);
            poseChange.SetSpeed(0d);
            // Keep this playable connected and fully weighted as his talk pose.
            lawrencePosePlayable = poseChange;
        }
        else
        {
            poseChange.SetTime(0d);
            poseChange.SetSpeed(0d);
            ambientPlayable = CreateClipPlayable(ambientClip);
            graph.Disconnect(mixer, 0);
            graph.Connect(ambientPlayable, 0, mixer, 0);
            ambientPlayable.Play();
            yield return Blend(1, 0);
        }

        // The standing pose must remain alive for the duration of dialogue.
        // On the way down, input 0 has been restored to the seated loop and
        // owns the output, so the temporary reverse playable can be released.
        if (!standUp)
        {
            graph.Disconnect(mixer, 1);
            poseChange.Destroy();
            lawrencePosePlayable = default;
        }
        transitionRoutine = null;
    }

    private IEnumerator Blend(int fromInput, int toInput)
    {
        float elapsed = 0f;
        while (elapsed < blendDuration)
        {
            elapsed += Time.deltaTime;
            float amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / blendDuration));
            mixer.SetInputWeight(fromInput, 1f - amount);
            mixer.SetInputWeight(toInput, amount);
            yield return null;
        }

        mixer.SetInputWeight(fromInput, 0f);
        mixer.SetInputWeight(toInput, 1f);
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }
}
