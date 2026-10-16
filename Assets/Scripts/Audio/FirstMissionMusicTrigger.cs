using UnityEngine;

/// <summary>Attach to the Delivery System to play a cue only when its first mission begins.</summary>
[RequireComponent(typeof(DeliverySystem))]
public sealed class FirstMissionMusicTrigger : MonoBehaviour
{
    [SerializeField] private OneShotMusicCue musicCue;
    private DeliverySystem deliverySystem;
    private bool firstMissionStarted;

    private void Awake() => deliverySystem = GetComponent<DeliverySystem>();

    private void OnEnable()
    {
        if (deliverySystem != null) deliverySystem.DeliveryStateChanged += OnDeliveryStateChanged;
    }

    private void OnDisable()
    {
        if (deliverySystem != null) deliverySystem.DeliveryStateChanged -= OnDeliveryStateChanged;
    }

    private void OnDeliveryStateChanged(DeliverySystem.DeliveryDefinition delivery, DeliverySystem.DeliveryState state)
    {
        if (firstMissionStarted || state != DeliverySystem.DeliveryState.Started) return;
        firstMissionStarted = true;
        if (musicCue != null) musicCue.Play();
    }
}
