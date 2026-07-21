using UnityEngine;

namespace MysticJourney.UI.Effects
{
    public class UIHighlightPulse : MonoBehaviour
    {
        public float speed = 4f;
        public float scaleAmount = 0.1f;
        private Vector3 originalScale;
        private bool isInitialized;

        private void Awake()
        {
            if (!isInitialized)
            {
                originalScale = transform.localScale;
                isInitialized = true;
            }
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                originalScale = transform.localScale;
                isInitialized = true;
            }
        }

        private void Update()
        {
            float t = Mathf.PingPong(Time.unscaledTime * speed, 1f);
            transform.localScale = originalScale * (1f + t * scaleAmount);
        }

        private void OnDisable()
        {
            if (isInitialized)
            {
                transform.localScale = originalScale;
            }
        }
    }
}
