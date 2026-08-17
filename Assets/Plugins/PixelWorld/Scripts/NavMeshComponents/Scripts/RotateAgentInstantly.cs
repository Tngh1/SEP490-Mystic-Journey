using UnityEngine;
using UnityEngine.AI;

//***********************************************************************************
// Contributed by author @Lazy_Sloth from unity forum (https://forum.unity.com/)
//***********************************************************************************
namespace NavMeshPlus.Extensions
{
    // Executes i agent override operation.
    public class RotateAgentInstantly: IAgentOverride
    {

        // Initializes a new instance of RotateAgentInstantly with dependencies: agent, owner.
        // Assigns injected service and configuration instances to readonly fields for runtime operations.
        public RotateAgentInstantly(NavMeshAgent agent, AgentOverride2d owner)
        {
            this.agent = agent;
            this.owner = owner;
        }
        private NavMeshAgent agent;
        private AgentOverride2d owner;
        private Vector3 nextWaypoint;

        // Executes update agent operation.
        public void UpdateAgent()
        {
            if (agent.hasPath && agent.path.corners.Length > 1)
            {
                if (nextWaypoint != agent.path.corners[1])
                {
                    RotateToPoint(agent.path.corners[1], agent.transform);
                    nextWaypoint = agent.path.corners[1];
                }
            }
        }

        // Executes rotate to point operation.
        private static void RotateToPoint(Vector3 targetPoint, Transform transform)
        {
            Vector3 targetVector = targetPoint - transform.position;
            float angleDifference = Vector2.SignedAngle(transform.up, targetVector);
            transform.rotation = Quaternion.Euler(0, 0, transform.localEulerAngles.z + angleDifference);
        }
    }
}