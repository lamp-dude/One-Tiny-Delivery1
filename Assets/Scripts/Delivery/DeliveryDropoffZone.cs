using UnityEngine;

/// <summary>
/// Place this on the destination's isTrigger Collider. Arrival, delivery, and the next-job
/// handoff progress as one Enter-driven conversation while the player remains inside.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DeliveryDropoffZone : MonoBehaviour
{
    [SerializeField] private string deliveryId;
    private int playerColliderCount;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerColliderCount++;
        if (playerColliderCount == 1 && DeliverySystem.Instance != null)
            DeliverySystem.Instance.BeginDropoffDialogue(deliveryId, this);
    }

    private void Update()
    {
        // If the player arrived while another conversation was open, start this one
        // as soon as it closes instead of requiring the player to leave and re-enter.
        if (playerColliderCount > 0 && DeliverySystem.Instance != null && !DeliverySystem.Instance.IsDialogueOpen)
            DeliverySystem.Instance.BeginDropoffDialogue(deliveryId, this);
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
