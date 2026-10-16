using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PrologueController : MonoBehaviour
{
    public static bool IsGameplayBlocked { get; private set; }

    [Serializable]
    private struct Message
    {
        public bool fromPlayer;
        [TextArea(2, 4)] public string text;
    }

    [Header("Story")]
    [SerializeField] private string gameSceneName = "gamebackup";
    [SerializeField] private Message[] messages =
    {
        new Message { text = "hey. you still coming?" },
        new Message { fromPlayer = true, text = "yeah. almost there." },
        new Message { text = "thought you needed a break from the city." },
        new Message { fromPlayer = true, text = "i do." },
        new Message { text = "then take the long way in. the air's better out here." },
        new Message { fromPlayer = true, text = "see you soon." }
    };

    [Header("Scene UI")]
    [SerializeField] private CanvasGroup conversationGroup;
    [SerializeField] private ScrollRect messageScroll;
    [SerializeField] private RectTransform messageContent;
    [SerializeField] private TMP_Text continuePrompt;
    [SerializeField] private Image blackBackground;
    [SerializeField] private Image blackCurtain;
    [SerializeField] private RawImage topFade;
    [SerializeField] private TMP_FontAsset dialogueFont;

    [Header("Pacing")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 42f;
    [SerializeField, Min(0f)] private float pauseAfterMessage = 0.6f;

    private AsyncOperation gameLoad;
    private Sprite bubbleSprite;
    private Sprite spinnerSprite;
    private Image loadingSpinner;
    private TMP_Text loadingLabel;
    private Texture2D fadeTexture;
    private AudioSource typingSource;
    private AudioClip typingClip;
    private float nextBlipTime;
    private bool enterHintShown;
    private int nextMessage;
    private bool canAdvance;
    private bool finishing;
    private Scene prologueScene;
    private float previousTimeScale;
    private bool previousAudioPause;
    private bool gameplayPaused;

    private void Start()
    {
        if (conversationGroup == null || messageScroll == null || messageContent == null ||
            continuePrompt == null || blackBackground == null || blackCurtain == null || topFade == null)
        {
            Debug.LogError("The Prologue scene is missing a UI reference.", this);
            enabled = false;
            return;
        }

        prologueScene = gameObject.scene;
        DontDestroyOnLoad(gameObject);
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            CreateLoadingSpinner(canvas.transform);
        }
        conversationGroup.alpha = 0f;
        blackBackground.color = Color.black;
        blackCurtain.color = new Color(0f, 0f, 0f, 0f);
        blackCurtain.raycastTarget = false;
        continuePrompt.text = string.Empty;
        messageScroll.vertical = false;
        messageScroll.horizontal = false;
        messageScroll.scrollSensitivity = 0f;
        BuildFadeTexture();
        typingSource = gameObject.AddComponent<AudioSource>();
        typingSource.playOnAwake = false;
        typingSource.spatialBlend = 0f;
        typingSource.ignoreListenerPause = true;
        typingClip = CreateTypingClip();

        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("Assign the game scene name on Prologue Controller.", this);
            continuePrompt.text = "GAME SCENE NOT SET";
            return;
        }

        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        gameplayPaused = true;
        IsGameplayBlocked = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        SceneManager.sceneLoaded += OnGameSceneLoaded;
        gameLoad = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Additive);
        if (gameLoad == null)
        {
            RestoreGameplay();
            continuePrompt.text = "COULD NOT LOAD GAME";
            return;
        }
        StartCoroutine(PlayPrologue());
    }

    private void Update()
    {
        if (loadingSpinner != null)
            loadingSpinner.rectTransform.Rotate(0f, 0f, -180f * Time.unscaledDeltaTime);
        UpdateLoadingLabel();

        if (!canAdvance || finishing || !Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter))
            return;

        canAdvance = false;
        continuePrompt.text = string.Empty;
        enterHintShown = true;
        if (nextMessage < messages.Length)
            StartCoroutine(ShowNextMessage());
        else
            StartCoroutine(FinishPrologue());
    }

    private IEnumerator PlayPrologue()
    {
        while (!gameLoad.isDone || !TerrainStreamLoader.AllTerrainReady)
            yield return null;

        yield return FadeGroup(0f, 1f, 0.7f);
        if (messages.Length > 0)
            yield return ShowNextMessage();
        else
            SetReadyPrompt();
    }

    private IEnumerator ShowNextMessage()
    {
        Message message = messages[nextMessage++];
        TextMeshProUGUI text = CreateMessageBubble(message);
        string fullText = message.text ?? string.Empty;
        text.text = fullText;
        text.maxVisibleCharacters = 0;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(messageContent);
        float startScroll = messageScroll.verticalNormalizedPosition;
        float elapsed = 0f;
        while (elapsed < 0.38f)
        {
            elapsed += Time.unscaledDeltaTime;
            messageScroll.verticalNormalizedPosition = Mathf.Lerp(startScroll, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.38f)));
            yield return null;
        }
        messageScroll.verticalNormalizedPosition = 0f;

        float revealTime = Mathf.Max(0.18f, fullText.Length / charactersPerSecond);
        elapsed = 0f;
        int visibleCharacters = 0;
        while (elapsed < revealTime)
        {
            elapsed += Time.unscaledDeltaTime;
            int target = Mathf.Min(fullText.Length, Mathf.FloorToInt(fullText.Length * elapsed / revealTime));
            for (int index = visibleCharacters; index < target; index++)
                PlayTypingBlip(fullText, index, message.fromPlayer);
            visibleCharacters = target;
            text.maxVisibleCharacters = target;
            yield return null;
        }
        for (int index = visibleCharacters; index < fullText.Length; index++)
            PlayTypingBlip(fullText, index, message.fromPlayer);
        text.maxVisibleCharacters = int.MaxValue;
        yield return new WaitForSecondsRealtime(pauseAfterMessage);
        SetReadyPrompt();
    }

    private void SetReadyPrompt()
    {
        continuePrompt.text = enterHintShown ? string.Empty : "PRESS ENTER TO CONTINUE";
        canAdvance = true;
    }

    private IEnumerator FinishPrologue()
    {
        finishing = true;
        continuePrompt.text = string.Empty;
        while (gameLoad != null && (!gameLoad.isDone || !TerrainStreamLoader.InitialTilesReady || !TerrainStreamLoader.AllTerrainReady))
            yield return null;

        yield return FadeCurtain(0f, 1f, 0.75f);
        if (gameLoad == null) yield break;
        Scene gameScene = SceneManager.GetSceneByName(gameSceneName);
        if (!gameScene.IsValid() || !gameScene.isLoaded)
        {
            Debug.LogError($"The gameplay scene '{gameSceneName}' did not finish loading.", this);
            RestoreGameplay();
            yield break;
        }
        SceneManager.SetActiveScene(gameScene);
        yield return RevealGame();
    }

    private void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != gameSceneName) return;
        SceneManager.sceneLoaded -= OnGameSceneLoaded;
        // Keep the prologue's overlay visible while the gameplay camera takes over.
        if (prologueScene.IsValid() && prologueScene.isLoaded)
            foreach (GameObject root in prologueScene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
    }

    private IEnumerator RevealGame()
    {
        conversationGroup.alpha = 0f;
        blackBackground.color = new Color(0f, 0f, 0f, 0f);
        yield return FadeCurtain(1f, 0f, 0.85f);
        RestoreGameplay();
        if (prologueScene.IsValid() && prologueScene.isLoaded)
            SceneManager.UnloadSceneAsync(prologueScene);
        Destroy(gameObject);
    }

    private void RestoreGameplay()
    {
        if (!gameplayPaused) return;
        gameplayPaused = false;
        IsGameplayBlocked = false;
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
    }

    private IEnumerator FadeGroup(float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            conversationGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        conversationGroup.alpha = to;
    }

    private IEnumerator FadeCurtain(float from, float to, float duration)
    {
        blackCurtain.raycastTarget = true;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            blackCurtain.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        blackCurtain.color = new Color(0f, 0f, 0f, to);
    }

    private TextMeshProUGUI CreateMessageBubble(Message message)
    {
        GameObject rowObject = new GameObject(message.fromPlayer ? "Player Message" : "Incoming Message", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.SetParent(messageContent, false);
        LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
        rowElement.preferredWidth = messageContent.rect.width;
        rowElement.flexibleWidth = 1f;
        HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = message.fromPlayer ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        rowLayout.padding = new RectOffset(20, 20, 0, 0);
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        GameObject bubbleObject = new GameObject("Speech Bubble", typeof(RectTransform), typeof(Image), typeof(Shadow), typeof(LayoutElement));
        RectTransform bubble = bubbleObject.GetComponent<RectTransform>();
        bubble.SetParent(row, false);
        Image background = bubbleObject.GetComponent<Image>();
        background.sprite = GetBubbleSprite();
        background.type = Image.Type.Sliced;
        background.color = message.fromPlayer ? new Color32(13, 133, 231, 250) : new Color(.07f, .09f, .13f, .98f);
        background.raycastTarget = false;
        Shadow shadow = bubbleObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
        shadow.effectDistance = new Vector2(2f, -3f);

        GameObject textObject = new GameObject("Message Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(bubble, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(30f, 15f);
        textRect.offsetMax = new Vector2(-30f, -15f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = dialogueFont != null ? dialogueFont : TMP_Settings.defaultFontAsset;
        text.fontSharedMaterial = text.font.material;
        text.fontSize = 36f;
        text.color = message.fromPlayer ? Color.white : new Color(.96f, .97f, 1f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        Vector2 preferred = text.GetPreferredValues(message.text ?? string.Empty, 660f, 0f);
        float width = Mathf.Clamp(preferred.x + 62f, 150f, 720f);
        float height = Mathf.Max(68f, preferred.y + 34f);
        LayoutElement bubbleLayout = bubbleObject.GetComponent<LayoutElement>();
        bubbleLayout.preferredWidth = width;
        bubbleLayout.preferredHeight = height;
        rowElement.preferredHeight = height + 18f;
        return text;
    }

    private Sprite GetBubbleSprite()
    {
        if (bubbleSprite != null) return bubbleSprite;
        const int size = 64;
        const int radius = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "Prologue Rounded Bubble"
        };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float horizontal = x < radius ? radius - .5f - x : x - (size - radius - .5f);
            float vertical = y < radius ? radius - .5f - y : y - (size - radius - .5f);
            float distance = Mathf.Sqrt(Mathf.Pow(Mathf.Max(0f, horizontal), 2f) + Mathf.Pow(Mathf.Max(0f, vertical), 2f));
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius + .5f - distance)));
        }
        texture.Apply();
        bubbleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return bubbleSprite;
    }

    private void CreateLoadingSpinner(Transform parent)
    {
        const int size = 64;
        const float outerRadius = 25f;
        const float innerRadius = 19f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "Prologue Loading Spinner"
        };

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - (size - 1) * .5f;
            float dy = y - (size - 1) * .5f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            bool onArc = angle >= 35f && angle <= 315f;
            float edge = Mathf.Min(radius - innerRadius, outerRadius - radius);
            float alpha = onArc ? Mathf.Clamp01(edge + .5f) : 0f;
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * .9f));
        }
        texture.Apply();
        spinnerSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);

        GameObject spinnerObject = new GameObject("Prologue Loading Spinner", typeof(RectTransform), typeof(Image));
        RectTransform rect = spinnerObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(-42f, 42f);
        rect.sizeDelta = new Vector2(46f, 46f);
        loadingSpinner = spinnerObject.GetComponent<Image>();
        loadingSpinner.sprite = spinnerSprite;
        loadingSpinner.raycastTarget = false;

        GameObject labelObject = new GameObject("Prologue Loading Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(parent, false);
        labelRect.anchorMin = new Vector2(1f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(1f, .5f);
        labelRect.anchoredPosition = new Vector2(-72f, 42f);
        labelRect.sizeDelta = new Vector2(180f, 28f);
        loadingLabel = labelObject.GetComponent<TextMeshProUGUI>();
        loadingLabel.font = dialogueFont != null ? dialogueFont : TMP_Settings.defaultFontAsset;
        loadingLabel.fontSize = 17f;
        loadingLabel.alignment = TextAlignmentOptions.MidlineRight;
        loadingLabel.color = new Color(1f, 1f, 1f, .8f);
        loadingLabel.raycastTarget = false;
        UpdateLoadingLabel();
    }

    private void UpdateLoadingLabel()
    {
        if (loadingLabel == null) return;

        if (gameLoad == null || !gameLoad.isDone)
        {
            int progress = gameLoad == null ? 0 : Mathf.RoundToInt(gameLoad.progress / .9f * 100f);
            loadingLabel.text = $"LOADING GAME WORLD {Mathf.Clamp(progress, 0, 100)}%";
        }
        else if (!TerrainStreamLoader.InitialTilesReady)
        {
            loadingLabel.text = string.IsNullOrEmpty(TerrainStreamLoader.CurrentLoadStatus)
                ? $"LOADING TERRAIN {TerrainStreamLoader.InitialTilesLoadedCount}/{TerrainStreamLoader.InitialTileTargetCount}"
                : TerrainStreamLoader.CurrentLoadStatus;
        }
        else
        {
            loadingLabel.text = string.IsNullOrEmpty(TerrainStreamLoader.CurrentLoadStatus)
                ? "LOADED TERRAIN!"
                : TerrainStreamLoader.CurrentLoadStatus;
        }
    }

    private void BuildFadeTexture()
    {
        fadeTexture = new Texture2D(1, 128, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < fadeTexture.height; y++)
        {
            float opacity = y / (float)(fadeTexture.height - 1);
            fadeTexture.SetPixel(0, y, new Color(0f, 0f, 0f, opacity));
        }
        fadeTexture.Apply();
        topFade.texture = fadeTexture;
        topFade.raycastTarget = false;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnGameSceneLoaded;
        RestoreGameplay();
        if (typingClip != null) Destroy(typingClip);
        if (bubbleSprite != null) Destroy(bubbleSprite.texture);
        if (bubbleSprite != null) Destroy(bubbleSprite);
        if (spinnerSprite != null) Destroy(spinnerSprite.texture);
        if (spinnerSprite != null) Destroy(spinnerSprite);
        if (fadeTexture != null) Destroy(fadeTexture);
    }

    private void PlayTypingBlip(string line, int index, bool fromPlayer)
    {
        if (char.IsWhiteSpace(line[index]) || Time.unscaledTime < nextBlipTime || typingSource == null) return;
        nextBlipTime = Time.unscaledTime + (fromPlayer ? .035f : .045f);
        typingSource.pitch = fromPlayer ? UnityEngine.Random.Range(.92f, 1.12f) : UnityEngine.Random.Range(.72f, .88f);
        typingSource.PlayOneShot(typingClip, fromPlayer ? UnityEngine.Random.Range(.36f, .48f) : UnityEngine.Random.Range(.32f, .42f));
    }

    // Same tone and envelope as DeliverySystem's built-in dialogue blip.
    private static AudioClip CreateTypingClip()
    {
        const int sampleRate = 44100;
        const float duration = .055f;
        int count = Mathf.CeilToInt(duration * sampleRate);
        float[] samples = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float progress = i / (float)count;
            phase += 520f / sampleRate;
            float envelope = Mathf.Sin(progress * Mathf.PI) * (1f - progress * .25f);
            samples[i] = Mathf.Sign(Mathf.Sin(phase * Mathf.PI * 2f)) * .08f * envelope;
        }
        AudioClip clip = AudioClip.Create("Prologue Dialogue Blip", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
