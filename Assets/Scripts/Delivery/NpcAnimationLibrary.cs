using UnityEngine;

[CreateAssetMenu(fileName = "Npc Animation Library", menuName = "Crumbia/NPC Animation Library")]
public sealed class NpcAnimationLibrary : ScriptableObject
{
    public AnimationClip defaultIdle;
    public AnimationClip lawrenceSit;
    public AnimationClip lawrenceSitToStand;
    public AnimationClip carrawayPullPlant;
    public AnimationClip ericJumpingJacks;
    public AnimationClip templePraying;
}
