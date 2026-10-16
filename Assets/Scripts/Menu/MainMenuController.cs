using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public sealed class MainMenuController : MonoBehaviour
{
    // Set by the credits before they load this scene. It is intentionally consumed in
    // Awake so opening the menu normally still plays the studio intro.
    public static bool SkipToDoroIntroOnce { get; private set; }
    private const string MusicVolumeKey = "MainMenuMusicVolume";
    private const string FullscreenKey = "MainMenuFullscreen";
    private const float ToDoroIntroDuration = 3f;
    private const float ToDoroIntroFadeDuration = 1.5f;
    private const float ToDoroIntroFramesPerSecond = 3.5f;

    [Header("Scene")]
    [SerializeField] private string prologueSceneName = "Prologue";
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject optionsPanel;

    [Header("Opening Intro")]
    [Tooltip("Shows the To Doro logo animation after Unity's built-in splash screen, before the menu fades in.")]
    [SerializeField] private bool showToDoroIntro = true;
    [SerializeField] private Texture2D toDoroFrameA;
    [SerializeField] private Texture2D toDoroFrameB;

    [Header("Menu Music")]
    [Tooltip("Drop a music clip here. It plays and loops while the menu is open.")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioSource musicSource;
    [SerializeField, Min(0f)] private float musicLoopFadeInDuration = 1.5f;
    [Tooltip("Fades the track down before it restarts, so the loop point is not abrupt.")]
    [SerializeField, Min(0f)] private float musicLoopFadeOutDuration = 2f;

    [Header("Button Sounds")]
    [Range(0f, 1f)] [SerializeField] private float buttonClickVolume = 0.35f;

    private AudioSource buttonSoundSource;
    private AudioClip buttonClickClip;
    private bool isStartingGame;
    private float musicVolume;
    private float musicFadeMultiplier = 1f;
    private Coroutine menuMusicCoroutine;

    [Header("Options")]
    [SerializeField] private UnityEngine.UI.Slider musicVolumeSlider;
    [SerializeField] private UnityEngine.UI.Toggle fullscreenToggle;
    [SerializeField] private TMP_Text volumeValueText;

    private void Awake()
    {
        bool skipToDoroIntro = SkipToDoroIntroOnce;
        SkipToDoroIntroOnce = false;
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 0.7f));
        musicVolume = volume;
        bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;

        if (musicSource != null)
        {
            musicSource.playOnAwake = false;
            musicSource.loop = false;
            musicSource.clip = menuMusic;
            ApplyMusicVolume();
            if (menuMusic != null) menuMusicCoroutine = StartCoroutine(PlayMenuMusicWithLoopFade());
        }

        SetupButtonSounds();

        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(volume);
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
        UpdateVolumeLabel(volume);
        Screen.fullScreen = fullscreen;
        ShowMain();
        if (skipToDoroIntro) StartCoroutine(FadeMenuIn());
        else if (showToDoroIntro) StartCoroutine(PlayToDoroIntro());
    }

    /// <summary>Returns from the finale without replaying the opening logo animation.</summary>
    public static void ReturnFromCredits(string mainMenuSceneName = "MainMenu")
    {
        SkipToDoroIntroOnce = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void Play()
    {
        if (isStartingGame) return;
        if (string.IsNullOrEmpty(prologueSceneName))
        {
            Debug.LogError("Assign a prologue scene name on the Main Menu Controller.", this);
            return;
        }

        isStartingGame = true;
        if (menuMusicCoroutine != null)
        {
            StopCoroutine(menuMusicCoroutine);
            menuMusicCoroutine = null;
        }
        StartCoroutine(FadeToPrologue());
    }

    private IEnumerator FadeToPrologue()
    {
        GameObject fadeObject = new GameObject("Menu Fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Image));
        Canvas fadeCanvas = fadeObject.GetComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.overrideSorting = true;
        fadeCanvas.sortingOrder = 1000;
        CanvasScaler scaler = fadeObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        RectTransform rect = fadeObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = fadeObject.GetComponent<Image>();
        image.raycastTarget = true;

        const float duration = 0.65f;
        float startingMusicVolume = musicSource != null ? musicSource.volume : 0f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            image.color = new Color(0f, 0f, 0f, progress);
            if (musicSource != null) musicSource.volume = Mathf.Lerp(startingMusicVolume, 0f, progress);
            yield return null;
        }

        image.color = Color.black;
        if (musicSource != null)
        {
            musicSource.volume = 0f;
            musicSource.Stop();
        }
        SceneManager.LoadScene(prologueSceneName);
    }

    private IEnumerator FadeMenuIn()
    {
        GameObject fadeObject = new GameObject("Menu Return Fade", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(Image));
        Canvas canvas = fadeObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2000;
        CanvasScaler scaler = fadeObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        RectTransform rect = fadeObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = fadeObject.GetComponent<Image>();
        image.raycastTarget = true;

        const float duration = 0.8f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            image.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        Destroy(fadeObject);
    }

    private IEnumerator PlayToDoroIntro()
    {
        if (toDoroFrameA == null || toDoroFrameB == null)
        {
            Debug.LogWarning("Assign both To Doro intro frames on the Main Menu Controller.", this);
            yield break;
        }

        Texture2D[] frames = { toDoroFrameA, toDoroFrameB };

        GameObject introObject = new GameObject("To Doro Intro", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas introCanvas = introObject.GetComponent<Canvas>();
        introCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        introCanvas.overrideSorting = true;
        introCanvas.sortingOrder = 2000;
        CanvasScaler scaler = introObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        CanvasGroup introGroup = introObject.GetComponent<CanvasGroup>();
        introGroup.blocksRaycasts = true;
        introGroup.interactable = false;
        GameObject artworkObject = new GameObject("Full Screen Artwork", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(RawImage));
        artworkObject.transform.SetParent(introObject.transform, false);
        RawImage artwork = artworkObject.GetComponent<RawImage>();
        RectTransform artworkRect = artwork.rectTransform;
        artworkRect.anchorMin = Vector2.zero;
        artworkRect.anchorMax = Vector2.one;
        artworkRect.offsetMin = Vector2.zero;
        artworkRect.offsetMax = Vector2.zero;
        artwork.raycastTarget = true;
        // The artwork already contains opaque black. Draw it as one full-screen
        // graphic so its black and logo pixels share exactly the same fade.
        float screenAspect = (float)Screen.width / Screen.height;
        float artworkAspect = (float)frames[0].width / frames[0].height;
        if (screenAspect >= artworkAspect)
        {
            float uvWidth = screenAspect / artworkAspect;
            artwork.uvRect = new Rect((1f - uvWidth) * 0.5f, 0f, uvWidth, 1f);
        }
        else
        {
            float uvHeight = artworkAspect / screenAspect;
            artwork.uvRect = new Rect(0f, (1f - uvHeight) * 0.5f, 1f, uvHeight);
        }
        foreach (Texture2D frame in frames) frame.wrapMode = TextureWrapMode.Clamp;
        artwork.texture = frames[0];

        // Begin after the first render and count displayed frames. The Editor can stall
        // for seconds while entering Play mode; that stall must not use up the intro.
        yield return new WaitForEndOfFrame();
        float introElapsed = 0f;
        int currentFrame = 0;
        const float maxFrameDelta = 0.1f;
        while (introElapsed < ToDoroIntroDuration - ToDoroIntroFadeDuration)
        {
            if (IsIntroSkipPressed())
            {
                Destroy(introObject);
                yield break;
            }
            int nextFrame = Mathf.FloorToInt(introElapsed * ToDoroIntroFramesPerSecond) % frames.Length;
            if (nextFrame != currentFrame)
            {
                artwork.texture = frames[nextFrame];
                currentFrame = nextFrame;
            }
            yield return null;
            introElapsed += Mathf.Min(Time.unscaledDeltaTime, maxFrameDelta);
        }

        float fadeElapsed = 0f;
        while (fadeElapsed < ToDoroIntroFadeDuration)
        {
            if (IsIntroSkipPressed())
            {
                Destroy(introObject);
                yield break;
            }
            introGroup.alpha = 1f - Mathf.Clamp01(fadeElapsed / ToDoroIntroFadeDuration);
            yield return null;
            fadeElapsed += Mathf.Min(Time.unscaledDeltaTime, maxFrameDelta);
        }

        Destroy(introObject);
    }

    private static bool IsIntroSkipPressed()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus) ||
            (Input.GetKeyDown(KeyCode.Equals) &&
             (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)));
#else
        return false;
#endif
    }

    public void ShowMain()
    {
        SetPanels(true, false, false);
    }

    public void ShowControls()
    {
        SetPanels(false, true, false);
    }

    public void ShowOptions()
    {
        SetPanels(false, false, true);
    }

    public void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        musicVolume = volume;
        ApplyMusicVolume();
        UpdateVolumeLabel(volume);
        PlayerPrefs.SetFloat(MusicVolumeKey, volume);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetPanels(bool showMain, bool showControls, bool showOptions)
    {
        if (mainPanel != null) mainPanel.SetActive(showMain);
        if (controlsPanel != null) controlsPanel.SetActive(showControls);
        if (optionsPanel != null) optionsPanel.SetActive(showOptions);
    }

    private void UpdateVolumeLabel(float volume)
    {
        if (volumeValueText != null) volumeValueText.text = Mathf.RoundToInt(volume * 100f) + "%";
    }

    private IEnumerator PlayMenuMusicWithLoopFade()
    {
        while (musicSource != null && menuMusic != null)
        {
            // Keep both fades inside the clip, even if a short replacement track is used.
            float requestedFades = musicLoopFadeInDuration + musicLoopFadeOutDuration;
            float fadeScale = requestedFades > menuMusic.length && requestedFades > 0f
                ? menuMusic.length / requestedFades
                : 1f;
            float fadeInDuration = musicLoopFadeInDuration * fadeScale;
            float fadeOutDuration = musicLoopFadeOutDuration * fadeScale;
            float fadeOutStart = Mathf.Max(fadeInDuration, menuMusic.length - fadeOutDuration);

            musicFadeMultiplier = fadeInDuration > 0f ? 0f : 1f;
            ApplyMusicVolume();
            musicSource.Play();

            float elapsed = 0f;
            while (elapsed < menuMusic.length && musicSource.isPlaying)
            {
                elapsed += Time.unscaledDeltaTime;
                if (fadeInDuration > 0f && elapsed < fadeInDuration)
                    musicFadeMultiplier = Mathf.SmoothStep(0f, 1f, elapsed / fadeInDuration);
                else if (fadeOutDuration > 0f && elapsed >= fadeOutStart)
                    musicFadeMultiplier = 1f - Mathf.SmoothStep(0f, 1f, (elapsed - fadeOutStart) / fadeOutDuration);
                else
                    musicFadeMultiplier = 1f;
                ApplyMusicVolume();
                yield return null;
            }

            musicSource.Stop();
            musicFadeMultiplier = 0f;
            ApplyMusicVolume();
        }
    }

    private void ApplyMusicVolume()
    {
        if (musicSource != null) musicSource.volume = musicVolume * musicFadeMultiplier;
    }

    private void SetupButtonSounds()
    {
        buttonSoundSource = gameObject.AddComponent<AudioSource>();
        buttonSoundSource.playOnAwake = false;
        buttonSoundSource.spatialBlend = 0f;
        buttonClickClip = CreateClickClip();

        AddButtonSounds(mainPanel);
        AddButtonSounds(controlsPanel);
        AddButtonSounds(optionsPanel);
    }

    private void AddButtonSounds(GameObject panel)
    {
        if (panel == null) return;

        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            button.onClick.AddListener(PlayButtonClick);
        }
    }

    private void PlayButtonClick()
    {
        if (buttonSoundSource != null && buttonClickClip != null)
        {
            buttonSoundSource.PlayOneShot(buttonClickClip, buttonClickVolume);
        }
    }

    private static AudioClip CreateClickClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.075f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float envelope = Mathf.Exp(-time * 55f);
            float tone = Mathf.Sin(2f * Mathf.PI * 760f * time);
            float tick = Mathf.Sin(2f * Mathf.PI * 1520f * time) * 0.25f;
            samples[i] = (tone + tick) * envelope * 0.3f;
        }

        AudioClip clip = AudioClip.Create("Menu Button Click", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
