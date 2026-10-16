using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a non-looping music clip once, with a gentle fade in and a fade out at
/// the end of the clip. The playback host survives a scene change, so a cue can
/// begin while the screen is black and continue into the next scene.
/// </summary>
public sealed class OneShotMusicCue : MonoBehaviour
{
    [Header("Music")]
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [SerializeField, Min(0f)] private float fadeInSeconds = 1.5f;
    [SerializeField, Min(0f)] private float fadeOutSeconds = 8f;
    [SerializeField] private bool playOnStart;

    [Header("Playback")]
    [SerializeField] private bool playOnlyOnce = true;
    [SerializeField] private bool interruptOtherOneShotMusic;

    private bool hasPlayed;

    public bool HasPlayed => hasPlayed;

    private void Start()
    {
        if (playOnStart) Play();
    }

    /// <summary>Used by the editor scene setup and available to runtime setup code.</summary>
    public void Configure(AudioClip musicClip, float targetVolume, float fadeIn, float fadeOut, bool autoPlay)
    {
        clip = musicClip;
        volume = Mathf.Clamp01(targetVolume);
        fadeInSeconds = Mathf.Max(0f, fadeIn);
        fadeOutSeconds = Mathf.Max(0f, fadeOut);
        playOnStart = autoPlay;
    }

    /// <summary>Safe to call from UnityEvents, trigger zones, and game code.</summary>
    public void Play()
    {
        if (clip == null)
        {
            Debug.LogWarning($"Music cue '{name}' has no clip assigned.", this);
            return;
        }

        if (playOnlyOnce && hasPlayed) return;
        hasPlayed = true;
        MusicCuePlaybackHost.Play(clip, volume, fadeInSeconds, fadeOutSeconds, interruptOtherOneShotMusic);
    }

    public void ResetCue() => hasPlayed = false;

    /// <summary>Fades the currently playing one-shot music to silence and stops it.</summary>
    public void FadeOutAndStop(float seconds)
    {
        MusicCuePlaybackHost.FadeOutAndStop(seconds);
    }

    private static class MusicCuePlaybackHost
    {
        private static MusicCueHost host;

        public static void Play(AudioClip clip, float volume, float fadeIn, float fadeOut, bool interrupt)
        {
            if (host == null)
            {
                GameObject hostObject = new GameObject("One-Shot Music Playback");
                Object.DontDestroyOnLoad(hostObject);
                host = hostObject.AddComponent<MusicCueHost>();
            }

            host.Play(clip, volume, fadeIn, fadeOut, interrupt);
        }

        public static void FadeOutAndStop(float seconds)
        {
            if (host != null) host.FadeOutAndStop(seconds);
        }
    }

    private sealed class MusicCueHost : MonoBehaviour
    {
        private AudioSource source;
        private Coroutine playback;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
        }

        public void Play(AudioClip clip, float volume, float fadeIn, float fadeOut, bool interrupt)
        {
            if (source.isPlaying && !interrupt) return;
            if (playback != null) StopCoroutine(playback);
            source.Stop();
            playback = StartCoroutine(PlayRoutine(clip, Mathf.Clamp01(volume), fadeIn, fadeOut));
        }

        public void FadeOutAndStop(float seconds)
        {
            if (playback != null) StopCoroutine(playback);
            if (seconds <= 0f)
            {
                source.volume = 0f;
                source.Stop();
                source.clip = null;
                playback = null;
                return;
            }
            playback = StartCoroutine(FadeOutRoutine(Mathf.Max(0f, seconds)));
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            if (!source.isPlaying)
            {
                source.volume = 0f;
                source.clip = null;
                playback = null;
                yield break;
            }

            float startingVolume = source.volume;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(startingVolume, 0f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            source.volume = 0f;
            source.Stop();
            source.clip = null;
            playback = null;
        }

        private IEnumerator PlayRoutine(AudioClip clip, float targetVolume, float requestedFadeIn, float requestedFadeOut)
        {
            // Keep both fades inside the clip, even for a very short cue.
            float totalFade = requestedFadeIn + requestedFadeOut;
            float scale = totalFade > clip.length && totalFade > 0f ? clip.length / totalFade : 1f;
            float fadeIn = requestedFadeIn * scale;
            float fadeOut = requestedFadeOut * scale;
            float fadeOutStart = Mathf.Max(fadeIn, clip.length - fadeOut);

            source.clip = clip;
            source.volume = fadeIn > 0f ? 0f : targetVolume;
            source.Play();

            for (float elapsed = 0f; elapsed < clip.length && source.isPlaying; elapsed += Time.unscaledDeltaTime)
            {
                float multiplier = 1f;
                if (fadeIn > 0f && elapsed < fadeIn)
                    multiplier = Mathf.SmoothStep(0f, 1f, elapsed / fadeIn);
                else if (fadeOut > 0f && elapsed >= fadeOutStart)
                    multiplier = 1f - Mathf.SmoothStep(0f, 1f, (elapsed - fadeOutStart) / fadeOut);

                source.volume = targetVolume * multiplier;
                yield return null;
            }

            source.Stop();
            source.clip = null;
            playback = null;
        }
    }
}
