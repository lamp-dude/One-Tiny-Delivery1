using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Plays the Finale's quiet, card-by-card end-credit sequence.</summary>
public sealed class EndCreditsSequence : MonoBehaviour
{
    [System.Serializable]
    private struct EpilogueLine
    {
        public bool fromPlayer;
        [TextArea(2, 4)] public string text;
    }

    [SerializeField] private Texture2D[] creditImages;
    [SerializeField] private Texture2D logoImage;
    [SerializeField, Min(0.1f)] private float cardSeconds = 8f;
    [SerializeField, Min(0.05f)] private float fadeSeconds = 2.5f;
    [SerializeField, Min(0.05f)] private float returnFadeSeconds = 2.5f;
    [SerializeField] private Vector2 creditImageFrameSize = new Vector2(500f, 220f);

    [Header("Automatic Epilogue")]
    [SerializeField] private EpilogueLine[] epilogueLines =
    {
        new EpilogueLine { text = "you made it home." },
        new EpilogueLine { fromPlayer = true, text = "i did." },
        new EpilogueLine { text = "does it feel any different?" },
        new EpilogueLine { fromPlayer = true, text = "yeah. better." },
        new EpilogueLine { text = "good. stay awhile." }
    };
    [SerializeField, Min(1f)] private float epilogueCharactersPerSecond = 38f;
    [SerializeField, Min(0f)] private float epiloguePauseAfterLine = 1.6f;
    [SerializeField] private TMP_FontAsset dialogueFont;

    private CanvasGroup cardGroup;
    private TextMeshProUGUI creditText;
    private UnityEngine.UI.Image creditImage;
    private RectTransform creditTextRect;
    private CanvasGroup epilogueGroup;
    private RectTransform epilogueContent;
    private ScrollRect epilogueScroll;
    private Sprite epilogueBubbleSprite;

    public void Configure(Texture2D[] images, Texture2D logo)
    {
        creditImages = images;
        logoImage = logo;
    }

    private void Awake()
    {
        // The Finale is a UI-only scene, so it has no gameplay camera to supply
        // Unity's required Audio Listener. Attach one here before its music cue starts.
        if (Object.FindFirstObjectByType<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();
    }

    private void Start()
    {
        CreateUi();
        StartCoroutine(PlayCredits());
    }

    private IEnumerator PlayCredits()
    {
        yield return ShowCard("THE END.", null, 3f, 84f);

        string[] credits =
        {
            "Made By\nRobbie Ma",
            "Written By\nRobbie Ma",
            "Music By\nRobbie Ma",
            "Made With\nUnity3D & Blender & Garage Band"
        };

        for (int index = 0; index < credits.Length; index++)
        {
            Texture2D image = creditImages != null && index < creditImages.Length ? creditImages[index] : null;
            yield return ShowCard(credits[index], image, cardSeconds, 56f);
        }

        yield return PlayEpilogue();
        yield return ShowCard("ONE TINY DELIVERY", logoImage, 5f, 96f);
        yield return FadeToMainMenu();
    }

    private IEnumerator ShowCard(string text, Texture2D image, float duration, float fontSize)
    {
        creditText.text = text;
        creditText.fontSize = fontSize;
        creditImage.sprite = image != null ? CreateCreditSprite(image) : null;
        creditImage.enabled = creditImage.sprite != null;
        creditText.alignment = TextAlignmentOptions.Center;
        // Leave a large fixed gap between the bounded image and the text.
        creditTextRect.anchorMin = image == null ? new Vector2(.08f, 0f) : new Vector2(.1f, .64f);
        creditTextRect.anchorMax = image == null ? new Vector2(.92f, 1f) : new Vector2(.9f, .94f);
        creditTextRect.offsetMin = Vector2.zero;
        creditTextRect.offsetMax = Vector2.zero;
        cardGroup.alpha = 0f;

        yield return Fade(0f, 1f);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration - fadeSeconds * 2f));
        yield return Fade(1f, 0f);
    }

    private IEnumerator Fade(float from, float to)
    {
        for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            cardGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeSeconds);
            yield return null;
        }
        cardGroup.alpha = to;
    }

    private IEnumerator PlayEpilogue()
    {
        if (epilogueLines == null || epilogueLines.Length == 0) yield break;

        yield return FadeCanvasGroup(epilogueGroup, 0f, 1f, fadeSeconds);
        foreach (EpilogueLine line in epilogueLines)
        {
            Canvas.ForceUpdateCanvases();
            TextMeshProUGUI text = CreateEpilogueBubble(line);
            string fullText = line.text ?? string.Empty;
            text.text = fullText;
            text.maxVisibleCharacters = 0;
            yield return ScrollEpilogueToNewestLine();

            float revealDuration = Mathf.Max(.18f, fullText.Length / epilogueCharactersPerSecond);
            for (float elapsed = 0f; elapsed < revealDuration; elapsed += Time.unscaledDeltaTime)
            {
                text.maxVisibleCharacters = Mathf.Min(fullText.Length,
                    Mathf.FloorToInt(fullText.Length * Mathf.Clamp01(elapsed / revealDuration)));
                yield return null;
            }
            text.maxVisibleCharacters = fullText.Length;
            yield return new WaitForSecondsRealtime(epiloguePauseAfterLine);
        }

        yield return FadeCanvasGroup(epilogueGroup, 1f, 0f, fadeSeconds);
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        group.alpha = to;
    }

    private IEnumerator ScrollEpilogueToNewestLine()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(epilogueContent);
        float start = epilogueScroll.verticalNormalizedPosition;
        const float scrollSeconds = .38f;
        for (float elapsed = 0f; elapsed < scrollSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / scrollSeconds));
            epilogueScroll.verticalNormalizedPosition = Mathf.Lerp(start, 0f, progress);
            yield return null;
        }
        epilogueScroll.verticalNormalizedPosition = 0f;
    }

    private IEnumerator FadeToMainMenu()
    {
        GameObject fadeObject = new GameObject("Credits Return Fade", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.Image));
        Canvas canvas = fadeObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        UnityEngine.UI.CanvasScaler scaler = fadeObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        RectTransform rect = fadeObject.GetComponent<RectTransform>();
        Stretch(rect);
        UnityEngine.UI.Image image = fadeObject.GetComponent<UnityEngine.UI.Image>();
        image.raycastTarget = true;

        OneShotMusicCue finaleMusic = Object.FindFirstObjectByType<OneShotMusicCue>();
        if (finaleMusic != null) finaleMusic.FadeOutAndStop(returnFadeSeconds);

        float duration = returnFadeSeconds;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            image.color = new Color(0f, 0f, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        image.color = Color.black;
        if (finaleMusic != null) finaleMusic.FadeOutAndStop(0f);
        MainMenuController.ReturnFromCredits();
    }

    private void CreateUi()
    {
        GameObject canvasObject = new GameObject("End Credits Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.SetParent(canvasObject.transform, false);
        Stretch(backgroundRect);
        UnityEngine.UI.Image background = backgroundObject.GetComponent<UnityEngine.UI.Image>();
        background.color = new Color(.018f, .024f, .038f, 1f);
        background.raycastTarget = false;

        GameObject cardObject = new GameObject("Credit Card", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.SetParent(canvasObject.transform, false);
        cardRect.anchorMin = new Vector2(.5f, .5f);
        cardRect.anchorMax = new Vector2(.5f, .5f);
        cardRect.pivot = new Vector2(.5f, .5f);
        cardRect.sizeDelta = new Vector2(1360f, 620f);
        cardGroup = cardObject.GetComponent<CanvasGroup>();

        GameObject textObject = new GameObject("Credit Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(cardRect, false);
        textRect.anchorMin = new Vector2(.05f, .4f);
        textRect.anchorMax = new Vector2(.95f, .96f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        creditTextRect = textRect;
        creditText = textObject.GetComponent<TextMeshProUGUI>();
        creditText.font = TMP_Settings.defaultFontAsset;
        creditText.alignment = TextAlignmentOptions.Center;
        creditText.color = Color.white;
        creditText.enableWordWrapping = false;
        creditText.raycastTarget = false;

        GameObject imageObject = new GameObject("Credit Image", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(cardRect, false);
        imageRect.anchorMin = new Vector2(.5f, .28f);
        imageRect.anchorMax = new Vector2(.5f, .28f);
        imageRect.pivot = new Vector2(.5f, .5f);
        imageRect.sizeDelta = creditImageFrameSize;
        creditImage = imageObject.GetComponent<UnityEngine.UI.Image>();
        creditImage.color = Color.white;
        creditImage.preserveAspect = true;
        creditImage.raycastTarget = false;

        CreateEpilogueUi(canvasObject.transform);
    }

    private void CreateEpilogueUi(Transform canvasTransform)
    {
        GameObject groupObject = new GameObject("Automatic Epilogue", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform groupRect = groupObject.GetComponent<RectTransform>();
        groupRect.SetParent(canvasTransform, false);
        Stretch(groupRect);
        epilogueGroup = groupObject.GetComponent<CanvasGroup>();
        epilogueGroup.alpha = 0f;
        epilogueGroup.blocksRaycasts = false;

        GameObject scrollObject = new GameObject("Message Scroll View", typeof(RectTransform), typeof(ScrollRect));
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.SetParent(groupRect, false);
        scrollRect.anchorMin = new Vector2(.06f, .08f);
        scrollRect.anchorMax = new Vector2(.94f, .94f);
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;
        epilogueScroll = scrollObject.GetComponent<ScrollRect>();
        epilogueScroll.horizontal = false;
        epilogueScroll.vertical = false;
        epilogueScroll.movementType = ScrollRect.MovementType.Clamped;
        epilogueScroll.inertia = false;
        epilogueScroll.scrollSensitivity = 0f;

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.Mask));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(scrollRect, false);
        Stretch(viewport);
        UnityEngine.UI.Image viewportImage = viewportObject.GetComponent<UnityEngine.UI.Image>();
        // A Mask needs an opaque graphic to write the stencil. The graphic itself
        // stays hidden; a transparent Image would clip every dialogue bubble.
        viewportImage.color = Color.white;
        viewportImage.raycastTarget = false;
        viewportObject.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject("Message Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        epilogueContent = contentObject.GetComponent<RectTransform>();
        epilogueContent.SetParent(viewport, false);
        // The content begins at the top and the viewport clips older messages as it scrolls.
        epilogueContent.anchorMin = new Vector2(0f, 1f);
        epilogueContent.anchorMax = new Vector2(1f, 1f);
        epilogueContent.pivot = new Vector2(.5f, 1f);
        epilogueContent.sizeDelta = Vector2.zero;
        epilogueContent.anchoredPosition = Vector2.zero;
        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 14f;
        layout.padding = new RectOffset(6, 6, 8, 38);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        epilogueScroll.viewport = viewport;
        epilogueScroll.content = epilogueContent;
        epilogueScroll.verticalNormalizedPosition = 1f;
    }

    private TextMeshProUGUI CreateEpilogueBubble(EpilogueLine line)
    {
        GameObject rowObject = new GameObject(line.fromPlayer ? "Player Message" : "Incoming Message", typeof(RectTransform),
            typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.SetParent(epilogueContent, false);
        LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
        rowElement.preferredWidth = epilogueContent.rect.width;
        rowElement.minWidth = 0f;
        rowElement.flexibleWidth = 1f;
        HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = line.fromPlayer ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        rowLayout.padding = new RectOffset(20, 20, 0, 0);
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        GameObject bubbleObject = new GameObject("Speech Bubble", typeof(RectTransform), typeof(UnityEngine.UI.Image),
            typeof(Shadow), typeof(LayoutElement));
        RectTransform bubble = bubbleObject.GetComponent<RectTransform>();
        bubble.SetParent(row, false);
        UnityEngine.UI.Image background = bubbleObject.GetComponent<UnityEngine.UI.Image>();
        background.sprite = GetEpilogueBubbleSprite();
        background.type = UnityEngine.UI.Image.Type.Sliced;
        background.color = line.fromPlayer ? new Color32(13, 133, 231, 250) : new Color(.07f, .09f, .13f, .98f);
        background.raycastTarget = false;
        Shadow shadow = bubbleObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, .3f);
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
        text.enableAutoSizing = false;
        text.color = line.fromPlayer ? Color.white : new Color(.96f, .97f, 1f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        float availableBubbleWidth = Mathf.Max(150f, epilogueContent.rect.width - 40f);
        float maximumBubbleWidth = Mathf.Min(720f, availableBubbleWidth);
        float availableTextWidth = Mathf.Max(88f, maximumBubbleWidth - 62f);
        Vector2 preferred = text.GetPreferredValues(line.text ?? string.Empty, availableTextWidth, 0f);
        float width = Mathf.Clamp(preferred.x + 62f, 150f, maximumBubbleWidth);
        float height = Mathf.Max(68f, preferred.y + 34f);
        LayoutElement bubbleLayout = bubbleObject.GetComponent<LayoutElement>();
        bubbleLayout.preferredWidth = width;
        bubbleLayout.preferredHeight = height;
        rowElement.preferredHeight = height + 18f;
        return text;
    }

    private Sprite GetEpilogueBubbleSprite()
    {
        if (epilogueBubbleSprite != null) return epilogueBubbleSprite;
        const int size = 64;
        const int radius = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "Epilogue Rounded Bubble"
        };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float horizontal = x < radius ? radius - .5f - x : x - (size - radius - .5f);
            float vertical = y < radius ? radius - .5f - y : y - (size - radius - .5f);
            float distance = Mathf.Sqrt(Mathf.Pow(Mathf.Max(0f, horizontal), 2f) +
                Mathf.Pow(Mathf.Max(0f, vertical), 2f));
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius + .5f - distance)));
        }
        texture.Apply();
        epilogueBubbleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return epilogueBubbleSprite;
    }

    private static Sprite CreateCreditSprite(Texture2D image)
    {
        // Image.preserveAspect fits the source into this frame without changing its proportions.
        return Sprite.Create(image, new Rect(0f, 0f, image.width, image.height), new Vector2(.5f, .5f));
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
