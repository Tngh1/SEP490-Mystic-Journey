using UnityEngine;

namespace MysticJourney.UI.Effects
{
    public class UIQuestPointer : MonoBehaviour
    {
        public float speed = 5f;
        public float moveAmount = 10f;
        private Vector3 originalPosition;
        private RectTransform rectTransform;
        private bool isInitialized;

        private void Awake()
        {
            Initialize();
        }

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

        private void OnEnable()
        {
            Initialize();
        }

        private void Update()
        {
            float t = Mathf.Sin(Time.unscaledTime * speed);
            Vector3 offset = new Vector3(0, t * moveAmount, 0);

            if (rectTransform != null)
                rectTransform.anchoredPosition3D = originalPosition + offset;
            else
                transform.localPosition = originalPosition + offset;
        }

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
