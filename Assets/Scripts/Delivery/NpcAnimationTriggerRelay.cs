using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcAnimationTriggerRelay : MonoBehaviour
{
    private readonly List<NpcMissionAnimator> listeners = new List<NpcMissionAnimator>();
    private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();

    public void AddListener(NpcMissionAnimator listener)
    {
        if (listener != null && !listeners.Contains(listener)) listeners.Add(listener);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other) || !playerColliders.Add(other)) return;
        if (playerColliders.Count == 1) SetPlayerPresent(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!playerColliders.Remove(other)) return;
        if (playerColliders.Count == 0) SetPlayerPresent(false);
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        SetPlayerPresent(false);
    }

    private void SetPlayerPresent(bool present)
    {
        listeners.RemoveAll(listener => listener == null);
        foreach (NpcMissionAnimator listener in listeners) listener.SetPlayerPresent(present);
    }

    private static bool IsPlayer(Collider other)
    {
        if (other.CompareTag("Player")) return true;
        Transform root = other.transform.root;
        return root != null && root.CompareTag("Player");
    }
}
