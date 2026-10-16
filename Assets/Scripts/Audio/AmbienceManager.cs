using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class AmbienceManager : MonoBehaviour
{
    [Header("Listener")]
    [SerializeField, Tooltip("Optional. When empty, the active AudioListener is used.")]
    private Transform listenerOverride;

    [Header("Ambience Loops")]
    [SerializeField] private AudioClip calmWind;
    [SerializeField] private AudioClip forestWind;
    [SerializeField] private AudioClip sea;
    [SerializeField] private AudioMixerGroup ambienceMixerGroup;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float calmVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float forestVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float seaVolume = 0.8f;
    [SerializeField, Range(0f, 1f), Tooltip("Calm wind never falls below this share of its normal volume.")]
    private float minimumCalmPresence = 0.6f;
    [SerializeField, Min(0.05f)] private float crossfadeSeconds = 2.5f;
    [SerializeField, Range(0.5f, 2f), Tooltip("Prevents several overlapping zones from becoming too loud.")]
    private float maximumCombinedOverlay = 1.15f;
    [SerializeField] private bool persistBetweenScenes;

    [Header("Runtime (read only)")]
    [SerializeField] private AudioSource calmSource;
    [SerializeField] private AudioSource forestSource;
    [SerializeField] private AudioSource seaSource;
    [SerializeField, Range(0f, 1f)] private float forestInfluence;
    [SerializeField, Range(0f, 1f)] private float seaInfluence;

    private Transform cachedListener;
    private float nextListenerSearchTime;

    public float ForestInfluence => forestInfluence;
    public float SeaInfluence => seaInfluence;
    public float CalmVolume => calmVolume;
    public float ForestVolume => forestVolume;
    public float SeaVolume => seaVolume;
    public float MinimumCalmPresence => minimumCalmPresence;

    private void Awake()
    {
        EnsureAudioSources();

        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        StartLoop(calmSource);
        StartLoop(forestSource);
        StartLoop(seaSource);

        // Calm wind is the world-wide bed and is audible even before the first blend update.
        if (calmSource != null)
        {
            calmSource.volume = calmVolume;
        }
    }

    private void Update()
    {
        Transform listener = ResolveListener();
        Vector3 listenerPosition = listener != null ? listener.position : transform.position;

        float targetForest = 0f;
        float targetSea = 0f;

        foreach (AmbienceZone zone in AmbienceZone.ActiveZones)
        {
            if (zone == null || !zone.isActiveAndEnabled)
            {
                continue;
            }

            float influence = zone.GetInfluence(listenerPosition);
            if (zone.Type == AmbienceType.Forest)
            {
                targetForest = Mathf.Max(targetForest, influence);
            }
            else
            {
                targetSea = Mathf.Max(targetSea, influence);
            }
        }

        float combined = targetForest + targetSea;
        if (combined > maximumCombinedOverlay)
        {
            float scale = maximumCombinedOverlay / combined;
            targetForest *= scale;
            targetSea *= scale;
        }

        float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.05f, crossfadeSeconds));
        forestInfluence = Mathf.Lerp(forestInfluence, targetForest, blend);
        seaInfluence = Mathf.Lerp(seaInfluence, targetSea, blend);

        float strongestOverlay = Mathf.Max(forestInfluence, seaInfluence);
        float calmPresence = Mathf.Lerp(1f, minimumCalmPresence, strongestOverlay);

        SetVolume(calmSource, calmVolume * calmPresence);
        SetVolume(forestSource, forestVolume * forestInfluence);
        SetVolume(seaSource, seaVolume * seaInfluence);
    }

    private Transform ResolveListener()
    {
        if (listenerOverride != null)
        {
            return listenerOverride;
        }

        if (cachedListener != null && cachedListener.gameObject.activeInHierarchy)
        {
            return cachedListener;
        }

        if (Time.unscaledTime < nextListenerSearchTime)
        {
            return null;
        }

        nextListenerSearchTime = Time.unscaledTime + 1f;
        AudioListener listener = FindFirstObjectByType<AudioListener>();
        cachedListener = listener != null
            ? listener.transform
            : Camera.main != null ? Camera.main.transform : null;
        return cachedListener;
    }

    private static void SetVolume(AudioSource source, float volume)
    {
        if (source != null)
        {
            source.volume = Mathf.Clamp01(volume);
        }
    }

    private static void StartLoop(AudioSource source)
    {
        if (source != null && source.clip != null && !source.isPlaying)
        {
            source.Play();
        }
    }

    public void Configure(AudioClip calm, AudioClip forest, AudioClip ocean)
    {
        calmWind = calm;
        forestWind = forest;
        sea = ocean;
        EnsureAudioSources();
    }

    public void ConfigureMix(float calm, float forest, float ocean, float minimumCalm)
    {
        calmVolume = Mathf.Clamp01(calm);
        forestVolume = Mathf.Clamp01(forest);
        seaVolume = Mathf.Clamp01(ocean);
        minimumCalmPresence = Mathf.Clamp01(minimumCalm);

        if (Application.isPlaying)
        {
            SetVolume(calmSource, calmVolume);
        }
    }

    [ContextMenu("Refresh Ambience Audio Sources")]
    public void EnsureAudioSources()
    {
        calmSource = EnsureSource(calmSource, "Calm Wind", calmWind);
        forestSource = EnsureSource(forestSource, "Forest Wind", forestWind);
        seaSource = EnsureSource(seaSource, "Sea", sea);
    }

    private AudioSource EnsureSource(AudioSource source, string childName, AudioClip clip)
    {
        if (source == null)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                GameObject childObject = new GameObject(childName);
                childObject.transform.SetParent(transform, false);
                child = childObject.transform;
            }

            source = child.GetComponent<AudioSource>();
            if (source == null)
            {
                source = child.gameObject.AddComponent<AudioSource>();
            }
        }

        source.clip = clip;
        source.outputAudioMixerGroup = ambienceMixerGroup;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.volume = 0f;
        return source;
    }

    private void OnValidate()
    {
        crossfadeSeconds = Mathf.Max(0.05f, crossfadeSeconds);
        maximumCombinedOverlay = Mathf.Max(0.5f, maximumCombinedOverlay);

        ApplySettings(calmSource, calmWind);
        ApplySettings(forestSource, forestWind);
        ApplySettings(seaSource, sea);
    }

    private void ApplySettings(AudioSource source, AudioClip clip)
    {
        if (source == null)
        {
            return;
        }

        source.clip = clip;
        source.outputAudioMixerGroup = ambienceMixerGroup;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
    }
}
