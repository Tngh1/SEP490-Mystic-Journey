using UnityEngine;
using TMPro;

namespace MysticJourney.UI.Effects
{
    public class UIWaypointPointer : MonoBehaviour
    {
        public SpriteRenderer arrowRenderer;
        public TextMesh distanceLabel;
        public float radius = 2.5f;

        private Transform target;
        private Transform player;

        public void Setup(Transform targetTransform, Transform playerTransform)
        {
            target = targetTransform;
            player = playerTransform;

            // Clear() tắt GameObject; nếu không bật lại ở đây thì LateUpdate sẽ không bao
            // giờ chạy lại và mũi tên mất vĩnh viễn sau lần Clear đầu tiên. LateUpdate tự
            // ẩn lại nếu target/player null hoặc đã tới đủ gần.
            if (target != null && !gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        public void Clear()
        {
            target = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (NetworkPlayer.Local != null)
            {
                player = NetworkPlayer.Local.transform;
            }
            else if (PlayerEntity.Instance != null)
            {
                player = PlayerEntity.Instance.transform;
            }

            if (target == null || player == null)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            // Ẩn mũi tên khi đang mở bảng hội thoại NPC hoặc bảng Main Quest Panel
            if (MainNpcPanelRuntime.Instance != null && MainNpcPanelRuntime.Instance.IsOpen)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            if (MainQuestPanelRuntime.Instance != null && MainQuestPanelRuntime.Instance.gameObject.activeInHierarchy)
            {
                // Kiểm tra xem bảng QuestPanel bên trong có đang mở không
                var panelGo = MainQuestPanelRuntime.Instance.gameObject;
                if (panelGo.activeSelf)
                {
                    var childPanel = panelGo.transform.Find("QuestPanel");
                    if (childPanel != null && childPanel.gameObject.activeInHierarchy)
                    {
                        if (gameObject.activeSelf) gameObject.SetActive(false);
                        return;
                    }
                }
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

            // Đặt mũi tên quanh người chơi theo khoảng cách radius, cộng bounce dao động
            // dọc theo hướng chỉ để mũi tên "nảy" thu hút mắt.
            float bounce = Mathf.Sin(Time.time * 6f) * 0.2f;
            transform.position = player.position + worldDir * (radius + bounce);

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
            int w = 32, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color clear = Color.clear;
            Color yellow = new Color(1f, 0.85f, 0.1f, 1f); // Vàng sáng đẹp
            Color outline = new Color(0.1f, 0.1f, 0.1f, 0.9f); // Viền đen rõ nét

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, clear);

            // Vẽ hình mũi tên đầy đủ (thân + đầu tam giác) hướng LÊN trên để khớp logic
            // xoay (-90°). Đầu chiếm ~40% trên, thân là dải chữ nhật ở dưới.
            int cx = w / 2;
            int headBaseY = Mathf.RoundToInt(h * 0.42f); // ranh giới giữa thân (dưới) và đầu (trên)
            float shaftHalf = w * 0.16f;                 // nửa bề rộng thân
            float headHalf = w * 0.34f;                  // nửa bề rộng đáy đầu mũi tên

            for (int y = 0; y < h; y++)
            {
                int left, right;
                if (y <= headBaseY)
                {
                    // Thân: dải chữ nhật
                    left = Mathf.RoundToInt(cx - shaftHalf);
                    right = Mathf.RoundToInt(cx + shaftHalf);
                }
                else
                {
                    // Đầu: tam giác thu nhỏ dần tới đỉnh (y = h-1)
                    float t = 1f - ((float)(y - headBaseY) / (h - 1 - headBaseY)); // 1 tại đáy đầu -> 0 tại đỉnh
                    float halfWidth = headHalf * t;
                    left = Mathf.RoundToInt(cx - halfWidth);
                    right = Mathf.RoundToInt(cx + halfWidth);
                }

                for (int x = left; x <= right; x++)
                {
                    if (x < 0 || x >= w) continue;
                    bool isEdge = x == left || x == right || y == 0 || y == 1 || y == h - 1;
                    tex.SetPixel(x, y, isEdge ? outline : yellow);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
