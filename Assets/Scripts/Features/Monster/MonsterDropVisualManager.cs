using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;

namespace MysticJourney.Features.Monster
{
    /// <summary>
    /// Quản lý việc hiển thị hiệu ứng rớt Vàng, EXP và Vật phẩm tại vị trí quái chết khi nhận thông tin từ Server.
    /// </summary>
    public class MonsterDropVisualManager : MonoBehaviour
    {
        private static MonsterDropVisualManager _instance;
        public static MonsterDropVisualManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MonsterDropVisualManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[MonsterDropVisualManager]");
                        _instance = go.AddComponent<MonsterDropVisualManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("Prefab & Styling Options")]
        [SerializeField] private GameObject dropTextPrefab;
        [SerializeField] private Color expColor = new Color(0.3f, 0.85f, 1f);      // Cyan / Blue
        [SerializeField] private Color goldColor = new Color(1f, 0.85f, 0.15f);    // Gold / Yellow
        [SerializeField] private Color gemColor = new Color(0.85f, 0.35f, 1f);     // Magenta / Gem Purple
        [SerializeField] private Color itemColor = new Color(0.4f, 0.95f, 0.45f);   // Green

        // Lưu trữ vị trí quái chết gần nhất theo monsterId
        private readonly Dictionary<int, Vector3> _recentDeathPositions = new Dictionary<int, Vector3>();
        private readonly Queue<Vector3> _fallbackPositions = new Queue<Vector3>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (MonsterManager.Instance != null)
            {
                MonsterManager.Instance.OnMonsterDefeated += HandleMonsterDefeated;
            }
        }

        private void OnDisable()
        {
            if (MonsterManager.Instance != null)
            {
                MonsterManager.Instance.OnMonsterDefeated -= HandleMonsterDefeated;
            }
        }

        private void Start()
        {
            // Đảm bảo đã subscribe sự kiện OnMonsterDefeated nếu MonsterManager khởi tạo sau
            if (MonsterManager.Instance != null)
            {
                MonsterManager.Instance.OnMonsterDefeated -= HandleMonsterDefeated;
                MonsterManager.Instance.OnMonsterDefeated += HandleMonsterDefeated;
            }
        }

        /// <summary>
        /// Được gọi bởi EnemyEntity ngay khi quái vừa chết để lưu lại vị trí không gian (Vector3).
        /// </summary>
        public void RegisterMonsterDeathPosition(int monsterId, Vector3 deathPosition)
        {
            if (monsterId > 0)
            {
                _recentDeathPositions[monsterId] = deathPosition;
            }
            
            _fallbackPositions.Enqueue(deathPosition);
            if (_fallbackPositions.Count > 10)
            {
                _fallbackPositions.Dequeue();
            }
        }

        /// <summary>
        /// Xử lý dữ liệu trả về từ backend sau khi hạ quái.
        /// </summary>
        private void HandleMonsterDefeated(MonsterDefeatResponse response)
        {
            if (response == null) return;

            // Xác định vị trí quái vừa chết
            Vector3 spawnPosition = Vector3.zero;
            bool foundPos = false;

            if (response.MonsterId > 0 && _recentDeathPositions.TryGetValue(response.MonsterId, out var savedPos))
            {
                spawnPosition = savedPos;
                _recentDeathPositions.Remove(response.MonsterId);
                foundPos = true;
            }
            else if (_fallbackPositions.Count > 0)
            {
                spawnPosition = _fallbackPositions.Dequeue();
                foundPos = true;
            }
            else
            {
                // Fallback: Tìm nhân vật người chơi gần nhất
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    spawnPosition = player.transform.position + Vector3.up * 1.5f;
                    foundPos = true;
                }
            }

            if (!foundPos)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    spawnPosition = player.transform.position + (Vector3)Random.insideUnitCircle * 0.5f;
                    foundPos = true;
                }
            }

            if (!foundPos) return;

            // Chạy Coroutine hiển thị lần lượt các hiệu ứng rớt đồ
            StartCoroutine(SpawnLootSequence(spawnPosition, response));
        }

        private IEnumerator SpawnLootSequence(Vector3 basePosition, MonsterDefeatResponse response)
        {
            int dropIndex = 0;

            // 1. Phôi vật thể Vàng (Gold) rơi ra map - sử dụng Gold-Icon.png từ Resources/Item
            if (response.GoldEarned > 0)
            {
                Sprite goldSprite = Resources.Load<Sprite>("Item/Gold-Icon") ?? Resources.Load<Sprite>("Item/Gold");
                Vector3 landPos = basePosition + (Vector3)(Random.insideUnitCircle * 0.8f);
                SpawnPhysicalDrop(basePosition, landPos, DropPickupType.Gold, "Gold", (float)response.GoldEarned, goldSprite, goldColor);
                dropIndex++;
                yield return new WaitForSeconds(0.08f);
            }

            // 2. Phôi vật thể Kinh nghiệm (EXP) rơi ra map - sử dụng Exp-icon.png từ Resources/Item
            if (response.ExperienceEarned > 0)
            {
                Sprite expSprite = Resources.Load<Sprite>("Item/Exp-icon") ?? Resources.Load<Sprite>("Item/EXP");
                Vector3 landPos = basePosition + (Vector3)(Random.insideUnitCircle * 0.8f);
                SpawnPhysicalDrop(basePosition, landPos, DropPickupType.Exp, "EXP", (float)response.ExperienceEarned, expSprite, expColor);
                dropIndex++;
                yield return new WaitForSeconds(0.08f);
            }

            // 3. Phôi vật thể Vật phẩm & Đá nâng cấp (Skill Upgrade Stone) rơi ra map
            if (response.DroppedItems != null && response.DroppedItems.Length > 0)
            {
                foreach (var item in response.DroppedItems)
                {
                    if (item == null) continue;

                    string itemName = item.ItemName ?? "";
                    DropPickupType type = DropPickupType.Item;
                    Color itemColorToUse = itemColor;

                    if (itemName.Contains("Skill Upgrade Stone") || itemName.Contains("Upgrade Stone"))
                    {
                        type = DropPickupType.SkillUpgradeStone;
                        itemColorToUse = gemColor;
                    }

                    Sprite customSprite = ResolveItemSprite(itemName);

                    Vector3 landPos = basePosition + (Vector3)(Random.insideUnitCircle * 1.0f);
                    SpawnPhysicalDrop(basePosition, landPos, type, itemName, item.Quantity, customSprite, itemColorToUse);
                    dropIndex++;
                    yield return new WaitForSeconds(0.08f);
                }
            }
        }

        private Sprite ResolveItemSprite(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            // 1. Try loading directly from Resources/Item/{itemName}
            Sprite spr = Resources.Load<Sprite>($"Item/{itemName}");
            if (spr != null) return spr;

            // 2. Try loading clean name
            string cleanName = itemName.Trim();
            spr = Resources.Load<Sprite>($"Item/{cleanName}");
            if (spr != null) return spr;

            // 3. Fallback for Skill Upgrade Stone
            if (itemName.Contains("Skill Upgrade Stone") || itemName.Contains("Upgrade Stone"))
            {
                return Resources.Load<Sprite>("Item/Skill Upgrade Stone");
            }

            return null;
        }

        private void SpawnPhysicalDrop(Vector3 spawnPos, Vector3 landPos, DropPickupType type, string name, float qty, Sprite sprite, Color color)
        {
            GameObject dropGO = new GameObject($"[DropPickup]_{name}");
            dropGO.transform.position = spawnPos;

            var pickup = dropGO.AddComponent<WorldDropPickup>();
            pickup.Setup(type, name, qty, sprite, color, landPos);
        }

        public void SpawnFloatingTextDirect(Vector3 position, string text, Color color)
        {
            SpawnFloatingText(position, text, color);
        }

        private void SpawnFloatingText(Vector3 position, string text, Color color)
        {
            GameObject dropGO = null;

            if (dropTextPrefab != null)
            {
                dropGO = Instantiate(dropTextPrefab, position, Quaternion.identity);
            }
            else
            {
                // Tạo Runtime World TextMeshPro nếu chưa gán Prefab trong Inspector
                dropGO = new GameObject($"[DropText]_{text}");
                dropGO.transform.position = position;

                var tmp = dropGO.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 4.5f;
                tmp.sortingOrder = 50;
            }

            var floating = dropGO.GetComponent<FloatingDropText>();
            if (floating == null)
            {
                floating = dropGO.AddComponent<FloatingDropText>();
            }

            floating.Setup(text, color);
        }
    }
}
