using UnityEngine;

namespace MysticJourney.UI.Effects
{
    // Executes mono behaviour operation.
    public class UIQuestPointer : MonoBehaviour
    {
        public float speed = 5f;
        public float moveAmount = 10f;
        private Vector3 originalPosition;
        private RectTransform rectTransform;
        private bool isInitialized;

        // Initializes internal component caches and dependencies for UIQuestPointer upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            Initialize();
        }

        // Executes initialize operation.
        private void Initialize()
        {
            if (isInitialized) return;
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
                originalPosition = rectTransform.anchoredPosition3D;
            else
                originalPosition = transform.localPosition;
            isInitialized = true;
        }

        // Refresh visible state and subscribe the event handlers required while this component is active.
        private void OnEnable()
        {
            Initialize();
        }

        // Per-frame update loop for UIQuestPointer.
        // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
        private void Update()
        {
            float t = Mathf.Sin(Time.unscaledTime * speed);
            Vector3 offset = new Vector3(0, t * moveAmount, 0);

            if (rectTransform != null)
                rectTransform.anchoredPosition3D = originalPosition + offset;
            else
                transform.localPosition = originalPosition + offset;
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDisable()
        {
            if (isInitialized)
            {
                if (rectTransform != null)
                    rectTransform.anchoredPosition3D = originalPosition;
                else
                    transform.localPosition = originalPosition;
            }
        }
    }
}
