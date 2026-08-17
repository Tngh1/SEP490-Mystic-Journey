using UnityEngine;

// Executes mono behaviour operation.
public sealed class PlayerSkillVisualReplica : MonoBehaviour
{
    // Executes owner operation.
    public Transform Owner { get; private set; }

    // Executes mark operation.
    public static PlayerSkillVisualReplica Mark(GameObject instance, Transform owner)
    {
        var marker = instance.GetComponent<PlayerSkillVisualReplica>();
        if (marker == null) marker = instance.AddComponent<PlayerSkillVisualReplica>();
        marker.Owner = owner;
        return marker;
    }

    // Executes is replica operation.
    public static bool IsReplica(Component component) =>
        component != null && component.GetComponentInParent<PlayerSkillVisualReplica>() != null;

    // Executes get owner operation.
    public static Transform GetOwner(Component component) =>
        component != null
            ? component.GetComponentInParent<PlayerSkillVisualReplica>()?.Owner
            : null;
}
