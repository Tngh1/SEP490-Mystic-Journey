using UnityEngine;

namespace NavMeshPlus.Extensions
{
    class AgentRotateSmooth2d: MonoBehaviour
    {
        public float angularSpeed;
        private AgentOverride2d override2D;

        // Performs startup initialization for AgentRotateSmooth2d on the first active frame.
        // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
        private void Start()
        {
            override2D = GetComponent<AgentOverride2d>();
            override2D.agentOverride = new RotateAgentSmoothly(override2D.Agent, override2D, angularSpeed);
        }
    }
}
