using UnityEngine;
using TMPro;

namespace MysticJourney.UI.Effects
{
    public class UIWaypointPointer : MonoBehaviour
    {
        public SpriteRenderer arrowRenderer;
        public TextMesh distanceLabel;
        public float radius = 1.5f;

        private Transform target;
        private Transform player;

        public void Setup(Transform targetTransform, Transform playerTransform)
        {
            target = targetTransform;
            player = playerTransform;
        }

        public void Clear()
        {
            target = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (target == null || player == null)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            float dist = Vector2.Distance(player.position, target.position);

            if (dist < 1.5f)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            // Hướng từ người chơi đến mục tiêu
            Vector3 worldDir = (target.position - player.position).normalized;

            // Đặt mũi tên quanh người chơi theo khoảng cách radius
            transform.position = player.position + worldDir * radius;

            // Xoay mũi tên hướng đến mục tiêu (sprite mặc định hướng lên - up)
            float angle = Mathf.Atan2(worldDir.y, worldDir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            if (distanceLabel != null)
            {
                distanceLabel.text = $"{Mathf.RoundToInt(dist)}m";
                // Giữ chữ luôn nằm ngang
                distanceLabel.transform.rotation = Quaternion.identity;
                distanceLabel.transform.position = transform.position + new Vector3(0.6f, 0, 0);
            }
        }

        /// <summary>
        /// Tạo Sprite mũi tên tam giác thon gọn bằng code (hướng lên trên)
        /// </summary>
        public static Sprite CreateArrowSprite()
        {
            int w = 64, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color clear = Color.clear;
            Color yellow = new Color(1f, 0.85f, 0.1f, 1f); // Vàng sáng đẹp
            Color outline = new Color(0.1f, 0.1f, 0.1f, 0.9f); // Viền đen rõ nét

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, clear);

            // Vẽ tam giác thon nhọn hướng lên trên (y = h là đỉnh, y = 0 là đáy)
            for (int y = 0; y < h; y++)
            {
                float norm = 1.0f - ((float)y / (h - 1)); // y = h-1 -> norm = 0 (đỉnh), y = 0 -> norm = 1 (đáy)
                float halfWidth = norm * (w * 0.25f); // Bề ngang thon gọn
                int cx = w / 2;
                int left = Mathf.RoundToInt(cx - halfWidth);
                int right = Mathf.RoundToInt(cx + halfWidth);
                for (int x = left; x <= right; x++)
                {
                    if (x < 0 || x >= w) continue;
                    bool isEdge = x == left || x == right || y == 0 || y == 1 || y == h - 1 || y == h - 2;
                    tex.SetPixel(x, y, isEdge ? outline : yellow);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
