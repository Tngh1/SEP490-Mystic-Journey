using UnityEngine;
using UnityEngine.AI;

// Executes mono behaviour operation.
public class WaypointMover : MonoBehaviour
{
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float arrivalDistance = 0.5f;

    private NavMeshAgent navMeshAgent;
    private int currentWaypointIndex = 0;

    // Initializes internal component caches and dependencies for WaypointMover upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    // Performs startup initialization for WaypointMover on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (waypoints.Length > 0)
            navMeshAgent.SetDestination(waypoints[0].position);
    }

    // Per-frame update loop for WaypointMover.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (waypoints.Length == 0) return;

        if (!navMeshAgent.pathPending &&
            navMeshAgent.remainingDistance <= arrivalDistance)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex < waypoints.Length)
                navMeshAgent.SetDestination(waypoints[currentWaypointIndex].position);
            else
                navMeshAgent.ResetPath();
        }
    }
}
