using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates the in-game equivalent of the main menu and toggles it with Escape.</summary>
public sealed class PauseMenuController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string MasterVolumeKey = "PauseMasterVolume";
    private const string FullscreenKey = "MainMenuFullscreen";

    private GameObject menuRoot;
    private GameObject mainPanel;
    private GameObject controlsPanel;
    private GameObject optionsPanel;
    private bool isPaused;
    private float previousTimeScale;
    private bool previousAudioPause;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;

    private void Update()
    {
        if (PrologueController.IsGameplayBlocked || !Input.GetKeyDown(KeyCode.Escape)) return;

        if (isPaused && controlsPanel != null && controlsPanel.activeSelf)
        {
            ShowMain();
            return;
        }

        if (isPaused && optionsPanel != null && optionsPanel.activeSelf)
        {
            ShowMain();
            return;
        }

        if (isPaused) Resume();
        else Pause();
    }

    private void Pause()
    {
        BuildMenuIfNeeded();
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        isPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        menuRoot.SetActive(true);
        ShowMain();
    }

    public void Resume()
    {
        if (!isPaused) return;
        isPaused = false;
        if (menuRoot != null) menuRoot.SetActive(false);
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
    }

    private void ShowMain()
    {
        mainPanel.SetActive(true);
        controlsPanel.SetActive(false);
        optionsPanel.SetActive(false);
    }

    private void ShowControls()
    {
        mainPanel.SetActive(false);
        controlsPanel.SetActive(true);
        optionsPanel.SetActive(false);
    }

    private void ShowOptions()
    {
        mainPanel.SetActive(false);
        controlsPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BuildMenuIfNeeded()
    {
        if (menuRoot != null) return;

        menuRoot = new GameObject("Pause Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = menuRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1500;
        CanvasScaler scaler = menuRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image shade = CreateImage("Dim Background", menuRoot.transform, new Color(.018f, .024f, .038f, .7f));
        Stretch(shade.rectTransform);
        shade.raycastTarget = true;

        mainPanel = CreatePanel("Pause Main Panel");
        CreateTitle(mainPanel.transform, "PAUSED");
        CreateButton(mainPanel.transform, "RESUME", 120f, Resume);
        CreateButton(mainPanel.transform, "CONTROLS", 35f, ShowControls);
        CreateButton(mainPanel.transform, "OPTIONS", -50f, ShowOptions);
        CreateButton(mainPanel.transform, "MAIN MENU", -135f, ReturnToMainMenu);
        CreateButton(mainPanel.transform, "QUIT", -220f, Quit);

        controlsPanel = CreatePanel("Pause Controls Panel");
        CreateTitle(controlsPanel.transform, "CONTROLS");
        CreateControlsLayout(controlsPanel.transform);
        CreateButton(controlsPanel.transform, "BACK", -285f, ShowMain);

        optionsPanel = CreatePanel("Pause Options Panel");
        CreateTitle(optionsPanel.transform, "OPTIONS");
        CreateLabel(optionsPanel.transform, "MASTER VOLUME", 25f, new Vector2(-170f, 80f), new Vector2(340f, 40f));
        Slider volume = CreateSlider(optionsPanel.transform, new Vector2(150f, 80f));
        float savedVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, AudioListener.volume));
        volume.SetValueWithoutNotify(savedVolume);
        AudioListener.volume = savedVolume;
        volume.onValueChanged.AddListener(SetMasterVolume);
        Toggle fullscreen = CreateToggle(optionsPanel.transform, new Vector2(-150f, -15f));
        fullscreen.SetIsOnWithoutNotify(PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0);
        fullscreen.onValueChanged.AddListener(SetFullscreen);
        CreateLabel(optionsPanel.transform, "FULLSCREEN", 25f, new Vector2(30f, -15f), new Vector2(260f, 40f));
        CreateButton(optionsPanel.transform, "BACK", -220f, ShowMain);

        if (EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("Pause Menu EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        menuRoot.SetActive(false);
    }

    private GameObject CreatePanel(string name)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(menuRoot.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(860f, 720f);
        panel.GetComponent<Image>().color = new Color(.045f, .07f, .11f, .76f);
        return panel;
    }

    private static void CreateTitle(Transform parent, string title)
    {
        CreateLabel(parent, title, 58f, new Vector2(0f, 265f), new Vector2(700f, 90f), FontStyles.Bold);
    }

    private static void CreateLabel(Transform parent, string text, float size, Vector2 position, Vector2 dimensions,
        FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject label = new GameObject(text, typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(parent, false);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }

    private static void CreateControlsLayout(Transform parent)
    {
        const string controls =
            "<b>DRIVING</b>\n\n" +
            "WASD / ARROWS  —  DRIVE & STEER\n" +
            "SHIFT  —  HANDBRAKE\n\n" +
            "<b>CAMERA</b>\n\n" +
            "MOUSE MOVEMENT  —  MOVE CAMERA\n\n" +
            "<b>GAMEPLAY</b>\n\n" +
            "ENTER  —  ADVANCE DIALOGUE\n" +
            "R  —  RESTART DELIVERY\n" +
            "ESC  —  PAUSE / RESUME";
        CreateLabel(parent, controls, 22f, new Vector2(0f, 15f), new Vector2(820f, 430f),
            FontStyles.Normal, TextAlignmentOptions.Center);
    }

    private static Button CreateButton(Transform parent, string text, float y, UnityEngine.Events.UnityAction action)
    {
        // Match the main-menu buttons: the Image starts white and the Button's
        // normal state fades it transparent; hover/selection supplies the blue.
        Image image = CreateImage(text + " Button", parent, Color.white);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(430f, 64f);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(.14509805f, .6509804f, 1f, 1f);
        colors.pressedColor = new Color(.14509805f, .6509804f, 1f, 1f);
        colors.selectedColor = new Color(.14509805f, .6509804f, 1f, 1f);
        colors.fadeDuration = .12f;
        button.colors = colors;
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        CreateLabel(image.transform, text, 27f, Vector2.zero, rect.sizeDelta, FontStyles.Bold);
        return button;
    }

    private static Slider CreateSlider(Transform parent, Vector2 position)
    {
        GameObject sliderObject = new GameObject("Volume Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);
        RectTransform rect = sliderObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(320f, 28f);
        Image background = CreateImage("Background", sliderObject.transform, new Color(.08f, .16f, .23f, 1f));
        Stretch(background.rectTransform);
        Image fill = CreateImage("Fill", sliderObject.transform, new Color(.15f, .65f, 1f, 1f));
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(5f, 5f);
        fillRect.offsetMax = new Vector2(-5f, -5f);
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        return slider;
    }

    private static Toggle CreateToggle(Transform parent, Vector2 position)
    {
        GameObject toggleObject = new GameObject("Fullscreen Toggle", typeof(RectTransform), typeof(Toggle));
        toggleObject.transform.SetParent(parent, false);
        RectTransform rect = toggleObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(38f, 38f);
        Image background = CreateImage("Background", toggleObject.transform, new Color(.08f, .16f, .23f, 1f));
        Stretch(background.rectTransform);
        Image checkmark = CreateImage("Checkmark", toggleObject.transform, new Color(.15f, .65f, 1f, 1f));
        Stretch(checkmark.rectTransform);
        checkmark.rectTransform.offsetMin = new Vector2(8f, 8f);
        checkmark.rectTransform.offsetMax = new Vector2(-8f, -8f);
        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        return toggle;
    }

    private void SetMasterVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(MasterVolumeKey, volume);
        PlayerPrefs.Save();
    }

    private static void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
