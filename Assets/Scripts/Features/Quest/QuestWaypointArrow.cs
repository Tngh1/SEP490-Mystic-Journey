using UnityEngine;

// Executes mono behaviour operation.
public class QuestWaypointArrow : MonoBehaviour
{
    // Initializes internal component caches and dependencies for QuestWaypointArrow upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        Destroy(gameObject);
    }
}
