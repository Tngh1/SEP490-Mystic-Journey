using TMPro;
using UnityEngine;

namespace MysticJourney.Features.Monster
{
    /// <summary>
    /// Hiệu ứng Floating Text cho vật phẩm, vàng, và kinh nghiệm rớt ra khi quái bị tiêu diệt.
    /// </summary>
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

        private void Awake()
        {
            if (tmpText == null) tmpText = GetComponent<TextMeshPro>();
            if (tmpTextUI == null) tmpTextUI = GetComponent<TextMeshProUGUI>();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            ApplySilverFont();

            // Random nhẹ hướng bay nghiêng để nhiều dòng không bị đè hoàn toàn lên nhau
            float randomX = Random.Range(-0.3f, 0.3f);
            moveDirection = new Vector3(randomX, 1f, 0f).normalized;
        }

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

        private void ApplySilverFont()
        {
            TMP_FontAsset font = SilverFontResolver.Font;
            if (font == null) return;

            if (tmpText != null)
                tmpText.font = font;

            if (tmpTextUI != null)
                tmpTextUI.font = font;
        }

        private void Update()
        {
            timer += Time.deltaTime;

            // Di chuyển lên trên
            transform.position += moveDirection * (moveSpeed * Time.deltaTime);

            // Mờ dần về cuối vòng đời
            float startFadeTime = lifetime - fadeDuration;
            if (timer >= startFadeTime)
            {
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

            // Hủy object sau khi hết thời gian
            if (timer >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
