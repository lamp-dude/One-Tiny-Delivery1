using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns all delivery progress and the TMP speech bubble. Add one component to a
/// scene, then add entries to Deliveries for the main route and any side routes.
/// </summary>
public class DeliverySystem : MonoBehaviour
{
    public static DeliverySystem Instance { get; private set; }

    public enum DeliveryState { NotStarted, Started, Finished }
    public enum DialogueSpeaker { Giver, Player }
    public enum DialogueVoice { RouteDefault, MrsJohnston, MrLawrence, Anna, MrsCarraway, Eric, TempleKeeper, SummitKeeper }

    [Serializable]
    public class DialogueLine
    {
        public DialogueSpeaker speaker;
        [TextArea(2, 5)] public string text;

        public DialogueLine(DialogueSpeaker speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }
    }

    [Serializable]
    public class DeliveryDefinition
    {
        [Tooltip("A unique key used by dialogue and destination triggers, e.g. Main_A_To_B.")]
        public string id = "NewDelivery";
        public string objectiveName = "Deliver the parcel";
        [Tooltip("Voice treatment for this route's giver lines. Player lines always retain the main-character voice.")]
        public DialogueVoice giverVoice = DialogueVoice.RouteDefault;

        [Header("Cargo")]
        [Tooltip("Prefab spawned when this delivery is accepted. Give it a Rigidbody and Collider so it can land naturally in the truck bed.")]
        [FormerlySerializedAs("cargo")]
        public GameObject cargoPrefab;
        [Tooltip("Point above the truck bed where cargo is spawned.")]
        public Transform safeCargoAnchor;
        [Tooltip("Vertical distance above the anchor before the package is released onto the bed.")]
        [Min(0f)] public float cargoSpawnHeight = 1f;
        [Tooltip("A cargo piece is marked dropped only after it moves this far sideways from the truck bed, or falls this far below it.")]
        [Min(0.1f)] public float droppedDistance = 3f;
        public bool destroyCargoOnDelivery = true;

        [Header("Conversations")]
        [Tooltip("Opening conversation for the first route only. Later routes begin at the previous drop-off.")]
        public DialogueLine[] pickupConversation =
        {
            new DialogueLine(DialogueSpeaker.Giver, "Hey there, what's up?"),
            new DialogueLine(DialogueSpeaker.Player, "Nothing much. You?"),
            new DialogueLine(DialogueSpeaker.Giver, "I'm good. Want to take this delivery?"),
            new DialogueLine(DialogueSpeaker.Player, "Sure.")
        };
        [Tooltip("First route only: runs immediately after its package drops into the truck bed.")]
        public DialogueLine[] startedConversation =
        {
            new DialogueLine(DialogueSpeaker.Giver, "Great! Please get it there safely."),
            new DialogueLine(DialogueSpeaker.Player, "I'll get it there safely.")
        };
        [Tooltip("Runs when the player returns to the giver with an active delivery.")]
        public DialogueLine[] startedReturnConversation =
        {
            new DialogueLine(DialogueSpeaker.Giver, "You're still on this delivery."),
            new DialogueLine(DialogueSpeaker.Player, "I'm still on it.")
        };
        public DialogueLine[] finishedConversation =
        {
            new DialogueLine(DialogueSpeaker.Giver, "Thanks again!"),
            new DialogueLine(DialogueSpeaker.Player, "Happy to help.")
        };
        [Tooltip("Legacy setting kept for existing scenes. The first route now always starts when its opening conversation ends.")]
        public bool acceptAfterNotStartedDialogue = true;
        public DialogueLine[] arrivalConversation =
        {
            new DialogueLine(DialogueSpeaker.Giver, "Is that my delivery?"),
            new DialogueLine(DialogueSpeaker.Player, "Yep, it's right here.")
        };
        public DialogueLine[] deliveredConversation =
        {
            new DialogueLine(DialogueSpeaker.Giver, "Perfect, thank you!"),
            new DialogueLine(DialogueSpeaker.Player, "Glad it made it.")
        };
        public DialogueLine[] droppedCargoConversation =
        {
            new DialogueLine(DialogueSpeaker.Giver, "...Thanks, I guess."),
            new DialogueLine(DialogueSpeaker.Player, "Sorry about that.")
        };
        [Tooltip("Runs after this delivery's result if another route follows. That route's package drops after this conversation.")]
        public DialogueLine[] nextDeliveryConversation =
        {
            new DialogueLine(DialogueSpeaker.Giver, "Want another one?"),
            new DialogueLine(DialogueSpeaker.Player, "Yes!")
        };

        [Header("Completion viewpoint (optional)")]
        [Tooltip("An empty marker at the summit/viewpoint. When assigned, the Player vehicle is moved here after this delivery succeeds.")]
        public Transform completionDestination;
        [Tooltip("A short pause lets the final delivery dialogue land before the reward transport.")]
        [Min(0f)] public float completionTeleportDelay = 0.75f;
        [Tooltip("Lifts the vehicle slightly above the marker so it can settle safely onto the terrain.")]
        [Min(0f)] public float completionGroundClearance = 0.35f;
        [Tooltip("A final Enter-driven conversation shown beside the player after reaching the completion viewpoint.")]
        public DialogueLine[] completionConversation = Array.Empty<DialogueLine>();

        [Header("Final scene transition (last delivery only)")]
        [Tooltip("After this delivery's completion dialogue, fade to black and load this scene. Leave empty to remain at the viewpoint.")]
        public string completionSceneName = "Finale";
        [Min(0.05f)] public float completionSceneRevealSeconds = 0.85f;

        [Header("Completion viewpoint atmosphere (optional)")]
        [Tooltip("A disabled sun GameObject to activate when the completion viewpoint begins.")]
        public GameObject completionSun;
        [Tooltip("The current directional light to disable after the new sun is activated.")]
        public Light completionDirectionalLightToDisable;
        public Color completionFogColor = new Color(.48f, .58f, .68f, 1f);
        public Color completionAmbientColor = new Color(.48f, .58f, .68f, 1f);

        [Header("Completion vehicle (optional)")]
        [Tooltip("When assigned, this prefab replaces the delivery vehicle at the completion viewpoint.")]
        public GameObject completionVehiclePrefab;
        [Tooltip("The camera to enable at the completion viewpoint. The normal vehicle camera is disabled.")]
        public Camera completionCamera;

        [Header("Events")]
        public UnityEvent onStarted;
        public UnityEvent onCargoDropped;
        public UnityEvent onFinished;
    }

    [Header("Deliveries")]
    [SerializeField] private List<DeliveryDefinition> deliveries = new List<DeliveryDefinition>();
    [SerializeField] private TMP_FontAsset dialogueFont;

    [Header("Mission Restart")]
    [SerializeField, Min(0f)] private float restartFadeSeconds = 0.4f;

    // Generated automatically: no UI, camera, or player references need to be assigned.
    private Canvas dialogueCanvas;
    private RectTransform dialogueBubble;
    private GameObject dialoguePanel;
    private TMP_Text dialogueText;
    private Image dialogueBackground;
    private bool playerSpeakingInCurrentLine;
    private Transform playerDialogueAnchor;
    private Transform dialogueSpeakerAnchor;
    private readonly Vector3 worldBubbleOffset = new Vector3(0f, 2.2f, 0f);
    private readonly Vector2 screenBubbleOffset = new Vector2(170f, 70f);
    // iMessage-like spacing: snug vertically, with enough horizontal breathing room.
    private const float BubbleHorizontalPadding = 36f;
    private const float MultilineVerticalPadding = 24f;
    private const float SingleLineBubbleHeight = 64f;
    private const float MaxBubbleWidth = 760f;
    private const float ScreenEdgePadding = 24f;
    private const int BubbleCornerRadius = 32;
    private const float PlayerCharactersPerSecond = 45f;
    private const float CharacterBounceSeconds = .28f;

    private readonly Dictionary<string, DeliveryState> states = new Dictionary<string, DeliveryState>();
    private readonly HashSet<string> droppedCargo = new HashSet<string>();
    private readonly Dictionary<string, DropoffStage> dropoffStages = new Dictionary<string, DropoffStage>();
    private readonly Dictionary<string, GameObject> activeCargo = new Dictionary<string, GameObject>();
    private Coroutine dialogueRoutine;
    private bool skipTyping;
    private Component activeDialogueSource;
    private Camera worldCamera;
    private static Sprite roundedBubbleSprite;
    private string activeDeliveryId;
    private Vector3 missionStartPosition;
    private Quaternion missionStartRotation;
    private bool hasMissionStart;
    private bool isRestarting;
    private bool isCompletingViewpoint;
    private bool useFinalDialogueLayout;
    private Image restartFadeImage;

    // Runtime-generated UI sounds keep the delivery system self-contained: no scene wiring or
    // imported audio assets are needed for dialogue, package loading, or completion feedback.
    private AudioSource uiSoundSource;
    private AudioSource[] dialogueBlipSources;
    private int nextDialogueBlipSource;
    private AudioClip dialogueBlipClip;
    private AudioClip packagePopClip;
    private AudioClip deliveryPoofClip;
    private float nextDialogueBlipTime;
    private float[] characterBounceStartTimes = Array.Empty<float>();

    public bool IsDialogueOpen => dialogueRoutine != null;
    public event Action<DeliveryDefinition, DeliveryState> DeliveryStateChanged;

    private enum DropoffStage { Arrival, AwaitingDeliveryResult, AwaitingNextDelivery, Complete }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (GetComponent<PauseMenuController>() == null)
            gameObject.AddComponent<PauseMenuController>();

        foreach (DeliveryDefinition delivery in deliveries)
        {
            if (delivery == null) continue;
            if (string.IsNullOrWhiteSpace(delivery.id))
            {
                Debug.LogError("DeliverySystem has a delivery with no id.", this);
                continue;
            }

            if (states.ContainsKey(delivery.id))
            {
                Debug.LogError($"DeliverySystem has a duplicate delivery id '{delivery.id}'. Each route needs a unique id.", this);
                continue;
            }

            states[delivery.id] = DeliveryState.NotStarted;

            // Existing scenes used a placed object for cargo. Keep it only as a hidden template
            // so the delivery now spawns a fresh physics package when the player accepts.
            if (delivery.cargoPrefab != null && delivery.cargoPrefab.scene.IsValid())
                delivery.cargoPrefab.SetActive(false);
        }

        CreateBuiltInSpeechBubble();
        CreateBuiltInAudio();
    }

    private void Update()
    {
        if (PrologueController.IsGameplayBlocked) return;
        if (Input.GetKeyDown(KeyCode.R)) RestartActiveDelivery();
        CheckForDroppedCargo();

        if (!IsDialogueOpen) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            skipTyping = true;
    }

    private void LateUpdate()
    {
        if (IsDialogueOpen && !useFinalDialogueLayout) UpdateBubbleScreenPosition();
    }

    private void OnEnable() => Canvas.willRenderCanvases += UpdateBubbleBeforeCanvasRender;
    private void OnDisable()
    {
        Canvas.willRenderCanvases -= UpdateBubbleBeforeCanvasRender;
        SetPlayerDialogueHandbrake(false);
        if (restartFadeImage != null) Destroy(restartFadeImage.canvas.gameObject);
        restartFadeImage = null;
        isRestarting = false;
        isCompletingViewpoint = false;
        useFinalDialogueLayout = false;
    }

    // Runs after camera LateUpdate and immediately before UI rendering, so orbiting the camera
    // cannot leave the bubble at an old screen position.
    private void UpdateBubbleBeforeCanvasRender()
    {
        if (IsDialogueOpen && !useFinalDialogueLayout) UpdateBubbleScreenPosition();
    }

    /// <summary>Returns the current state so other UI can display it.</summary>
    public DeliveryState GetState(string deliveryId)
    {
        if (string.IsNullOrWhiteSpace(deliveryId)) return DeliveryState.NotStarted;
        return states.TryGetValue(deliveryId, out DeliveryState state) ? state : DeliveryState.NotStarted;
    }

    public DeliveryDefinition GetDelivery(string deliveryId)
    {
        return deliveries.Find(delivery => delivery != null && delivery.id == deliveryId);
    }

    /// <summary>Runs a route's completion presentation from the Delivery System inspector in Play Mode.</summary>
    public void DebugCompletionViewpoint(string deliveryId)
    {
        DeliveryDefinition delivery = GetDelivery(deliveryId);
        if (!Application.isPlaying || delivery == null || delivery.completionDestination == null ||
            IsDialogueOpen || isRestarting || isCompletingViewpoint) return;

        StartDialogueRoutine(ShowDebugCompletionViewpoint(delivery), null);
    }

    private IEnumerator ShowDebugCompletionViewpoint(DeliveryDefinition delivery)
    {
        yield return ShowCompletionViewpoint(delivery);
        CloseDialogue();
    }

    public void StartDelivery(string deliveryId)
    {
        DeliveryDefinition delivery = GetDelivery(deliveryId);
        if (delivery == null || GetState(deliveryId) != DeliveryState.NotStarted) return;

        int routeIndex = deliveries.IndexOf(delivery);
        if (routeIndex > 0)
        {
            DeliveryDefinition previous = deliveries[routeIndex - 1];
            if (previous == null || string.IsNullOrWhiteSpace(previous.id) ||
                GetState(previous.id) != DeliveryState.Finished) return;
        }

        states[deliveryId] = DeliveryState.Started;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            missionStartPosition = player.transform.position;
            missionStartRotation = player.transform.rotation;
            hasMissionStart = true;
        }
        else
        {
            hasMissionStart = false;
            Debug.LogWarning($"Could not save the restart point for '{delivery.objectiveName}': no active Player-tagged vehicle was found.", this);
        }
        activeDeliveryId = deliveryId;
        droppedCargo.Remove(deliveryId);
        dropoffStages.Remove(deliveryId);
        SpawnCargoOnVehicle(delivery);
        delivery.onStarted?.Invoke();
        DeliveryStateChanged?.Invoke(delivery, DeliveryState.Started);
    }

    /// <summary>Only the first route has a pickup introduction; later routes begin at the previous drop-off.</summary>
    public void TalkToGiver(string deliveryId, Component dialogueSource = null)
    {
        DeliveryDefinition delivery = GetDelivery(deliveryId);
        if (delivery == null || IsDialogueOpen || isRestarting) return;

        DeliveryState state = GetState(deliveryId);
        if (state == DeliveryState.NotStarted)
        {
            // The previous route's next-delivery conversation already accepts every later job.
            if (deliveries.IndexOf(delivery) == 0)
                StartDialogueRoutine(ShowFirstPickupDialogue(delivery), dialogueSource);
        }
        else if (state == DeliveryState.Started)
            StartDialogueRoutine(ShowStandaloneDialogue(delivery.startedReturnConversation, delivery), dialogueSource);
        else
            StartDialogueRoutine(ShowStandaloneDialogue(delivery.finishedConversation, delivery), dialogueSource);
    }

    /// <summary>Runs arrival, result, and the next-job handoff without closing the bubble between them.</summary>
    public void BeginDropoffDialogue(string deliveryId, Component dialogueSource = null)
    {
        DeliveryDefinition delivery = GetDelivery(deliveryId);
        if (delivery == null || IsDialogueOpen || isRestarting) return;

        if (!dropoffStages.TryGetValue(deliveryId, out DropoffStage stage)) stage = DropoffStage.Arrival;
        DeliveryState state = GetState(deliveryId);
        if ((state == DeliveryState.Started && stage != DropoffStage.Complete) ||
            (state == DeliveryState.Finished && stage == DropoffStage.AwaitingNextDelivery))
            StartDialogueRoutine(ShowDropoffDialogue(delivery, stage), dialogueSource);
    }

    private DeliveryDefinition GetNextDelivery(DeliveryDefinition completedDelivery)
    {
        int completedIndex = deliveries.IndexOf(completedDelivery);
        int nextIndex = completedIndex + 1;
        if (completedIndex < 0 || nextIndex >= deliveries.Count) return null;
        DeliveryDefinition nextDelivery = deliveries[nextIndex];
        return nextDelivery != null && !string.IsNullOrWhiteSpace(nextDelivery.id) ? nextDelivery : null;
    }

    private void StartNextDelivery(DeliveryDefinition completedDelivery)
    {
        DeliveryDefinition nextDelivery = GetNextDelivery(completedDelivery);
        if (nextDelivery != null) StartDelivery(nextDelivery.id);
    }

    /// <summary>Stops only the dialogue that belongs to the zone the player just left.</summary>
    public void CancelDialogue(Component dialogueSource)
    {
        // Teleporting away from the drop-off naturally exits its trigger. That must not
        // cancel the completion sequence that the exit caused.
        if (isCompletingViewpoint) return;
        if (!IsDialogueOpen || (dialogueSource != null && activeDialogueSource != dialogueSource)) return;
        StopCoroutine(dialogueRoutine);
        dialogueRoutine = null;
        activeDialogueSource = null;
        dialogueSpeakerAnchor = null;
        skipTyping = false;
        useFinalDialogueLayout = false;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        SetPlayerDialogueHandbrake(false);
    }

    /// <summary>Restarts the current route at the vehicle's saved mission-start position.</summary>
    public void RestartActiveDelivery()
    {
        if (isRestarting || !hasMissionStart || string.IsNullOrEmpty(activeDeliveryId) ||
            GetState(activeDeliveryId) != DeliveryState.Started) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("Cannot restart the delivery: no active Player-tagged vehicle was found.", this);
            return;
        }

        StartCoroutine(RestartDeliveryRoutine(player));
    }

    private IEnumerator RestartDeliveryRoutine(GameObject player)
    {
        isRestarting = true;
        CancelDialogue(null);
        SetPlayerDialogueHandbrake(true);
        Image fade = GetRestartFadeImage();
        yield return FadeRestartScreen(fade, 0f, 1f);

        DeliveryDefinition delivery = GetDelivery(activeDeliveryId);
        if (delivery != null)
        {
            if (activeCargo.TryGetValue(delivery.id, out GameObject cargo) && cargo != null)
                Destroy(cargo);
            activeCargo.Remove(delivery.id);
            droppedCargo.Remove(delivery.id);
            dropoffStages.Remove(delivery.id);

            Rigidbody body = player.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                // Move the Transform too: the bed anchor is a child Transform, and with
                // Rigidbody interpolation it can otherwise still report the old location.
                player.transform.SetPositionAndRotation(missionStartPosition, missionStartRotation);
                body.position = missionStartPosition;
                body.rotation = missionStartRotation;
                body.Sleep();
            }
            else
            {
                player.transform.SetPositionAndRotation(missionStartPosition, missionStartRotation);
            }
            Physics.SyncTransforms();

            // Let physics and child transforms settle at the new position, and finish
            // destroying the old cargo, before reading the bed anchor for a fresh spawn.
            yield return new WaitForFixedUpdate();
            SpawnCargoOnVehicle(delivery);
            delivery.onStarted?.Invoke();
            DeliveryStateChanged?.Invoke(delivery, DeliveryState.Started);
        }

        yield return FadeRestartScreen(fade, 1f, 0f);
        if (fade != null) Destroy(fade.canvas.gameObject);
        restartFadeImage = null;
        SetPlayerDialogueHandbrake(false);
        isRestarting = false;
    }

    private Image GetRestartFadeImage()
    {
        if (restartFadeImage != null) return restartFadeImage;

        GameObject fadeObject = new GameObject("Delivery Restart Fade", typeof(RectTransform), typeof(Canvas), typeof(Image));
        fadeObject.transform.SetParent(transform, false);
        Canvas canvas = fadeObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32767;

        RectTransform rect = fadeObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        restartFadeImage = fadeObject.GetComponent<Image>();
        restartFadeImage.color = new Color(0f, 0f, 0f, 0f);
        restartFadeImage.raycastTarget = false;
        return restartFadeImage;
    }

    private IEnumerator FadeRestartScreen(Image fade, float from, float to)
    {
        float duration = Mathf.Max(0f, restartFadeSeconds);
        if (duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
        }
        fade.color = new Color(0f, 0f, 0f, to);
    }

    private void FinishDelivery(DeliveryDefinition delivery, bool wasDropped)
    {
        states[delivery.id] = DeliveryState.Finished;
        if (!wasDropped) PlayUiSound(deliveryPoofClip, 0.16f);
        if (activeCargo.TryGetValue(delivery.id, out GameObject cargo) && cargo != null)
        {
            if (delivery.destroyCargoOnDelivery) Destroy(cargo);
            else cargo.SetActive(false);
        }
        activeCargo.Remove(delivery.id);

        delivery.onFinished?.Invoke();
        DeliveryStateChanged?.Invoke(delivery, DeliveryState.Finished);
    }

    private IEnumerator ShowCompletionViewpoint(DeliveryDefinition delivery)
    {
        isCompletingViewpoint = true;
        if (delivery.completionTeleportDelay > 0f)
            yield return new WaitForSeconds(delivery.completionTeleportDelay);

        Image fade = GetRestartFadeImage();
        yield return FadeRestartScreen(fade, 0f, 1f);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning($"Could not move the player to the completion viewpoint for '{delivery.objectiveName}': no active Player-tagged vehicle was found.", this);
            yield return FadeRestartScreen(fade, 1f, 0f);
            isCompletingViewpoint = false;
            yield break;
        }

        Vector3 destination = delivery.completionDestination.position + Vector3.up * delivery.completionGroundClearance;
        Quaternion rotation = delivery.completionDestination.rotation;
        ApplyCompletionAtmosphere(delivery);
        ApplyCompletionCamera(delivery);

        if (delivery.completionVehiclePrefab != null)
        {
            GameObject replacement = Instantiate(delivery.completionVehiclePrefab, destination, rotation);
            replacement.name = $"{delivery.objectiveName} Completion Vehicle";
            replacement.tag = "Player";
            player.tag = "Untagged";
            Destroy(player);
            player = replacement;

            carCam vehicleCamera = FindFirstObjectByType<carCam>();
            if (vehicleCamera != null) vehicleCamera.target = player.transform;
        }

        Rigidbody vehicleBody = player.GetComponent<Rigidbody>();

        // Clear existing motion so delivery momentum cannot carry the player off the summit.
        if (vehicleBody != null)
        {
            vehicleBody.linearVelocity = Vector3.zero;
            vehicleBody.angularVelocity = Vector3.zero;
            vehicleBody.position = destination;
            vehicleBody.rotation = rotation;
            vehicleBody.Sleep();
        }
        else
        {
            player.transform.SetPositionAndRotation(destination, rotation);
        }

        Physics.SyncTransforms();
        SetPlayerVehicleLocked(true);
        yield return new WaitForFixedUpdate();
        yield return FadeRestartScreen(fade, 1f, 0f);

        useFinalDialogueLayout = true;
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        // Passing no source makes both sides of this final conversation stay beside the player.
        if (delivery.completionConversation != null)
        {
            foreach (DialogueLine line in delivery.completionConversation)
            {
                if (line == null) continue;
                SetDialogueSpeaker(null, line, delivery);
                yield return TypeLine(line.text ?? string.Empty, GetVoiceProfileForLine(line, delivery));
                skipTyping = false;
                while (!skipTyping) yield return null;
            }
        }

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        yield return FadeRestartScreen(fade, 0f, 1f);
        if (GetNextDelivery(delivery) == null && !string.IsNullOrWhiteSpace(delivery.completionSceneName))
        {
            StartCompletionSceneTransition(delivery.completionSceneName, delivery.completionSceneRevealSeconds);
            yield break;
        }
        isCompletingViewpoint = false;
    }

    private static void StartCompletionSceneTransition(string sceneName, float revealSeconds)
    {
        GameObject transitionObject = new GameObject("Final Mission Scene Transition");
        DontDestroyOnLoad(transitionObject);
        transitionObject.AddComponent<FinalMissionSceneTransition>().Begin(sceneName, revealSeconds);
    }

    private sealed class FinalMissionSceneTransition : MonoBehaviour
    {
        public void Begin(string sceneName, float revealSeconds)
        {
            StartCoroutine(LoadAfterFinalMission(sceneName, revealSeconds));
        }

        private IEnumerator LoadAfterFinalMission(string sceneName, float revealSeconds)
        {
            Image curtain = CreatePersistentCurtain();
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"Final mission could not load scene '{sceneName}'. Add it to Build Settings.");
                Destroy(curtain.canvas.gameObject);
                Destroy(gameObject);
                yield break;
            }

            yield return operation;
            for (float elapsed = 0f; elapsed < revealSeconds; elapsed += Time.unscaledDeltaTime)
            {
                curtain.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(elapsed / revealSeconds));
                yield return null;
            }
            Destroy(curtain.canvas.gameObject);
            Destroy(gameObject);
        }

        private static Image CreatePersistentCurtain()
        {
            GameObject curtainObject = new GameObject("Final Mission Fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Image));
            DontDestroyOnLoad(curtainObject);
            Canvas canvas = curtainObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;
            CanvasScaler scaler = curtainObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            RectTransform rect = curtainObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = curtainObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;
            return image;
        }
    }

    private static void ApplyCompletionAtmosphere(DeliveryDefinition delivery)
    {
        if (delivery.completionSun != null)
        {
            delivery.completionSun.SetActive(true);
            Light completionSunLight = delivery.completionSun.GetComponentInChildren<Light>(true);
            if (completionSunLight != null && completionSunLight.type == LightType.Directional)
                RenderSettings.sun = completionSunLight;
        }
        if (delivery.completionDirectionalLightToDisable != null &&
            delivery.completionDirectionalLightToDisable.gameObject != delivery.completionSun)
            delivery.completionDirectionalLightToDisable.gameObject.SetActive(false);

        LightingManager dayNightLighting = FindFirstObjectByType<LightingManager>();
        if (dayNightLighting != null) dayNightLighting.enabled = false;
        RenderSettings.fogColor = delivery.completionFogColor;
        RenderSettings.ambientLight = delivery.completionAmbientColor;
    }

    private static void ApplyCompletionCamera(DeliveryDefinition delivery)
    {
        if (delivery.completionCamera == null) return;

        carCam vehicleCamera = FindFirstObjectByType<carCam>();
        Camera normalCamera = vehicleCamera != null ? vehicleCamera.GetComponent<Camera>() : Camera.main;
        if (vehicleCamera != null) vehicleCamera.enabled = false;
        if (normalCamera != null && normalCamera != delivery.completionCamera)
            normalCamera.gameObject.SetActive(false);

        delivery.completionCamera.gameObject.SetActive(true);
        delivery.completionCamera.enabled = true;
    }

    private void CheckForDroppedCargo()
    {
        foreach (DeliveryDefinition delivery in deliveries)
        {
            if (delivery == null || string.IsNullOrWhiteSpace(delivery.id)) continue;
            if (GetState(delivery.id) != DeliveryState.Started || droppedCargo.Contains(delivery.id) ||
                delivery.safeCargoAnchor == null || !activeCargo.TryGetValue(delivery.id, out GameObject cargo) || cargo == null) continue;

            if (HasCargoPieceFallenOut(cargo, delivery.safeCargoAnchor, delivery.droppedDistance))
            {
                droppedCargo.Add(delivery.id);
                delivery.onCargoDropped?.Invoke();
                Debug.Log($"Cargo for '{delivery.objectiveName}' was dropped.", this);
            }
        }
    }

    private static bool HasCargoPieceFallenOut(GameObject cargo, Transform bedAnchor, float allowedDistance)
    {
        // Prefabs can contain several independently simulated pieces. Check each Rigidbody's
        // physical centre instead of the prefab root/pivot, which may sit outside its visible parts.
        Rigidbody[] cargoBodies = cargo.GetComponentsInChildren<Rigidbody>();
        if (cargoBodies.Length == 0)
            return IsOutsideBed(cargo.transform.position, bedAnchor.position, allowedDistance);

        foreach (Rigidbody cargoBody in cargoBodies)
        {
            if (cargoBody != null && IsOutsideBed(cargoBody.worldCenterOfMass, bedAnchor.position, allowedDistance))
                return true;
        }

        return false;
    }

    private static bool IsOutsideBed(Vector3 piecePosition, Vector3 bedPosition, float allowedDistance)
    {
        Vector3 offset = piecePosition - bedPosition;
        float sidewaysDistance = new Vector2(offset.x, offset.z).magnitude;
        return sidewaysDistance > allowedDistance || offset.y < -allowedDistance;
    }

    private void StartDialogueRoutine(IEnumerator routine, Component dialogueSource)
    {
        activeDialogueSource = dialogueSource;
        SetPlayerDialogueHandbrake(true);
        dialogueRoutine = StartCoroutine(routine);
    }

    private void SetPlayerDialogueHandbrake(bool active)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        ThreeWheelerController vehicle = player != null
            ? player.GetComponentInChildren<ThreeWheelerController>()
            : FindFirstObjectByType<ThreeWheelerController>();
        if (vehicle != null) vehicle.SetDialogueHandbrake(active);
    }

    private void SetPlayerVehicleLocked(bool locked)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        ThreeWheelerController vehicle = player != null
            ? player.GetComponentInChildren<ThreeWheelerController>()
            : FindFirstObjectByType<ThreeWheelerController>();
        if (vehicle != null)
        {
            vehicle.SetVehicleLocked(locked);
            return;
        }

        if (player == null || !locked) return;
        foreach (Rigidbody body in player.GetComponentsInChildren<Rigidbody>())
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.Sleep();
        }
    }

    private void SetDialogueSpeaker(Component dialogueSource, DialogueLine line, DeliveryDefinition delivery)
    {
        bool playerSpeaking = line.speaker == DialogueSpeaker.Player;
        playerSpeakingInCurrentLine = playerSpeaking;
        if (playerSpeaking)
        {
            FindPlayerAnchorIfNeeded();
            dialogueSpeakerAnchor = playerDialogueAnchor;
        }
        else
        {
            dialogueSpeakerAnchor = dialogueSource != null ? dialogueSource.transform : null;
            if (dialogueSpeakerAnchor == null)
            {
                FindPlayerAnchorIfNeeded();
                dialogueSpeakerAnchor = playerDialogueAnchor;
            }
        }

        if (dialogueBackground != null)
            dialogueBackground.color = playerSpeaking
                ? new Color32(13, 133, 231, 250)
                : new Color(.07f, .09f, .13f, .98f);
        if (dialogueText != null)
        {
            dialogueText.color = playerSpeaking
                ? Color.white
                : new Color(.96f, .97f, 1f, 1f);
            dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        ApplyDialogueLayout(playerSpeaking);
    }

    private void ApplyDialogueLayout(bool playerSpeaking)
    {
        if (dialogueBubble == null) return;

        if (useFinalDialogueLayout)
        {
            // Final-scene dialogue is intentionally a fixed two-sided composition.
            // It does not follow either world Transform or camera movement.
            float horizontalAnchor = playerSpeaking ? 0f : 1f;
            dialogueBubble.anchorMin = new Vector2(horizontalAnchor, .5f);
            dialogueBubble.anchorMax = new Vector2(horizontalAnchor, .5f);
            dialogueBubble.pivot = new Vector2(horizontalAnchor, .5f);
            dialogueBubble.anchoredPosition = new Vector2(playerSpeaking ? 72f : -72f, 0f);
            return;
        }

        dialogueBubble.anchorMin = Vector2.one * .5f;
        dialogueBubble.anchorMax = Vector2.one * .5f;
        dialogueBubble.pivot = Vector2.one * .5f;
    }

    private IEnumerator ShowFirstPickupDialogue(DeliveryDefinition delivery)
    {
        // Yield once so even an empty conversation has an active coroutine for its whole lifetime.
        yield return null;
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        yield return ShowLines(delivery.pickupConversation, delivery);

        StartDelivery(delivery.id);
        yield return ShowLines(delivery.startedConversation, delivery);

        CloseDialogue();
    }

    private IEnumerator ShowStandaloneDialogue(DialogueLine[] lines, DeliveryDefinition delivery = null)
    {
        yield return null;
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        yield return ShowLines(lines, delivery);
        CloseDialogue();
    }

    private IEnumerator ShowDropoffDialogue(DeliveryDefinition delivery, DropoffStage startingStage)
    {
        yield return null;
        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        if (startingStage == DropoffStage.Arrival)
        {
            yield return ShowLines(delivery.arrivalConversation, delivery);
            dropoffStages[delivery.id] = DropoffStage.AwaitingDeliveryResult;
        }

        if (GetState(delivery.id) == DeliveryState.Started)
        {
            bool wasDropped = droppedCargo.Contains(delivery.id);
            DialogueLine[] result = wasDropped ? delivery.droppedCargoConversation : delivery.deliveredConversation;
            yield return ShowLines(result, delivery);
            FinishDelivery(delivery, wasDropped);
            dropoffStages[delivery.id] = DropoffStage.AwaitingNextDelivery;

            if (delivery.completionDestination != null)
            {
                yield return ShowCompletionViewpoint(delivery);
                dropoffStages[delivery.id] = DropoffStage.Complete;
                CloseDialogue();
                yield break;
            }
        }

        if (GetNextDelivery(delivery) != null)
        {
            yield return ShowLines(delivery.nextDeliveryConversation, delivery);
            dropoffStages[delivery.id] = DropoffStage.Complete;
            StartNextDelivery(delivery);
        }
        else
        {
            dropoffStages[delivery.id] = DropoffStage.Complete;
        }

        CloseDialogue();
    }

    private IEnumerator ShowLines(DialogueLine[] lines, DeliveryDefinition delivery = null)
    {
        if (lines != null)
        {
            foreach (DialogueLine line in lines)
            {
                if (line == null) continue;
                SetDialogueSpeaker(activeDialogueSource, line, delivery);
                yield return TypeLine(line.text ?? string.Empty, GetVoiceProfileForLine(line, delivery));
                skipTyping = false;
                while (!skipTyping) yield return null;
            }
        }
    }

    private void CloseDialogue()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        SetPlayerDialogueHandbrake(false);
        dialogueRoutine = null;
        activeDialogueSource = null;
        dialogueSpeakerAnchor = null;
        skipTyping = false;
        useFinalDialogueLayout = false;
    }

    private IEnumerator TypeLine(string line, DialogueVoiceProfile voice)
    {
        if (dialogueText == null) yield break;

        skipTyping = false;
        dialogueText.text = string.Empty;
        dialogueText.maxVisibleCharacters = 0;
        int characterCount = line.Length;
        characterBounceStartTimes = new float[characterCount];
        for (int i = 0; i < characterBounceStartTimes.Length; i++) characterBounceStartTimes[i] = -1f;
        float elapsed = 0f;

        while (dialogueText.maxVisibleCharacters < characterCount)
        {
            if (skipTyping)
            {
                RevealCharacters(line, dialogueText.maxVisibleCharacters, characterCount, voice);
                dialogueText.maxVisibleCharacters = characterCount;
                dialogueText.text = line;
                ResizeBubbleToText(line);
                ApplyCharacterBounces();
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            int visibleCharacters = Mathf.Min(characterCount, Mathf.FloorToInt(elapsed * voice.charactersPerSecond));
            RevealCharacters(line, dialogueText.maxVisibleCharacters, visibleCharacters, voice);
            dialogueText.maxVisibleCharacters = visibleCharacters;
            dialogueText.text = line.Substring(0, visibleCharacters);
            ResizeBubbleToText(dialogueText.text);
            ApplyCharacterBounces();
            yield return null;
        }

        // A skipped line still receives the same per-letter bounce before it can advance.
        while (HasBouncingCharacters())
        {
            ApplyCharacterBounces();
            yield return null;
        }
    }

    private void RevealCharacters(string line, int firstCharacter, int characterCount, DialogueVoiceProfile voice)
    {
        if (characterCount <= firstCharacter) return;

        float revealTime = Time.unscaledTime;
        for (int i = firstCharacter; i < characterCount; i++)
        {
            characterBounceStartTimes[i] = revealTime;
            PlayDialogueBlipFor(line, i, voice);
        }
    }

    private bool HasBouncingCharacters()
    {
        float now = Time.unscaledTime;
        foreach (float startTime in characterBounceStartTimes)
            if (startTime >= 0f && now - startTime < CharacterBounceSeconds) return true;
        return false;
    }

    private void ApplyCharacterBounces()
    {
        if (dialogueText == null || characterBounceStartTimes.Length == 0) return;

        dialogueText.ForceMeshUpdate();
        TMP_TextInfo textInfo = dialogueText.textInfo;
        float now = Time.unscaledTime;
        bool changedVertices = false;

        for (int i = 0; i < textInfo.characterCount && i < characterBounceStartTimes.Length; i++)
        {
            TMP_CharacterInfo character = textInfo.characterInfo[i];
            float age = now - characterBounceStartTimes[i];
            if (!character.isVisible || age < 0f || age >= CharacterBounceSeconds) continue;

            float progress = age / CharacterBounceSeconds;
            float scale = progress < .52f
                ? Mathf.Lerp(1.55f, .76f, progress / .52f)
                : Mathf.Lerp(.76f, 1f, (progress - .52f) / .48f);
            float lift = Mathf.Lerp(15f, 0f, progress);
            int materialIndex = character.materialReferenceIndex;
            int vertexIndex = character.vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
            Vector3 centre = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * .5f;

            for (int vertex = 0; vertex < 4; vertex++)
                vertices[vertexIndex + vertex] = centre + (vertices[vertexIndex + vertex] - centre) * scale + Vector3.up * lift;

            changedVertices = true;
        }

        if (changedVertices) dialogueText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    private static DialogueVoice ResolveGiverVoice(DeliveryDefinition delivery)
    {
        if (delivery == null) return DialogueVoice.RouteDefault;
        if (delivery.giverVoice != DialogueVoice.RouteDefault) return delivery.giverVoice;

        // Older scenes (including gamebackup.unity) predate the serialized voice field.
        // Keep those conversations expressive without requiring hand-updating every line.
        switch (delivery.id)
        {
            case "main": return DialogueVoice.MrsJohnston;
            case "J-L": return DialogueVoice.MrLawrence;
            case "L-A": return DialogueVoice.Anna;
            case "A-C": return DialogueVoice.MrsCarraway;
            case "C-E": return DialogueVoice.Eric;
            case "E-T": return DialogueVoice.TempleKeeper;
            case "T-H": return DialogueVoice.SummitKeeper;
            default: return DialogueVoice.RouteDefault;
        }
    }

    private static DialogueVoiceProfile GetVoiceProfileForLine(DialogueLine line, DeliveryDefinition delivery)
    {
        return GetVoiceProfile(line.speaker == DialogueSpeaker.Player
            ? DialogueVoice.RouteDefault
            : ResolveGiverVoice(delivery));
    }

    /// <summary>Spawns cargo above the truck bed; the final route's cargo is secured to the bed.</summary>
    private void SpawnCargoOnVehicle(DeliveryDefinition delivery)
    {
        if (delivery.cargoPrefab == null)
        {
            Debug.LogWarning($"Delivery '{delivery.objectiveName}' has no cargo prefab assigned.", this);
            return;
        }
        if (delivery.safeCargoAnchor == null)
        {
            Debug.LogWarning($"Delivery '{delivery.objectiveName}' has no truck-bed cargo anchor assigned.", this);
            return;
        }

        Vector3 spawnPosition = delivery.safeCargoAnchor.position + Vector3.up * delivery.cargoSpawnHeight;
        // This truck's bed marker is imported with a fixed -90° X rotation. Its own forward
        // axis therefore points sideways/upward, so read the parent body's heading instead.
        // We apply only that Y heading to keep upright prefabs upright.
        Transform headingSource = delivery.safeCargoAnchor.parent != null
            ? delivery.safeCargoAnchor.parent
            : delivery.safeCargoAnchor;
        Quaternion uprightHeading = Quaternion.Euler(0f, headingSource.eulerAngles.y, 0f);
        GameObject cargo = Instantiate(delivery.cargoPrefab, spawnPosition, uprightHeading);
        cargo.name = $"{delivery.objectiveName} Package";
        cargo.SetActive(true);
        activeCargo[delivery.id] = cargo;

        Rigidbody[] cargoBodies = cargo.GetComponentsInChildren<Rigidbody>();
        // The last delivery is the summit apple. Secure it to the truck so the steep final
        // climb cannot knock it out of the bed. Keep its world pose when parenting because
        // the bed marker itself has an imported rotation.
        bool secureToBed = GetNextDelivery(delivery) == null;
        if (secureToBed)
        {
            cargo.transform.SetParent(headingSource, true);
            foreach (Rigidbody cargoBody in cargoBodies)
            {
                if (cargoBody == null) continue;
                cargoBody.linearVelocity = Vector3.zero;
                cargoBody.angularVelocity = Vector3.zero;
                cargoBody.useGravity = false;
                cargoBody.isKinematic = true;
            }
            PlayUiSound(packagePopClip, 0.16f);
            return;
        }

        if (cargoBodies.Length > 0)
        {
            foreach (Rigidbody cargoBody in cargoBodies)
            {
                if (cargoBody == null) continue;
                cargoBody.isKinematic = false;
                cargoBody.useGravity = true;
                cargoBody.linearVelocity = Vector3.zero;
                cargoBody.angularVelocity = Vector3.zero;
                cargoBody.WakeUp();
            }
        }
        else
        {
            Debug.LogWarning($"Cargo prefab for '{delivery.objectiveName}' has no Rigidbody, so it cannot drop into the truck bed.", cargo);
        }

        PlayUiSound(packagePopClip, 0.16f);
    }

    private void PlayDialogueBlipFor(string line, int characterIndex, DialogueVoiceProfile voice)
    {
        if (characterIndex < 0 || characterIndex >= line.Length || char.IsWhiteSpace(line[characterIndex])) return;
        if (Time.unscaledTime < nextDialogueBlipTime) return;

        nextDialogueBlipTime = Time.unscaledTime + voice.blipInterval;
        PlayDialogueBlip(UnityEngine.Random.Range(voice.minVolume, voice.maxVolume),
            UnityEngine.Random.Range(voice.minPitch, voice.maxPitch));
    }

    private readonly struct DialogueVoiceProfile
    {
        public readonly float charactersPerSecond, minPitch, maxPitch, minVolume, maxVolume, blipInterval;
        public DialogueVoiceProfile(float charactersPerSecond, float minPitch, float maxPitch, float minVolume, float maxVolume, float blipInterval)
        {
            this.charactersPerSecond = charactersPerSecond;
            this.minPitch = minPitch;
            this.maxPitch = maxPitch;
            this.minVolume = minVolume;
            this.maxVolume = maxVolume;
            this.blipInterval = blipInterval;
        }
    }

    private static DialogueVoiceProfile GetVoiceProfile(DialogueVoice voice)
    {
        // Player values deliberately match the original dialogue cadence and blip treatment.
        switch (voice)
        {
            case DialogueVoice.MrLawrence: return new DialogueVoiceProfile(20f, .52f, .64f, .38f, .46f, .075f);
            case DialogueVoice.Anna: return new DialogueVoiceProfile(62f, 1.42f, 1.62f, .18f, .27f, .02f);
            case DialogueVoice.MrsJohnston: return new DialogueVoiceProfile(38f, .82f, .98f, .38f, .48f, .04f);
            case DialogueVoice.MrsCarraway: return new DialogueVoiceProfile(31f, .88f, 1.02f, .35f, .44f, .05f);
            case DialogueVoice.Eric: return new DialogueVoiceProfile(42f, .94f, 1.06f, .34f, .42f, .035f);
            case DialogueVoice.TempleKeeper: return new DialogueVoiceProfile(30f, .76f, .88f, .36f, .44f, .055f);
            case DialogueVoice.SummitKeeper: return new DialogueVoiceProfile(36f, 1.02f, 1.16f, .30f, .38f, .045f);
            default: return new DialogueVoiceProfile(PlayerCharactersPerSecond, .92f, 1.12f, .36f, .48f, .035f);
        }
    }

    private void CreateBuiltInAudio()
    {
        uiSoundSource = GetComponent<AudioSource>();
        if (uiSoundSource == null) uiSoundSource = gameObject.AddComponent<AudioSource>();
        uiSoundSource.playOnAwake = false;
        uiSoundSource.spatialBlend = 0f;
        uiSoundSource.ignoreListenerPause = true;

        dialogueBlipClip = CreateToneClip("Delivery Dialogue Blip", 0.055f, 520f, 0.08f, false);
        packagePopClip = CreateToneClip("Delivery Package Pop", 0.18f, 250f, 0.22f, true);
        deliveryPoofClip = CreatePoofClip();

        // PlayOneShot on the general UI source shares its pitch with package cues
        // and overlapping blips. Independent sources preserve each character's pitch.
        dialogueBlipSources = new AudioSource[6];
        for (int i = 0; i < dialogueBlipSources.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.clip = dialogueBlipClip;
            dialogueBlipSources[i] = source;
        }
    }

    private void PlayDialogueBlip(float volume, float pitch)
    {
        if (dialogueBlipSources == null || dialogueBlipClip == null) return;
        for (int i = 0; i < dialogueBlipSources.Length; i++)
        {
            int index = (nextDialogueBlipSource + i) % dialogueBlipSources.Length;
            AudioSource source = dialogueBlipSources[index];
            if (source.isPlaying) continue;
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
            nextDialogueBlipSource = (index + 1) % dialogueBlipSources.Length;
            return;
        }
    }

    private void PlayUiSound(AudioClip clip, float volume, float pitch = 1f)
    {
        if (uiSoundSource == null || clip == null) return;
        uiSoundSource.pitch = pitch;
        uiSoundSource.PlayOneShot(clip, volume);
    }

    private static AudioClip CreateToneClip(string clipName, float duration, float frequency, float volume, bool sweepUp)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(duration * sampleRate);
        float[] samples = new float[sampleCount];
        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / (float)sampleCount;
            float currentFrequency = sweepUp ? Mathf.Lerp(frequency, frequency * 2.1f, progress) : frequency;
            phase += currentFrequency / sampleRate;
            float envelope = Mathf.Sin(progress * Mathf.PI) * (1f - progress * 0.25f);
            samples[i] = Mathf.Sign(Mathf.Sin(phase * Mathf.PI * 2f)) * volume * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreatePoofClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.32f;
        int sampleCount = Mathf.CeilToInt(duration * sampleRate);
        float[] samples = new float[sampleCount];
        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / (float)sampleCount;
            phase += Mathf.Lerp(230f, 70f, progress) / sampleRate;
            float envelope = Mathf.Pow(1f - progress, 2.2f);
            float noise = UnityEngine.Random.Range(-1f, 1f) * 0.33f;
            samples[i] = (Mathf.Sin(phase * Mathf.PI * 2f) * 0.45f + noise) * envelope;
        }

        AudioClip clip = AudioClip.Create("Delivery Complete Poof", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void FindPlayerAnchorIfNeeded()
    {
        if (playerDialogueAnchor != null) return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerDialogueAnchor = player.transform;
            return;
        }

        // This project uses carCam. Falling back to its target means the dialogue works even
        // when the truck has not been given the Player tag yet.
        carCam vehicleCamera = FindFirstObjectByType<carCam>();
        if (vehicleCamera != null) playerDialogueAnchor = vehicleCamera.target;
    }

    // Creates a single, known-good UI hierarchy. This deliberately does not reuse arbitrary
    // Canvas children: old panel/text setups often have layout groups, anchors, or fitters that
    // fight each other and scatter the visible result.
    private void CreateBuiltInSpeechBubble()
    {
        if (dialogueCanvas == null)
        {
            GameObject canvasObject = GameObject.Find("Delivery Dialogue Canvas");
            if (canvasObject == null)
                canvasObject = new GameObject("Delivery Dialogue Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            dialogueCanvas = canvasObject.GetComponent<Canvas>();
            if (dialogueCanvas == null) dialogueCanvas = canvasObject.AddComponent<Canvas>();
            dialogueCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            dialogueCanvas.overrideSorting = true;
            dialogueCanvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            if (canvasObject.GetComponent<GraphicRaycaster>() == null) canvasObject.AddComponent<GraphicRaycaster>();
        }

        Transform existing = dialogueCanvas.transform.Find("DeliverySpeechBubble");
        GameObject bubbleObject = existing == null
            ? new GameObject("DeliverySpeechBubble", typeof(RectTransform), typeof(Image), typeof(Shadow))
            : existing.gameObject;
        bubbleObject.transform.SetParent(dialogueCanvas.transform, false);

        dialogueBubble = bubbleObject.GetComponent<RectTransform>();
        dialogueBubble.anchorMin = Vector2.one * .5f;
        dialogueBubble.anchorMax = Vector2.one * .5f;
        dialogueBubble.pivot = Vector2.one * .5f;
        dialogueBackground = bubbleObject.GetComponent<Image>();
        if (dialogueBackground == null) dialogueBackground = bubbleObject.AddComponent<Image>();
        dialogueBackground.sprite = GetRoundedBubbleSprite();
        dialogueBackground.type = Image.Type.Sliced;
        dialogueBackground.color = new Color32(13, 133, 231, 250);
        Shadow shadow = bubbleObject.GetComponent<Shadow>();
        if (shadow == null) shadow = bubbleObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(.12f, .1f, .05f, .3f);
        shadow.effectDistance = new Vector2(3f, -4f);

        Transform textChild = bubbleObject.transform.Find("DialogueText");
        GameObject textObject = textChild == null
            ? new GameObject("DialogueText", typeof(RectTransform), typeof(TextMeshProUGUI))
            : textChild.gameObject;
        textObject.transform.SetParent(bubbleObject.transform, false);
        dialogueText = textObject.GetComponent<TextMeshProUGUI>();
        if (dialogueText == null) dialogueText = textObject.AddComponent<TextMeshProUGUI>();
        dialogueText.font = dialogueFont != null ? dialogueFont : TMP_Settings.defaultFontAsset;
        // Reset a stale TMP material preset whenever the runtime bubble is reused. The font
        // asset's own material has the correct Sniglet atlas and no white highlight effects.
        dialogueText.fontSharedMaterial = dialogueText.font.material;
        dialogueText.fontSize = 38f;
        dialogueText.color = Color.white;
        dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.overflowMode = TextOverflowModes.Overflow;
        RectTransform textRect = dialogueText.rectTransform;
        textRect.anchorMin = Vector2.one * .5f;
        textRect.anchorMax = Vector2.one * .5f;
        textRect.pivot = Vector2.one * .5f;
        textRect.anchoredPosition = Vector2.zero;

        dialoguePanel = bubbleObject;
        bubbleObject.SetActive(false);
    }

    private Sprite GetRoundedBubbleSprite()
    {
        if (roundedBubbleSprite != null) return roundedBubbleSprite;

        const int size = 64;
        int radius = BubbleCornerRadius;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "Delivery Rounded Bubble"
        };

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Pixel-centred quarter circles create a true capsule when the 32 px borders
            // meet at the bubble's 64 px single-line height.
            float horizontal = x < radius ? radius - .5f - x : x - (size - radius - .5f);
            float vertical = y < radius ? radius - .5f - y : y - (size - radius - .5f);
            horizontal = Mathf.Max(0f, horizontal);
            vertical = Mathf.Max(0f, vertical);
            float distance = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
            float alpha = Mathf.Clamp01(radius + .5f - distance);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        texture.Apply();
        roundedBubbleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        roundedBubbleSprite.name = "Delivery Rounded Bubble Sprite";
        return roundedBubbleSprite;
    }

    /// <summary>Projects the active speaker's world position into the Canvas, keeping the bubble a flat UI element.</summary>
    private void UpdateBubbleScreenPosition()
    {
        if (dialogueBubble == null || dialogueCanvas == null) return;
        if (dialogueSpeakerAnchor == null)
        {
            FindPlayerAnchorIfNeeded();
            dialogueSpeakerAnchor = playerDialogueAnchor;
        }
        if (dialogueSpeakerAnchor == null) return;

        Camera projectionCamera = GetProjectionCamera();
        if (projectionCamera == null) return;

        Vector3 screenPoint = projectionCamera.WorldToScreenPoint(dialogueSpeakerAnchor.position + worldBubbleOffset);
        if (screenPoint.z <= 0f) return;

        RectTransform canvasRect = dialogueCanvas.transform as RectTransform;
        Camera canvasCamera = dialogueCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : dialogueCanvas.worldCamera;
        if (canvasCamera == null && dialogueCanvas.renderMode != RenderMode.ScreenSpaceOverlay) canvasCamera = projectionCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCamera, out Vector2 canvasPoint)) return;

        Vector2 position = canvasPoint + screenBubbleOffset;
        Vector2 halfCanvas = canvasRect.rect.size * .5f;
        Vector2 halfBubble = dialogueBubble.rect.size * .5f;
        position.x = Mathf.Clamp(position.x, -halfCanvas.x + halfBubble.x + ScreenEdgePadding, halfCanvas.x - halfBubble.x - ScreenEdgePadding);
        position.y = Mathf.Clamp(position.y, -halfCanvas.y + halfBubble.y + ScreenEdgePadding, halfCanvas.y - halfBubble.y - ScreenEdgePadding);
        dialogueBubble.anchoredPosition = position;
    }

    private Camera GetProjectionCamera()
    {
        if (worldCamera != null && worldCamera.isActiveAndEnabled) return worldCamera;
        if (dialogueCanvas != null && dialogueCanvas.worldCamera != null) return dialogueCanvas.worldCamera;
        if (Camera.main != null) return Camera.main;
        carCam vehicleCamera = FindFirstObjectByType<carCam>();
        if (vehicleCamera != null) return vehicleCamera.GetComponent<Camera>();
        return FindFirstObjectByType<Camera>();
    }

    /// <summary>Sizes both the bubble and its TMP text area as the current line grows.</summary>
    private void ResizeBubbleToText(string visibleText)
    {
        if (dialogueBubble == null || dialogueText == null) return;

        float usableWidth = Mathf.Max(1f, MaxBubbleWidth - BubbleHorizontalPadding);
        Vector2 preferred = dialogueText.GetPreferredValues(visibleText, usableWidth, 0f);
        float textWidth = Mathf.Clamp(preferred.x, 1f, usableWidth);
        float bubbleWidth = textWidth + BubbleHorizontalPadding;
        float textHeight = dialogueText.GetPreferredValues(visibleText, textWidth, 0f).y;
        float singleLineTextHeight = dialogueText.GetPreferredValues("Ag", usableWidth, 0f).y;
        bool isMultiline = visibleText.IndexOf('\n') >= 0 || textHeight > singleLineTextHeight * 1.35f;
        float bubbleHeight = isMultiline
            ? Mathf.Max(SingleLineBubbleHeight, textHeight + MultilineVerticalPadding)
            : SingleLineBubbleHeight;

        dialogueBubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
        dialogueBubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
        RectTransform textRect = dialogueText.rectTransform;
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
        textRect.anchoredPosition = Vector2.zero;
    }

}
