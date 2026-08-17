using UnityEngine;

namespace NavMeshPlus.Extensions
{
    // Executes mono behaviour operation.
    public class AgentRotate2d: MonoBehaviour
    {
        private AgentOverride2d override2D;
        // Performs startup initialization for AgentRotate2d on the first active frame.
        // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
        private void Start()
        {
            override2D = GetComponent<AgentOverride2d>();
            override2D.agentOverride = new RotateAgentInstantly(override2D.Agent, override2D);
        }

    }
}
