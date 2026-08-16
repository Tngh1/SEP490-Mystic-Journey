using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private float collectDistance = 0.5f; // Khoảng cách thực sự nhặt vật phẩm
        [SerializeField] private float magnetSpeed = 12.0f;    // Tốc độ bay mượt về người chơi

        private SpriteRenderer _spriteRenderer;
        private Vector3 _landPosition;
        private bool _isSpawning = true;
        private bool _isBeingMagnetized = false;
        private float magnetDelayTimer = 0.5f; // Chờ 0.5s trên mặt đất trước khi tự động hút
        private float _bobTimer = 0f;
        private Transform _playerTransform;

        public void Setup(DropPickupType type, string name, float qty, Sprite customSprite, Color color, Vector3 targetLandPos)
        {
            dropType = type;
            itemName = name;
            amount = qty;
            glowColor = color;
            _landPosition = targetLandPos;
            _isBeingMagnetized = false;
            magnetDelayTimer = 0.5f;

            // Scale vật phẩm rõ ràng trên bản đồ (0.5 world units)
            transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            EnsureComponents();

            if (_spriteRenderer != null)
            {
                if (customSprite != null)
                {
                    _spriteRenderer.sprite = customSprite;
                }
                else
                {
                    _spriteRenderer.sprite = GetProceduralSprite(type);
                }

                _spriteRenderer.color = Color.white;
                
                // Đảm bảo nổi hoàn toàn lên trên các lớp Tilemap / đất / cỏ
                try
                {
                    _spriteRenderer.sortingLayerName = "Units";
                }
                catch
                {
                    // Fallback nếu layer Units chưa khai báo
                }
                _spriteRenderer.sortingOrder = 100;
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

            // Không thêm Collider2D: việc nhặt xét bằng khoảng cách (collectDistance), không có
            // OnTriggerEnter2D nào ở đây. Collider tĩnh mà di chuyển mỗi frame buộc physics
            // rebuild broadphase, và còn lọt vào các OverlapCircleAll không dùng layer mask
            // của skill (LightsaberSkill, ProtectiveShieldSkill...) thành hit rác.
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

            // 1. Giai đoạn chờ trên mặt đất (0.5s): Vật phẩm bồng bềnh nhẹ để mắt người chơi nhận biết
            if (magnetDelayTimer > 0f)
            {
                magnetDelayTimer -= Time.deltaTime;
                _bobTimer += Time.deltaTime * 3f;
                float offset = Mathf.Sin(_bobTimer) * 0.05f;
                transform.position = new Vector3(_landPosition.x, _landPosition.y + offset, _landPosition.z);
                return;
            }

            // 2. Giai đoạn tự động hút về phía người chơi
            FindPlayer();

            if (_playerTransform != null)
            {
                _isBeingMagnetized = true;

                // Bay mượt về phía người chơi
                Vector3 targetPos = _playerTransform.position + Vector3.up * 0.5f;
                transform.position = Vector3.MoveTowards(transform.position, targetPos, magnetSpeed * Time.deltaTime);

                // Khi đến gần người chơi -> Nhặt thành công!
                // So sánh bình phương để tránh sqrt cho từng drop ở mỗi frame.
                if ((transform.position - targetPos).sqrMagnitude <= collectDistance * collectDistance)
                {
                    CollectItem();
                    return;
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

        // Player được share giữa mọi drop: ưu tiên NetworkPlayer.Local (nguồn local-player
        // chuẩn); FindWithTag chỉ còn fallback có throttle khi player chưa spawn xong.
        private static Transform _sharedPlayer;
        private static int _nextPlayerSearchFrame;

        private void FindPlayer()
        {
            if (_playerTransform != null && _playerTransform.gameObject.activeInHierarchy) return;

            if (_sharedPlayer != null && _sharedPlayer.gameObject.activeInHierarchy)
            {
                _playerTransform = _sharedPlayer;
                return;
            }

            // NetworkPlayer.Local là nguồn local-player chuẩn; FindWithTag chỉ là fallback khi
            // player đang được spawn. Giới hạn fallback còn 4 lần/giây thay vì mỗi frame.
            if (NetworkPlayer.Local != null && NetworkPlayer.Local.gameObject.activeInHierarchy)
            {
                _sharedPlayer = NetworkPlayer.Local.transform;
                _playerTransform = _sharedPlayer;
                return;
            }

            // The visual player can be spawned before NetworkPlayer.Local is
            // published (and some offline/test sessions have no network player
            // at all). PlayerEntity is the stable local-player anchor in both
            // paths, so drops must use it as a fallback instead of remaining on
            // the ground forever.
            if (PlayerEntity.Instance != null && PlayerEntity.Instance.gameObject.activeInHierarchy)
            {
                _sharedPlayer = PlayerEntity.Instance.transform;
                _playerTransform = _sharedPlayer;
                return;
            }

            if (Time.frameCount < _nextPlayerSearchFrame) return;
            _nextPlayerSearchFrame = Time.frameCount + 15;

            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _sharedPlayer = player.transform;
                _playerTransform = _sharedPlayer;
                return;
            }

            var entity = Object.FindFirstObjectByType<PlayerEntity>();
            if (entity != null && entity.gameObject.activeInHierarchy)
            {
                _sharedPlayer = entity.transform;
                _playerTransform = _sharedPlayer;
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
            //
            // Một con quái rơi nhiều món và cả loạt được hút về người chơi trong vài frame, nên
            // refresh ngay tại đây = N lần gọi API + N lần rebuild UI dồn vào một chỗ (đo được
            // ~1.6ms chỉ riêng 2 FindFirstObjectByType mỗi món). Gộp về 1 lần qua manager.
            MonsterDropVisualManager.Instance.RequestRewardRefresh(
                inventoryAndSkill: dropType != DropPickupType.Gold && dropType != DropPickupType.Exp);

            Destroy(gameObject);
        }

        // Sprite dự phòng khi không tìm được icon: cache theo type vì Destroy(gameObject) KHÔNG
        // giải phóng Texture2D tạo bằng new — mỗi drop trước đây rò rỉ một texture 32x32.
        private static readonly Dictionary<DropPickupType, Sprite> _proceduralCache =
            new Dictionary<DropPickupType, Sprite>();

        private Sprite GetProceduralSprite(DropPickupType type)
        {
            if (_proceduralCache.TryGetValue(type, out var cached) && cached != null)
                return cached;

            var sprite = CreateProceduralSprite(type);
            _proceduralCache[type] = sprite;
            return sprite;
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
