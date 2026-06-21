using UnityEngine;
using UnityEngine.AI;

public class WaypointMover : MonoBehaviour
{
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float arrivalDistance = 0.5f;

    private NavMeshAgent navMeshAgent;
    private int currentWaypointIndex = 0;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (waypoints.Length > 0)
            navMeshAgent.SetDestination(waypoints[0].position);
    }

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
