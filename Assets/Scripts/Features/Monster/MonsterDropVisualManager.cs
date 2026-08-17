using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;

namespace MysticJourney.Features.Monster
{
    // Executes core business logic for mono behaviour.
    public class MonsterDropVisualManager : MonoBehaviour
    {
        private static MonsterDropVisualManager _instance;
        // Executes core business logic for instance.
        public static MonsterDropVisualManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<MonsterDropVisualManager>();
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
        [SerializeField] private TMP_FontAsset dropFont;
        [SerializeField] private Color expColor = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private Color goldColor = new Color(1f, 0.85f, 0.15f);
        [SerializeField] private Color gemColor = new Color(0.85f, 0.35f, 1f);
        [SerializeField] private Color itemColor = new Color(0.4f, 0.95f, 0.45f);

        private readonly Dictionary<int, Vector3> _recentDeathPositions = new Dictionary<int, Vector3>();
        private readonly Queue<Vector3> _fallbackPositions = new Queue<Vector3>();

        // Initializes internal component caches and dependencies for MonsterDropVisualManager upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
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

        // Refresh visible state and subscribe the event handlers required while this component is active.
        private void OnEnable()
        {
            if (MonsterManager.Instance != null)
            {
                MonsterManager.Instance.OnMonsterDefeated += HandleMonsterDefeated;
            }
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDisable()
        {
            if (MonsterManager.Instance != null)
            {
                MonsterManager.Instance.OnMonsterDefeated -= HandleMonsterDefeated;
            }
        }

        // Performs startup initialization for MonsterDropVisualManager on the first active frame.
        // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
        private void Start()
        {
            if (MonsterManager.Instance != null)
            {
                MonsterManager.Instance.OnMonsterDefeated -= HandleMonsterDefeated;
                MonsterManager.Instance.OnMonsterDefeated += HandleMonsterDefeated;
            }
        }

        // Executes core business logic for register monster death position.
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

        // Executes core business logic for handle monster defeated.
        private void HandleMonsterDefeated(MonsterDefeatResponse response)
        {
            if (response == null) return;

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

            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(SpawnLootSequence(spawnPosition, response));
        }

        // Executes core business logic for spawn loot sequence.
        private IEnumerator SpawnLootSequence(Vector3 basePosition, MonsterDefeatResponse response)
        {
            int dropIndex = 0;

            if (response.GoldEarned > 0)
            {
                Sprite goldSprite = Resources.Load<Sprite>("Item/Gold-Icon") ?? Resources.Load<Sprite>("Item/Gold");
                Vector3 landPos = basePosition + (Vector3)(Random.insideUnitCircle * 0.8f);
                SpawnPhysicalDrop(basePosition, landPos, DropPickupType.Gold, "Gold", (float)response.GoldEarned, goldSprite, goldColor);
                dropIndex++;
                yield return new WaitForSeconds(0.08f);
            }

            if (response.ExperienceEarned > 0)
            {
                Sprite expSprite = Resources.Load<Sprite>("Item/Exp-icon") ?? Resources.Load<Sprite>("Item/EXP");
                Vector3 landPos = basePosition + (Vector3)(Random.insideUnitCircle * 0.8f);
                SpawnPhysicalDrop(basePosition, landPos, DropPickupType.Exp, "EXP", (float)response.ExperienceEarned, expSprite, expColor);
                dropIndex++;
                yield return new WaitForSeconds(0.08f);
            }

            if (response.DroppedItems != null && response.DroppedItems.Length > 0)
            {
                foreach (var item in response.DroppedItems)
                {
                    if (item == null) continue;

                    string itemName = item.ItemName ?? "";
                    // Supported world-drop types: Gold, Exp, SkillUpgradeStone, or Item; the type selects pickup visuals and collection behavior.
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

        // Executes core business logic for resolve item sprite.
        // Logic details: validates required non-empty string arguments.
        private Sprite ResolveItemSprite(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            Sprite spr = Resources.Load<Sprite>($"Item/{itemName}");
            if (spr != null) return spr;

            string cleanName = itemName.Trim();
            spr = Resources.Load<Sprite>($"Item/{cleanName}");
            if (spr != null) return spr;

            if (itemName.Contains("Skill Upgrade Stone") || itemName.Contains("Upgrade Stone"))
            {
                return Resources.Load<Sprite>("Item/Skill Upgrade Stone");
            }

            return null;
        }

        // Executes core business logic for spawn physical drop.
        private void SpawnPhysicalDrop(Vector3 spawnPos, Vector3 landPos, DropPickupType type, string name, float qty, Sprite sprite, Color color)
        {
            GameObject dropGO = new GameObject($"[DropPickup]_{name}");
            dropGO.transform.position = spawnPos;

            var pickup = dropGO.AddComponent<WorldDropPickup>();
            pickup.Setup(type, name, qty, sprite, color, landPos);
        }

        private Coroutine _rewardRefreshRoutine;
        private bool _pendingInventoryRefresh;

        // Executes core business logic for request reward refresh.
        public void RequestRewardRefresh(bool inventoryAndSkill)
        {
            _pendingInventoryRefresh |= inventoryAndSkill;

            if (_rewardRefreshRoutine != null)
                StopCoroutine(_rewardRefreshRoutine);
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            _rewardRefreshRoutine = StartCoroutine(FlushRewardRefresh());
        }

        // Executes core business logic for flush reward refresh.
        private IEnumerator FlushRewardRefresh()
        {
            yield return new WaitForSeconds(0.25f);

            if (PlayerHUDUIManager.Instance != null)
                PlayerHUDUIManager.Instance.RefreshHUD();

            if (_pendingInventoryRefresh)
            {
                InventoryUIManager.RefreshAny(refreshStats: true);

                var skillPanel = FindFirstObjectByType<SkillUIManager>(FindObjectsInactive.Include);
                if (skillPanel != null)
                    skillPanel.RefreshStoneCount();

                var skillPopup = FindFirstObjectByType<SkillPopup>(FindObjectsInactive.Include);
                if (skillPopup != null && skillPopup.gameObject.activeInHierarchy)
                    skillPopup.AutoBindComponents();
            }

            _pendingInventoryRefresh = false;
            _rewardRefreshRoutine = null;
        }

        // Executes core business logic for spawn floating text direct.
        public void SpawnFloatingTextDirect(Vector3 position, string text, Color color)
        {
            SpawnFloatingText(position, text, color);
        }

        // Executes core business logic for spawn floating text.
        private void SpawnFloatingText(Vector3 position, string text, Color color)
        {
            GameObject dropGO = null;

            if (dropTextPrefab != null)
            {
                dropGO = Instantiate(dropTextPrefab, position, Quaternion.identity);
            }
            else
            {
                dropGO = new GameObject($"[DropText]_{text}");
                dropGO.transform.position = position;

                var tmp = dropGO.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 4.5f;
                tmp.sortingOrder = 50;

                if (dropFont != null)
                {
                    tmp.font = dropFont;
                }
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
