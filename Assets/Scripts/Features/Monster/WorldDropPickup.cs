using System.Collections;
using UnityEngine;
using TMPro;

namespace MysticJourney.Features.Monster
{
    public enum DropPickupType
    {
        Gold,
        Exp,
        SkillUpgradeStone,
        Item
    }

    /// <summary>
    /// Prefab/GameObject rơi ra môi trường thế giới (World Map) khi đánh bại quái vật.
    /// Có hiệu ứng nảy (pop/bounce), xoay/bay bổng (floating), hút về phía người chơi (magnet),
    /// và khi chạm vào người chơi mới thu thập và hiển thị số lượng.
    /// </summary>
    public class WorldDropPickup : MonoBehaviour
    {
        [Header("Drop Info")]
        public DropPickupType dropType;
        public string itemName;
        public float amount;
        public Color glowColor = Color.yellow;

        [Header("Settings")]
        [SerializeField] private float magnetDistance = 9999f; // Bay trực tiếp về người chơi ngay lập tức
        [SerializeField] private float collectDistance = 0.5f; // Khoảng cách thực sự nhặt vật phẩm
        [SerializeField] private float magnetSpeed = 12.0f;    // Tốc độ bay mượt về người chơi

        private SpriteRenderer _spriteRenderer;
        private Vector3 _landPosition;
        private bool _isSpawning = true;
        private bool _isBeingMagnetized = true;
        private float _bobTimer = 0f;
        private Transform _playerTransform;

        public void Setup(DropPickupType type, string name, float qty, Sprite customSprite, Color color, Vector3 targetLandPos)
        {
            dropType = type;
            itemName = name;
            amount = qty;
            glowColor = color;
            _landPosition = targetLandPos;
            _isBeingMagnetized = true;

            // Scale down physical drop to neat world size (0.35 world units)
            transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            EnsureComponents();

            if (_spriteRenderer != null)
            {
                if (customSprite != null)
                {
                    _spriteRenderer.sprite = customSprite;
                }
                else
                {
                    _spriteRenderer.sprite = CreateProceduralSprite(type);
                }

                _spriteRenderer.color = Color.white;
                _spriteRenderer.sortingOrder = 35; // Render neatly above ground and grass
            }

            StartCoroutine(PopAnimationSequence(targetLandPos));
        }

        private void EnsureComponents()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                var circle = gameObject.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                circle.radius = 0.5f;
            }
        }

        private IEnumerator PopAnimationSequence(Vector3 landPos)
        {
            _isSpawning = true;
            Vector3 startPos = transform.position;
            float duration = 0.15f;
            float elapsed = 0f;
            float arcHeight = 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                Vector3 currentPos = Vector3.Lerp(startPos, landPos, t);
                currentPos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                transform.position = currentPos;
                yield return null;
            }

            transform.position = landPos;
            _isSpawning = false;
        }

        private bool _isCollected = false;

        private void Update()
        {
            if (_isSpawning || _isCollected) return;

            FindPlayer();

            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);

                if (dist <= magnetDistance)
                {
                    _isBeingMagnetized = true;
                }

                if (_isBeingMagnetized)
                {
                    // Bay mượt về phía người chơi
                    Vector3 targetPos = _playerTransform.position + Vector3.up * 0.5f;
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, magnetSpeed * Time.deltaTime);

                    // Khi đến gần người chơi -> Nhặt thành công!
                    if (Vector3.Distance(transform.position, targetPos) <= collectDistance)
                    {
                        CollectItem();
                        return;
                    }
                }
            }

            // Hiệu ứng nhấp nhô bồng bềnh nhẹ trên mặt đất khi chưa bị hút
            if (!_isBeingMagnetized)
            {
                _bobTimer += Time.deltaTime * 3f;
                float offset = Mathf.Sin(_bobTimer) * 0.05f;
                transform.position = new Vector3(_landPosition.x, _landPosition.y + offset, _landPosition.z);
            }
        }

        private void FindPlayer()
        {
            if (_playerTransform != null && _playerTransform.gameObject.activeInHierarchy) return;

            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
            }
        }

        private void CollectItem()
        {
            if (_isCollected) return;
            _isCollected = true;

            // Disable visuals and collider immediately on pickup so it disappears visually
            if (_spriteRenderer != null) _spriteRenderer.enabled = false;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            // Hiển thị text cộng điểm/vàng/đá trên đầu người chơi khi vừa nhặt xong
            string text = "";
            switch (dropType)
            {
                case DropPickupType.Gold:
                    text = $"+{amount:N0} Gold";
                    break;
                case DropPickupType.Exp:
                    text = $"+{amount:N0} EXP";
                    break;
                case DropPickupType.SkillUpgradeStone:
                    text = $"+{amount} Skill Upgrade Stone";
                    break;
                case DropPickupType.Item:
                    text = amount > 1 ? $"+{amount}x {itemName}" : $"+{itemName}";
                    break;
            }

            // Gọi MonsterDropVisualManager để hiển thị floating text số tiền/đá vừa thu thập
            if (MonsterDropVisualManager.Instance != null)
            {
                MonsterDropVisualManager.Instance.SpawnFloatingTextDirect(transform.position, text, glowColor);
            }

            // Server đã cộng vàng/exp/vật phẩm trong transaction của /monsters/{id}/defeat.
            // Ở đây chỉ đọc lại số liệu để UI khớp — không gửi gì lên nữa, vì client không có
            // thẩm quyền quyết định phần thưởng.
            if (dropType == DropPickupType.Gold || dropType == DropPickupType.Exp)
            {
                // RefreshHUD (không phải ForceRefreshHUD) để cờ _isRefreshing gộp được các
                // pickup rơi liên tiếp thành một lần gọi API.
                if (PlayerHUDController.Instance != null)
                {
                    PlayerHUDController.Instance.RefreshHUD();
                }
            }
            else
            {
                InventoryManager.RefreshAny(refreshStats: true);

                var skillPanel = FindFirstObjectByType<SkillPanelManager>(FindObjectsInactive.Include);
                if (skillPanel != null)
                {
                    skillPanel.RefreshStoneCount();
                }

                var skillPopup = FindFirstObjectByType<SkillPopup>(FindObjectsInactive.Include);
                if (skillPopup != null && skillPopup.gameObject.activeInHierarchy)
                {
                    skillPopup.AutoBindComponents();
                }
            }

            Destroy(gameObject);
        }

        private Sprite CreateProceduralSprite(DropPickupType type)
        {
            int res = 32;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            Color[] colors = new Color[res * res];
            Vector2 center = new Vector2(res / 2f, res / 2f);
            float radius = res / 2.5f;

            Color baseCol = glowColor;
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        float alpha = Mathf.Clamp01(1f - (dist / radius) * 0.4f);
                        colors[y * res + x] = new Color(baseCol.r, baseCol.g, baseCol.b, alpha);
                    }
                    else
                    {
                        colors[y * res + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 32);
        }
    }
}
