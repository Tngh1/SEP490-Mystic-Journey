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
        }
    }

    /// <summary>
    /// Apply damage to the player. In multiplayer this delegates to
    /// <see cref="NetworkPlayer.ApplyDamage"/>, which is server-authoritative.
    /// In single-player fallback it directly mutates local HP.
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (_networkPlayer != null)
        {
            _networkPlayer.ApplyDamage(damage);
            return;
        }

        if (currentHealth <= 0) return; // Prevent multiple death triggers in single-player

        bool isCrit = UnityEngine.Random.Range(0f, 100f) <= 10f;
        int initialDamage = isCrit ? Mathf.RoundToInt(damage * 1.5f) : damage;

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

        if (_networkPlayer == null && PlayerHUDController.Instance != null)
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