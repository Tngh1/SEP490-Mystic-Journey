using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace MysticJourney.Features.Monster
{
    // Executes drop pickup type operation.
    public enum DropPickupType
    {
        Gold,
        Exp,
        SkillUpgradeStone,
        Item
    }

    // Executes mono behaviour operation.
    public class WorldDropPickup : MonoBehaviour
    {
        [Header("Drop Info")]
        public DropPickupType dropType;
        public string itemName;
        public float amount;
        public Color glowColor = Color.yellow;

        [Header("Settings")]
        [SerializeField] private float collectDistance = 0.5f;
        [SerializeField] private float magnetSpeed = 12.0f;

        private SpriteRenderer _spriteRenderer;
        private Vector3 _landPosition;
        private bool _isSpawning = true;
        private bool _isBeingMagnetized = false;
        private float magnetDelayTimer = 0.5f;
        private float _bobTimer = 0f;
        private Transform _playerTransform;

        // Executes setup operation.
        public void Setup(DropPickupType type, string name, float qty, Sprite customSprite, Color color, Vector3 targetLandPos)
        {
            dropType = type;
            itemName = name;
            amount = qty;
            glowColor = color;
            _landPosition = targetLandPos;
            _isBeingMagnetized = false;
            magnetDelayTimer = 0.5f;

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

                try
                {
                    _spriteRenderer.sortingLayerName = "Units";
                }
                catch
                {
                }
                _spriteRenderer.sortingOrder = 100;
            }

            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(PopAnimationSequence(targetLandPos));
        }

        // Executes ensure components operation.
        private void EnsureComponents()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

        }

        // Executes pop animation sequence operation.
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

        // Per-frame update loop for WorldDropPickup.
        // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
        private void Update()
        {
            if (_isSpawning || _isCollected) return;

            if (magnetDelayTimer > 0f)
            {
                magnetDelayTimer -= Time.deltaTime;
                _bobTimer += Time.deltaTime * 3f;
                float offset = Mathf.Sin(_bobTimer) * 0.05f;
                transform.position = new Vector3(_landPosition.x, _landPosition.y + offset, _landPosition.z);
                return;
            }

            FindPlayer();

            if (_playerTransform != null)
            {
                _isBeingMagnetized = true;

                Vector3 targetPos = _playerTransform.position + Vector3.up * 0.5f;
                transform.position = Vector3.MoveTowards(transform.position, targetPos, magnetSpeed * Time.deltaTime);

                if ((transform.position - targetPos).sqrMagnitude <= collectDistance * collectDistance)
                {
                    CollectItem();
                    return;
                }
            }

            if (!_isBeingMagnetized)
            {
                _bobTimer += Time.deltaTime * 3f;
                float offset = Mathf.Sin(_bobTimer) * 0.05f;
                transform.position = new Vector3(_landPosition.x, _landPosition.y + offset, _landPosition.z);
            }
        }

        private static Transform _sharedPlayer;
        private static int _nextPlayerSearchFrame;

        // Executes find player operation.
        private void FindPlayer()
        {
            if (_playerTransform != null && _playerTransform.gameObject.activeInHierarchy) return;

            if (_sharedPlayer != null && _sharedPlayer.gameObject.activeInHierarchy)
            {
                _playerTransform = _sharedPlayer;
                return;
            }

            if (NetworkPlayer.Local != null && NetworkPlayer.Local.gameObject.activeInHierarchy)
            {
                _sharedPlayer = NetworkPlayer.Local.transform;
                _playerTransform = _sharedPlayer;
                return;
            }

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

        // Executes collect item operation.
        private void CollectItem()
        {
            if (_isCollected) return;
            _isCollected = true;

            MysticJourney.Core.Services.AudioManager.Instance?.PlayPickup();

            if (_spriteRenderer != null) _spriteRenderer.enabled = false;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

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

            if (MonsterDropVisualManager.Instance != null)
            {
                MonsterDropVisualManager.Instance.SpawnFloatingTextDirect(transform.position, text, glowColor);
            }

            MonsterDropVisualManager.Instance.RequestRewardRefresh(
                inventoryAndSkill: dropType != DropPickupType.Gold && dropType != DropPickupType.Exp);

            Destroy(gameObject);
        }

        private static readonly Dictionary<DropPickupType, Sprite> _proceduralCache =
            new Dictionary<DropPickupType, Sprite>();

        // Executes get procedural sprite operation.
        private Sprite GetProceduralSprite(DropPickupType type)
        {
            if (_proceduralCache.TryGetValue(type, out var cached) && cached != null)
                return cached;

            var sprite = CreateProceduralSprite(type);
            _proceduralCache[type] = sprite;
            return sprite;
        }

        // Executes create procedural sprite operation.
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
                        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
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
