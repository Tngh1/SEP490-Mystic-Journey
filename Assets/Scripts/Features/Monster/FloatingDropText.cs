using TMPro;
using UnityEngine;

namespace MysticJourney.Features.Monster
{
    // Executes mono behaviour operation.
    public class FloatingDropText : MonoBehaviour
    {
        [SerializeField] private TextMeshPro tmpText;
        [SerializeField] private TextMeshProUGUI tmpTextUI;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float moveSpeed = 1.2f;
        [SerializeField] private float lifetime = 1.8f;
        [SerializeField] private float fadeDuration = 0.6f;

        private Vector3 moveDirection = Vector3.up;
        private float timer = 0f;
        private Color originalColor = Color.white;

        // Initializes internal component caches and dependencies for FloatingDropText upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            if (tmpText == null) tmpText = GetComponent<TextMeshPro>();
            if (tmpTextUI == null) tmpTextUI = GetComponent<TextMeshProUGUI>();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            ApplySilverFont();

            // Randomize the eligible candidates before selecting this gameplay result.
            float randomX = Random.Range(-0.3f, 0.3f);
            moveDirection = new Vector3(randomX, 1f, 0f).normalized;
        }

        // Executes setup operation.
        public void Setup(string text, Color color, float speedMultiplier = 1f)
        {
            ApplySilverFont();
            originalColor = color;
            moveSpeed *= speedMultiplier;

            if (tmpText != null)
            {
                tmpText.text = text;
                tmpText.color = color;
            }

            if (tmpTextUI != null)
            {
                tmpTextUI.text = text;
                tmpTextUI.color = color;
            }
        }

        // Executes apply silver font operation.
        private void ApplySilverFont()
        {
            TMP_FontAsset font = SilverFontResolver.Font;
            if (font == null) return;

            if (tmpText != null)
                tmpText.font = font;

            if (tmpTextUI != null)
                tmpTextUI.font = font;
        }

        // Per-frame update loop for FloatingDropText.
        // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
        private void Update()
        {
            timer += Time.deltaTime;

            transform.position += moveDirection * (moveSpeed * Time.deltaTime);

            float startFadeTime = lifetime - fadeDuration;
            if (timer >= startFadeTime)
            {
                // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
                float alpha = Mathf.Clamp01(1f - ((timer - startFadeTime) / fadeDuration));

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = alpha;
                }
                else if (tmpText != null)
                {
                    Color c = originalColor;
                    c.a = alpha;
                    tmpText.color = c;
                }
                else if (tmpTextUI != null)
                {
                    Color c = originalColor;
                    c.a = alpha;
                    tmpTextUI.color = c;
                }
            }

            if (timer >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
