using UnityEngine;

/// <summary>
/// DEPRECATED: Duplicate arrow script disabled in favor of single unified QuestWaypointManager.
/// </summary>
public class QuestWaypointArrow : MonoBehaviour
{
    private void Awake()
    {
        // Immediately destroy duplicate arrow object to keep QuestWaypointManager as sole arrow provider
        Destroy(gameObject);
    }
}
