using System.Collections;
using UnityEngine;
using UnityEngine.AI;

//***********************************************************************************
// Contributed by author @Lazy_Sloth from unity forum (https://forum.unity.com/)
//***********************************************************************************
namespace NavMeshPlus.Extensions
{
    // Executes i agent override operation.
    public class RotateAgentSmoothly: IAgentOverride
    {
        // Initializes a new instance of RotateAgentSmoothly with dependencies: agent, owner, rotateSpeed.
        // Assigns injected service and configuration instances to readonly fields for runtime operations.
        public RotateAgentSmoothly(NavMeshAgent agent, AgentOverride2d owner, float rotateSpeed)
        {
            this.agent = agent;
            this.owner = owner;
            this.rotateSpeed = rotateSpeed;
        }

        private NavMeshAgent agent;
        private AgentOverride2d owner;
        private Vector2 nextWaypoint;
        private float angleDifference;
        private float targetAngle;
        public float rotateSpeed;

        // Executes update agent operation.
        public void UpdateAgent()
        {
            if (agent.hasPath && agent.path.corners.Length > 1)
            {
                if (nextWaypoint != (Vector2)agent.path.corners[1])
                {
                    owner.StartCoroutine(_RotateCoroutine());
                    nextWaypoint = agent.path.corners[1];
                }
            }
        }
        // Executes _rotate coroutine operation.
        protected IEnumerator _RotateCoroutine()
        {
            yield return RotateToWaypoints(agent.transform);
        }
        // Executes rotate to waypoints operation.
        protected IEnumerator RotateToWaypoints(Transform transform)
        {
            Vector2 targetVector = agent.path.corners[1] - transform.position;
            angleDifference = Vector2.SignedAngle(transform.up, targetVector);
            targetAngle = transform.localEulerAngles.z + angleDifference;

            if (targetAngle >= 360) { targetAngle -= 360; }
            else if (targetAngle < 0) { targetAngle += 360; }

            while (transform.localEulerAngles.z < targetAngle - 0.1f || transform.localEulerAngles.z > targetAngle + 0.1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, 0, targetAngle), rotateSpeed * Time.deltaTime);
                yield return null;
            }
        }
    }
}