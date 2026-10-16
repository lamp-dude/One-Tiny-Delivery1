using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// Place on an NPC or pickup marker with an isTrigger Collider. Entering the zone
/// automatically opens the matching delivery's not-started / started / finished dialogue.
/// </summary>
[RequireComponent(typeof(Collider))]
[MovedFrom(true, "", "", "DeliveryDialogueTrigger")]
public class DeliveryPickupZone : MonoBehaviour
{
    [SerializeField] private string deliveryId;
    private int playerColliderCount;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other) || DeliverySystem.Instance == null) return;
        playerColliderCount++;
        if (playerColliderCount == 1)
            DeliverySystem.Instance.TalkToGiver(deliveryId, this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerColliderCount = Mathf.Max(0, playerColliderCount - 1);
        if (playerColliderCount == 0 && DeliverySystem.Instance != null)
            DeliverySystem.Instance.CancelDialogue(this);
    }

    private bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") ||
               (other.transform.root != other.transform && other.transform.root.CompareTag("Player"));
    }
}
