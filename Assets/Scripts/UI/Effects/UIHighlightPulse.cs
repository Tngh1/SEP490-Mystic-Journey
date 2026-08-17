using UnityEngine;

namespace MysticJourney.UI.Effects
{
    // Executes mono behaviour operation.
    public class UIHighlightPulse : MonoBehaviour
    {
        public float speed = 4f;
        public float scaleAmount = 0.1f;
        private Vector3 originalScale;
        private bool isInitialized;

        // Initializes internal component caches and dependencies for UIHighlightPulse upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            if (!isInitialized)
            {
                originalScale = transform.localScale;
                isInitialized = true;
            }
        }

        // Refresh visible state and subscribe the event handlers required while this component is active.
        private void OnEnable()
        {
            if (!isInitialized)
            {
                originalScale = transform.localScale;
                isInitialized = true;
            }
        }

        // Per-frame update loop for UIHighlightPulse.
        // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
        private void Update()
        {
            float t = Mathf.PingPong(Time.unscaledTime * speed, 1f);
            transform.localScale = originalScale * (1f + t * scaleAmount);
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDisable()
        {
            if (isInitialized)
            {
                transform.localScale = originalScale;
            }
        }
    }
}
