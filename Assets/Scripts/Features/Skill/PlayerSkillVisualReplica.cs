using UnityEngine;

/// <summary>
/// Marks a locally-instantiated copy of another player's legacy skill prefab.
/// The copy may animate and move, but must never apply gameplay effects.
/// </summary>
public sealed class PlayerSkillVisualReplica : MonoBehaviour
{
    public Transform Owner { get; private set; }

    public static PlayerSkillVisualReplica Mark(GameObject instance, Transform owner)
    {
        var marker = instance.GetComponent<PlayerSkillVisualReplica>();
        if (marker == null) marker = instance.AddComponent<PlayerSkillVisualReplica>();
        marker.Owner = owner;
        return marker;
    }

    public static bool IsReplica(Component component) =>
        component != null && component.GetComponentInParent<PlayerSkillVisualReplica>() != null;

    public static Transform GetOwner(Component component) =>
        component != null
            ? component.GetComponentInParent<PlayerSkillVisualReplica>()?.Owner
            : null;
}
