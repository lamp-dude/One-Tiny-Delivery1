using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatically wires every hierarchy named "people" to its nearest delivery trigger.
/// Person names choose special animations; all other humanoids use the shared idle.
/// </summary>
public static class NpcAnimationBootstrap
{
    private const string LibraryResourceName = "Npc Animation Library";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoad()
    {
        SceneManager.sceneLoaded -= Initialize;
        SceneManager.sceneLoaded += Initialize;
    }

    private static void Initialize(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "gamebackup") return;

        NpcAnimationLibrary library = Resources.Load<NpcAnimationLibrary>(LibraryResourceName);
        if (library == null || library.defaultIdle == null)
        {
            Debug.LogWarning("NPC animations were not initialized because the NPC Animation Library resource is missing.");
            return;
        }

        List<Collider> deliveryTriggers = FindDeliveryTriggers();
        foreach (GameObject root in scene.GetRootGameObjects())
            BindPeopleGroups(root.transform, library, deliveryTriggers);
    }

    private static void BindPeopleGroups(Transform current, NpcAnimationLibrary library, List<Collider> triggers)
    {
        if (current.name.Equals("people", StringComparison.OrdinalIgnoreCase))
            BindGroup(current, library, triggers);

        for (int i = 0; i < current.childCount; i++)
            BindPeopleGroups(current.GetChild(i), library, triggers);
    }

    private static void BindGroup(Transform peopleRoot, NpcAnimationLibrary library, List<Collider> triggers)
    {
        foreach (Animator animator in peopleRoot.GetComponentsInChildren<Animator>(true))
        {
            if (animator.avatar != null && !animator.avatar.isHuman) continue;

            NpcMissionAnimator npc = animator.GetComponent<NpcMissionAnimator>() ?? animator.gameObject.AddComponent<NpcMissionAnimator>();
            ConfigureByName(npc, BuildHierarchyName(animator.transform, peopleRoot), library);

            // The scene uses one global "people" parent. Match each individual person to
            // the delivery trigger beside that person's actual world position.
            Collider trigger = FindBestTrigger(animator.transform, triggers);
            NpcAnimationTriggerRelay relay = trigger != null
                ? trigger.gameObject.GetComponent<NpcAnimationTriggerRelay>() ?? trigger.gameObject.AddComponent<NpcAnimationTriggerRelay>()
                : null;
            if (relay != null) relay.AddListener(npc);
            else
                Debug.LogWarning($"No delivery trigger was found near '{animator.name}'. The character will remain in its ambient animation.", animator);
        }
    }

    private static void ConfigureByName(NpcMissionAnimator npc, string hierarchyName, NpcAnimationLibrary library)
    {
        string name = hierarchyName.ToLowerInvariant();
        if (name.Contains("lawrence"))
            npc.Configure(NpcMissionAnimator.AmbientBehaviour.LawrenceSitting, library.defaultIdle, library.lawrenceSit, library.lawrenceSitToStand);
        else if (name.Contains("carraway"))
            npc.Configure(NpcMissionAnimator.AmbientBehaviour.LoopingAction, library.defaultIdle, library.carrawayPullPlant, null);
        else if (name.Contains("eric"))
            npc.Configure(NpcMissionAnimator.AmbientBehaviour.LoopingAction, library.defaultIdle, library.ericJumpingJacks, null);
        else if (name.Contains("monk") || name.Contains("temple"))
            npc.Configure(NpcMissionAnimator.AmbientBehaviour.LoopingAction, library.defaultIdle, library.templePraying, null);
        else
            npc.Configure(NpcMissionAnimator.AmbientBehaviour.Idle, library.defaultIdle, library.defaultIdle, null);
    }

    private static List<Collider> FindDeliveryTriggers()
    {
        List<Collider> result = new List<Collider>();
        foreach (Collider collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!collider.isTrigger) continue;
            if (collider.GetComponent<DeliveryPickupZone>() != null || collider.GetComponent<DeliveryDropoffZone>() != null)
                result.Add(collider);
        }
        return result;
    }

    private static Collider FindBestTrigger(Transform person, List<Collider> triggers)
    {
        Collider best = null;
        float bestScore = float.PositiveInfinity;
        foreach (Collider trigger in triggers)
        {
            float score = (trigger.ClosestPoint(person.position) - person.position).sqrMagnitude;
            if (score < bestScore)
            {
                bestScore = score;
                best = trigger;
            }
        }
        return best;
    }

    private static string BuildHierarchyName(Transform current, Transform stopAt)
    {
        string result = current.name;
        for (Transform parent = current.parent; parent != null && parent != stopAt; parent = parent.parent)
            result = parent.name + "/" + result;
        return result;
    }
}
