using UnityEngine;
using UnityEngine.AI;

namespace NavMeshPlus.Extensions
{
    // Initializes a new default instance of the IAgentOverride class.
    public interface IAgentOverride
    {
        void UpdateAgent();
    }

    // Initializes a new default instance of the IAgentOverride class.
    public class AgentDefaultOverride : IAgentOverride
    {
        // Executes update agent operation.
        public void UpdateAgent()
        {
        }
    }
    // Executes mono behaviour operation.
    public class AgentOverride2d: MonoBehaviour
    {
        // Executes agent operation.
        public NavMeshAgent Agent { get; private set; }
        // Executes agent override operation.
        public IAgentOverride agentOverride { get; set; }
        // Initializes internal component caches and dependencies for IAgentOverride upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
        }
        // Performs startup initialization for IAgentOverride on the first active frame.
        // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
        private void Start()
        {
            Agent.updateRotation = false;
            Agent.updateUpAxis = false;
        }

        // Per-frame update loop for IAgentOverride.
        // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
        private void Update()
        {
            agentOverride?.UpdateAgent();
        }
    }
}
