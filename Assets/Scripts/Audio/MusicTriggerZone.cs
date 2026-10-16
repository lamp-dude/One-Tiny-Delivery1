using UnityEngine;

/// <summary>Plays its cue once when the Player enters this BoxCollider trigger.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class MusicTriggerZone : MonoBehaviour
{
    [SerializeField] private OneShotMusicCue musicCue;
    private int playerColliderCount;

    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerColliderCount++;
        if (playerColliderCount == 1 && musicCue != null) musicCue.Play();
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other)) playerColliderCount = Mathf.Max(0, playerColliderCount - 1);
    }

    private static bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") ||
               (other.transform.root != other.transform && other.transform.root.CompareTag("Player"));
    }
}
