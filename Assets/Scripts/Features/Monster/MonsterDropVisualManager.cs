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

            if (!foundPos) return;

            // Chạy Coroutine hiển thị lần lượt các hiệu ứng rớt đồ
            StartCoroutine(SpawnLootSequence(spawnPosition, response));
        }

        private IEnumerator SpawnLootSequence(Vector3 basePosition, MonsterDefeatResponse response)
        {
            float yOffset = 0.5f;

            // 1. Hiệu ứng EXP
            if (response.ExperienceEarned > 0)
            {
                SpawnFloatingText(basePosition + Vector3.up * yOffset, $"+{response.ExperienceEarned} EXP", expColor);
                yOffset += 0.4f;
                yield return new WaitForSeconds(0.12f);
            }

            // 2. Hiệu ứng Coins (Vàng)
            if (response.GoldEarned > 0)
            {
                string goldStr = response.GoldEarned % 1 == 0 
                    ? $"{response.GoldEarned:N0}" 
                    : $"{response.GoldEarned:F1}";
                string coinLabel = response.GoldEarned == 1 ? "Coin" : "Coins";
                SpawnFloatingText(basePosition + Vector3.up * yOffset, $"+{goldStr} {coinLabel}", goldColor);
                yOffset += 0.4f;
                yield return new WaitForSeconds(0.12f);
            }

            // 3. Hiệu ứng Vật phẩm (Items / Gems / Coins)
            if (response.DroppedItems != null && response.DroppedItems.Length > 0)
            {
                foreach (var item in response.DroppedItems)
                {
                    if (item == null) continue;
                    
                    string itemName = item.ItemName ?? "";
                    if (itemName.Equals("Vàng", System.StringComparison.OrdinalIgnoreCase))
                    {
                        itemName = item.Quantity == 1 ? "Coin" : "Coins";
                    }
                    else if (itemName.Equals("Kim Cương", System.StringComparison.OrdinalIgnoreCase) || 
                             itemName.Equals("Đá Quý", System.StringComparison.OrdinalIgnoreCase) || 
                             itemName.Equals("Gem", System.StringComparison.OrdinalIgnoreCase))
                    {
                        itemName = item.Quantity == 1 ? "Gem" : "Gems";
                    }

                    string itemText = item.Quantity > 1 
                        ? $"+{item.Quantity}x {itemName}" 
                        : $"+{itemName}";

                    Color colorToUse = (itemName.IndexOf("Gem", System.StringComparison.OrdinalIgnoreCase) >= 0) 
                        ? gemColor 
                        : itemColor;

                    SpawnFloatingText(basePosition + Vector3.up * yOffset, itemText, colorToUse);
                    yOffset += 0.4f;
                    yield return new WaitForSeconds(0.12f);
                }
            }
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
