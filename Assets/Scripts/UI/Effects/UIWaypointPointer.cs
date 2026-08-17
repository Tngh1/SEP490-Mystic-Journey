using UnityEngine;
using TMPro;

namespace MysticJourney.UI.Effects
{
    // Executes mono behaviour operation.
    public class UIWaypointPointer : MonoBehaviour
    {
        public SpriteRenderer arrowRenderer;
        public TextMesh distanceLabel;
        public float radius = 2.5f;

        private const float HideDistance = 1.5f;
        private const float TargetClearance = 1.2f;

        private Transform target;
        private Transform player;

        // Executes setup operation.
        public void Setup(Transform targetTransform, Transform playerTransform)
        {
            target = targetTransform;
            player = playerTransform;

            if (target != null && !gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        // Executes clear operation.
        public void Clear()
        {
            target = null;
            gameObject.SetActive(false);
        }

        private int m_LastDistInt = -1;

        // Executes late update operation.
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

            if (MainNpcPanel.Instance != null && MainNpcPanel.Instance.IsOpen)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            if (MainQuestPanelRuntime.Instance != null && MainQuestPanelRuntime.Instance.gameObject.activeInHierarchy)
            {
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

            if (dist < HideDistance)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            Vector3 worldDir = (target.position - player.position).normalized;

            float bounce = Mathf.Sin(Time.time * 6f) * 0.2f;

            float arrowDist = Mathf.Min(radius + bounce, dist - TargetClearance);
            transform.position = player.position + worldDir * arrowDist;

            float angle = Mathf.Atan2(worldDir.y, worldDir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            if (distanceLabel != null)
            {
                int roundedDist = Mathf.RoundToInt(dist);
                if (m_LastDistInt != roundedDist)
                {
                    m_LastDistInt = roundedDist;
                    distanceLabel.text = $"{roundedDist}m";
                }
                distanceLabel.transform.rotation = Quaternion.identity;
                distanceLabel.transform.position = transform.position + new Vector3(0.6f, 0, 0);
            }
        }

        // Executes create arrow sprite operation.
        public static Sprite CreateArrowSprite()
        {
            int w = 32, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color clear = Color.clear;
            Color yellow = new Color(1f, 0.85f, 0.1f, 1f);
            Color outline = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, clear);

            int cx = w / 2;
            int headBaseY = Mathf.RoundToInt(h * 0.42f);
            float shaftHalf = w * 0.16f;
            float headHalf = w * 0.34f;

            for (int y = 0; y < h; y++)
            {
                int left, right;
                if (y <= headBaseY)
                {
                    left = Mathf.RoundToInt(cx - shaftHalf);
                    right = Mathf.RoundToInt(cx + shaftHalf);
                }
                else
                {
                    float t = 1f - ((float)(y - headBaseY) / (h - 1 - headBaseY));
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
