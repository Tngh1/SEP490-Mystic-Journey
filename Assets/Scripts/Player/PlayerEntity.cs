using UnityEngine;
using System;

/// <summary>
/// Player health adapter. Bridges backend stats and the network-authoritative HP
/// stored on <see cref="NetworkPlayer"/> to the legacy single-player UI / event
/// consumers (<c>PlayerHUDController</c>, <c>DamagePopupManager</c>, etc.).
///
/// Why adapter (not NetworkBehaviour):
///   - Per the Phase 0 decision, the multiplayer path keeps the legacy
///     PlayerEntity singleton API so existing UI does not need to be rewritten.
///   - HP authoritative state lives on <see cref="NetworkPlayer.CurrentHp"/> /
///     <c>MaxHp</c> / <c>IsAlive</c> ([Networked]).
///   - PlayerEntity polls NetworkPlayer.CurrentHp and forwards changes via the
///     legacy OnHealthChanged static event.
///
/// Single-player fallback:
///   - When no NetworkPlayer exists (Photon not started), PlayerEntity behaves
///     exactly as before — local HP field, TakeDamage, Die event.
///
/// Lifetime: MonoBehaviour, attached to PlayerNetwork prefab root. Legacy
/// callers read <see cref="Instance"/> for the local player's HP.
/// </summary>
public class PlayerEntity : MonoBehaviour
{
    [SerializeField] private int maxHealth = 500;
    private int currentHealth;

    public static PlayerEntity Instance { get; internal set; }

    public event EventHandler OnTakeHit;
    public event EventHandler OnDeath;

    public float MoveSpeed { get; private set; } = 100f;
    public float AttackSpeed { get; private set; } = 100f;

    public static event Action OnStatsLoaded;
    public static event Action<int, int> OnHealthChanged;

    // Cached reference to NetworkPlayer (may be null in single-player fallback).
    private NetworkPlayer _networkPlayer;
    private int _lastBroadcastHp = -1;
    private int _lastBroadcastMaxHp = -1;
    private bool _lastBroadcastAlive = true;

    // List of all active players for environment scripts (e.g. TilemapAutoFader)
    public static System.Collections.Generic.List<PlayerEntity> AllPlayers = new System.Collections.Generic.List<PlayerEntity>();

    // For dynamic sorting
    private struct SpriteOrder {
        public SpriteRenderer renderer;
        public int initialOrder;
    }
    private SpriteOrder[] _spriteOrders;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        currentHealth = maxHealth;
        _networkPlayer = GetComponent<NetworkPlayer>();
        
        if (GetComponent<BuffManager>() == null)
        {
            gameObject.AddComponent<BuffManager>();
        }

        // Cache initial sorting orders
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _spriteOrders = new SpriteOrder[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            _spriteOrders[i] = new SpriteOrder { renderer = renderers[i], initialOrder = renderers[i].sortingOrder };
        }
    }

    private int _wallOverlapCount = 0;

    public void AddWallOverlap()
    {
        _wallOverlapCount++;
        UpdateSortingOrder();
    }

    public void RemoveWallOverlap()
    {
        _wallOverlapCount = Mathf.Max(0, _wallOverlapCount - 1);
        UpdateSortingOrder();
    }

    private void UpdateSortingOrder()
    {
        if (_spriteOrders == null) return;
        
        bool isBehindWall = _wallOverlapCount > 0;
        int offset = isBehindWall ? -14 : 0; // If initially 15, goes down to 1 (behind wall which is 2)
        
        for (int i = 0; i < _spriteOrders.Length; i++)
        {
            if (_spriteOrders[i].renderer != null)
            {
                _spriteOrders[i].renderer.sortingOrder = _spriteOrders[i].initialOrder + offset;
            }
        }
    }

    private void OnEnable()
    {
        if (!AllPlayers.Contains(this)) AllPlayers.Add(this);
    }

    private void OnDisable()
    {
        AllPlayers.Remove(this);
        // Xoá khỏi collider cache của TilemapAutoFader khi player leave
        TilemapAutoFader.InvalidatePlayerCache(this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Fire initial event so UI can render a full-HP fallback before stats arrive.
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (MysticJourney.API.Core.ApiClient.Instance.HasToken())
        {
            MysticJourney.API.Endpoints.CharacterApi.Instance.GetMyStats(
                response =>
                {
                    if (response != null)
                    {
                        MoveSpeed = response.MoveSpeed;
                        AttackSpeed = response.AttackSpeed;
                        ApplyHealth(response.CurrentHp, response.MaxHp);
                        OnStatsLoaded?.Invoke();
                    }
                },
                error => Debug.LogWarning($"[PlayerEntity] GetMyStats failed: {error.Message}")
            );
        }
    }

    private void Update()
    {
        // Poll the networked HP every frame and broadcast to legacy listeners
        // when it changes. Cheap: only fires the event on actual change.
        if (_networkPlayer == null || _networkPlayer.Object == null) return;
        
        // ONLY broadcast the health of the local player to the UI
        if (!_networkPlayer.HasInputAuthority) return;

        int netHp = _networkPlayer.CurrentHp;
        int netMaxHp = _networkPlayer.MaxHp;
        bool netAlive = _networkPlayer.IsAlive;

        if (netHp != _lastBroadcastHp || netMaxHp != _lastBroadcastMaxHp)
        {
            bool hpChanged = netHp != _lastBroadcastHp;
            
            _lastBroadcastHp = netHp;
            _lastBroadcastMaxHp = netMaxHp;
            currentHealth = netHp;
            maxHealth = netMaxHp;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            // If HP changed (e.g. took damage or healed), sync to DB using the rate-limited coroutine
            if (hpChanged)
            {
                if (syncHpCoroutine != null) StopCoroutine(syncHpCoroutine);
                syncHpCoroutine = StartCoroutine(SyncHpRoutine());
            }
        }

        if (netAlive != _lastBroadcastAlive)
        {
            _lastBroadcastAlive = netAlive;
            if (!netAlive) Die();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;

    public void ApplyHealth(int currentHp, int maxHp)
    {
        maxHealth = Mathf.Max(0, maxHp);
        currentHealth = maxHealth > 0 ? Mathf.Clamp(currentHp, 0, maxHealth) : Mathf.Max(0, currentHp);
        _lastBroadcastHp = currentHealth;
        _lastBroadcastMaxHp = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // If the avatar is already spawned and we have authority, push to network
        if (_networkPlayer != null && _networkPlayer.Object != null && _networkPlayer.HasStateAuthority)
        {
            _networkPlayer.MaxHp = maxHealth;
            _networkPlayer.CurrentHp = currentHealth;
            
            // Sync alive state if HP was loaded as 0 (e.g. logging back in after death)
            if (currentHealth <= 0 && _networkPlayer.IsAlive)
            {
                _networkPlayer.Die();
            }
            else if (currentHealth > 0 && !_networkPlayer.IsAlive)
            {
                _networkPlayer.IsAlive = true;
            }
        }
        else
        {
            // SINGLE-PLAYER FALLBACK / NETWORK NOT READY
            if (currentHealth <= 0)
            {
                Die();
            }
        }
    }

    /// <summary>
    /// Apply damage to the player. In multiplayer this delegates to
    /// <see cref="NetworkPlayer.ApplyDamage"/>, which is server-authoritative.
    /// In single-player fallback it directly mutates local HP.
    ///
    /// attackerCrit/attackerCritMultiplier cho phép NGUỒN tấn công tự quyết định cú đánh
    /// có crit hay không (quái đọc CritRate/CritDamage từ Monster table). Để null nghĩa là
    /// "không có dữ liệu crit của attacker" → giữ nguyên hành vi cũ: tự roll 10%.
    /// KHÔNG được roll thêm một lần nữa khi attackerCrit != null, nếu không một cú đánh sẽ
    /// bị tính crit hai lần (quái crit rồi player lại roll crit đè lên).
    /// </summary>
    public void TakeDamage(int damage, bool? attackerCrit = null, float attackerCritMultiplier = 1.5f)
    {
        if (_networkPlayer != null)
        {
            // Networked: nhân crit TRƯỚC khi gửi đi, vì NetworkPlayer.ApplyDamage là
            // server-authoritative và không biết gì về CritRate của quái.
            int networkedDamage = attackerCrit == true
                ? Mathf.RoundToInt(damage * Mathf.Max(1f, attackerCritMultiplier))
                : damage;
            _networkPlayer.ApplyDamage(networkedDamage);
            return;
        }

        if (currentHealth <= 0) return; // Prevent multiple death triggers in single-player

        bool isCrit = attackerCrit ?? (UnityEngine.Random.Range(0f, 100f) <= 10f);
        float critMultiplier = attackerCrit.HasValue ? Mathf.Max(1f, attackerCritMultiplier) : 1.5f;
        int initialDamage = isCrit ? Mathf.RoundToInt(damage * critMultiplier) : damage;

        // Giảm trừ sát thương bằng Def
        float currentDef = 0f;
        var combat = GetComponent<PlayerCombat>();
        if (combat != null) currentDef = combat.TotalDef;

        // Ví dụ công thức: Giảm (Def / 5) điểm sát thương, tối đa giảm 50% sát thương nhận vào
        int reducedDamage = Mathf.RoundToInt(currentDef / 5f);
        int finalDamage = Mathf.Max(Mathf.RoundToInt(initialDamage * 0.5f), initialDamage - reducedDamage);

        currentHealth -= finalDamage;
        if (currentHealth < 0) currentHealth = 0;

        if (DamagePopupManager.Instance != null)
        {
            DamagePopupManager.Instance.Create(transform.position, finalDamage, isCrit, true);
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnTakeHit?.Invoke(this, EventArgs.Empty);

        if (currentHealth <= 0)
        {
            Die();
        }

        if (syncHpCoroutine != null) StopCoroutine(syncHpCoroutine);
        syncHpCoroutine = StartCoroutine(SyncHpRoutine());
    }

    /// <summary>
    /// Apply healing to the player. In multiplayer delegates to
    /// <see cref="NetworkPlayer.RequestHeal"/>, which routes to state authority.
    /// </summary>
    public void Heal(int amount)
    {
        if (amount <= 0) return;

        if (_networkPlayer != null)
        {
            _networkPlayer.RequestHeal(amount);
            return;
        }

        // Offline / Local healing
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        if (DamagePopupManager.Instance != null)
        {
            // Spawn a green popup for healing
            DamagePopupManager.Instance.Create(transform.position, amount, false, false, true); 
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (syncHpCoroutine != null) StopCoroutine(syncHpCoroutine);
        syncHpCoroutine = StartCoroutine(SyncHpRoutine());
    }

    public void Die()
    {
        Debug.Log("[PlayerEntity] Player died.");
        OnDeath?.Invoke(this, EventArgs.Empty);

        if (PlayerHUDController.Instance != null)
        {
            PlayerHUDController.Instance.ShowDeathPopup();
        }
    }

    public void WorldRespawn(Vector3 pos)
    {
        currentHealth = Mathf.Max(1, maxHealth / 10);
        transform.position = pos;
        Debug.Log($"[PlayerEntity] Player respawned in world at {pos} with 10% HP.");
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (syncHpCoroutine != null) StopCoroutine(syncHpCoroutine);
        syncHpCoroutine = StartCoroutine(SyncHpRoutine());
    }

    public void DungeonRespawn(Vector3 pos)
    {
        currentHealth = maxHealth;
        transform.position = pos;
        Debug.Log($"[PlayerEntity] Player respawned in dungeon at {pos} with FULL HP.");
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (syncHpCoroutine != null) StopCoroutine(syncHpCoroutine);
        syncHpCoroutine = StartCoroutine(SyncHpRoutine());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Backend HP sync (single-player fallback only)
    // ─────────────────────────────────────────────────────────────────────────

    private Coroutine syncHpCoroutine;

    private System.Collections.IEnumerator SyncHpRoutine()
    {
        yield return new WaitForSeconds(1f);
        if (MysticJourney.API.Core.ApiClient.Instance.HasToken())
        {
            MysticJourney.API.Endpoints.CharacterApi.Instance.UpdateHp(
                currentHealth,
                response => { /* Sync OK */ },
                error => Debug.LogWarning($"[PlayerEntity] Sync HP failed: {error.Message}")
            );
        }
    }
}