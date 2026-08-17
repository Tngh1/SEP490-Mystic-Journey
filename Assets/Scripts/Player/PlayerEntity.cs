using UnityEngine;
using System;

// Executes mono behaviour operation.
public class PlayerEntity : MonoBehaviour
{
    [SerializeField] private int maxHealth = 500;
    private int currentHealth;

    // Executes instance operation.
    public static PlayerEntity Instance { get; internal set; }

    public event EventHandler OnTakeHit;
    public event EventHandler OnDeath;

    // Executes move speed operation.
    public float MoveSpeed { get; private set; } = 100f;
    // Executes attack speed operation.
    public float AttackSpeed { get; private set; } = 100f;

    public static event Action OnStatsLoaded;
    public static event Action<int, int> OnHealthChanged;

    private NetworkPlayer _networkPlayer;
    private int _lastBroadcastHp = -1;
    private int _lastBroadcastMaxHp = -1;
    private bool _lastBroadcastAlive = true;

    public static System.Collections.Generic.List<PlayerEntity> AllPlayers = new System.Collections.Generic.List<PlayerEntity>();

    // Executes sprite order operation.
    private struct SpriteOrder {
        public SpriteRenderer renderer;
        public int initialOrder;
    }
    private SpriteOrder[] _spriteOrders;


    // Initializes internal component caches and dependencies for PlayerEntity upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (Instance == null) Instance = this; // Cache local singleton instance

        currentHealth = maxHealth; // Initialize HP to max
        _networkPlayer = GetComponent<NetworkPlayer>(); // Cache network player peer component

        if (GetComponent<BuffManager>() == null)
        {
            gameObject.AddComponent<BuffManager>(); // Auto-attach BuffManager if absent
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _spriteOrders = new SpriteOrder[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            _spriteOrders[i] = new SpriteOrder { renderer = renderers[i], initialOrder = renderers[i].sortingOrder }; // Record initial sprite sorting layer orders
        }
    }

    private int _wallOverlapCount = 0;

    // Create wall overlap; it updates sorting order.
    public void AddWallOverlap()
    {
        _wallOverlapCount++;
        UpdateSortingOrder();
    }

    // Executes remove wall overlap operation.
    public void RemoveWallOverlap()
    {
        _wallOverlapCount = Mathf.Max(0, _wallOverlapCount - 1);
        UpdateSortingOrder();
    }

    // Executes update sorting order operation.
    private void UpdateSortingOrder()
    {
        if (_spriteOrders == null) return;

        int offset = 0;

        for (int i = 0; i < _spriteOrders.Length; i++)
        {
            if (_spriteOrders[i].renderer != null)
            {
                _spriteOrders[i].renderer.sortingOrder = _spriteOrders[i].initialOrder + offset;
            }
        }
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        if (!AllPlayers.Contains(this)) AllPlayers.Add(this);
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        AllPlayers.Remove(this);
        TilemapAutoFader.InvalidatePlayerCache(this);
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Performs startup initialization for PlayerEntity on the first active frame.
    private void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth); // Broadcast initial HP state to HUD

        if (MysticJourney.API.Core.ApiClient.Instance.HasToken())
        {
            MysticJourney.API.Endpoints.CharacterApi.Instance.GetMyStats(
                response =>
                {
                    if (response != null)
                    {
                        MoveSpeed = response.MoveSpeed; // Apply server-calculated movement speed
                        AttackSpeed = response.AttackSpeed; // Apply server-calculated attack animation rate
                        ApplyHealth(response.CurrentHp, response.MaxHp); // Sync server health values
                        OnStatsLoaded?.Invoke(); // Trigger UI stats refresh
                    }
                },
                error => Debug.LogWarning($"[PlayerEntity] GetMyStats failed: {error.Message}")
            );
        }
    }

    // Monitors networked health and alive state changes every frame and synchronizes with server.
    private void Update()
    {
        if (_networkPlayer == null || _networkPlayer.Object == null) return;

        if (!_networkPlayer.HasInputAuthority) return; // Only execute local client sync logic for input authority

        int netHp = _networkPlayer.CurrentHp;
        int netMaxHp = _networkPlayer.MaxHp;
        bool netAlive = _networkPlayer.IsAlive;

        if (netHp != _lastBroadcastHp || netMaxHp != _lastBroadcastMaxHp)
        {
            bool hpChanged = netHp != _lastBroadcastHp;

            _lastBroadcastHp = netHp;
            _lastBroadcastMaxHp = netMaxHp;
            currentHealth = netHp; // Update local health state
            maxHealth = netMaxHp;
            OnHealthChanged?.Invoke(currentHealth, maxHealth); // Update HUD health bar

            if (hpChanged)
            {
                if (syncHpCoroutine != null) StopCoroutine(syncHpCoroutine);
                syncHpCoroutine = StartCoroutine(SyncHpRoutine()); // Debounce sync HTTP call to backend
            }
        }

        if (netAlive != _lastBroadcastAlive)
        {
            _lastBroadcastAlive = netAlive;
            if (!netAlive) Die(); // Trigger local death sequence and popup
        }
    }


    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;

    // Sets new current and max health values, syncing to NetworkPlayer state authority if active.
    public void ApplyHealth(int currentHp, int maxHp)
    {
        maxHealth = Mathf.Max(0, maxHp);
        currentHealth = maxHealth > 0 ? Mathf.Clamp(currentHp, 0, maxHealth) : Mathf.Max(0, currentHp); // Clamp health between 0 and max
        _lastBroadcastHp = currentHealth;
        _lastBroadcastMaxHp = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth); // Broadcast HP change to UI

        if (_networkPlayer != null && _networkPlayer.Object != null && _networkPlayer.HasStateAuthority)
        {
            _networkPlayer.MaxHp = maxHealth; // Sync networked state variables
            _networkPlayer.CurrentHp = currentHealth;

            if (currentHealth <= 0 && _networkPlayer.IsAlive)
            {
                _networkPlayer.Die(); // Execute networked death state mutation
            }
            else if (currentHealth > 0 && !_networkPlayer.IsAlive)
            {
                _networkPlayer.IsAlive = true; // Revive if HP restored above zero
            }
        }
        else
        {
            if (currentHealth <= 0)
            {
                Die(); // Local death fallback
            }
        }
    }

    // Routes incoming damage calculation, applying critical multipliers and forwarding to network RPC.
    public void TakeDamage(int damage, bool? attackerCrit = null, float attackerCritMultiplier = 1.5f)
    {
        if (_networkPlayer != null)
        {
            int networkedDamage = attackerCrit == true
                ? Mathf.RoundToInt(damage * Mathf.Max(1f, attackerCritMultiplier)) // Scale critical hit damage
                : damage;
            _networkPlayer.RequestDamage(networkedDamage, attackerCrit == true); // Send damage RPC request to host
            return;
        }

        if (currentHealth <= 0) return;

        // Randomize the eligible candidates before selecting this gameplay result.
        bool isCrit = attackerCrit ?? (UnityEngine.Random.Range(0f, 100f) <= 10f);
        float critMultiplier = attackerCrit.HasValue ? Mathf.Max(1f, attackerCritMultiplier) : 1.5f;
        int initialDamage = isCrit ? Mathf.RoundToInt(damage * critMultiplier) : damage;

        float currentDef = 0f;
        var combat = GetComponent<PlayerCombat>();
        if (combat != null) currentDef = combat.TotalDef;

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
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        syncHpCoroutine = StartCoroutine(SyncHpRoutine());
    }

    // Executes heal operation.
    public void Heal(int amount)
    {
        if (amount <= 0) return;

        if (_networkPlayer != null)
        {
            _networkPlayer.RequestHeal(amount);
            return;
        }

        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        if (DamagePopupManager.Instance != null)
        {
            DamagePopupManager.Instance.Create(transform.position, amount, false, false, true);
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (syncHpCoroutine != null) StopCoroutine(syncHpCoroutine);
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        syncHpCoroutine = StartCoroutine(SyncHpRoutine());
    }

    // Marks player as dead (IsAlive = false), halts movement, and triggers death sequence.
    public void Die()
    {
        Debug.Log("[PlayerEntity] Player died.");
        OnDeath?.Invoke(this, EventArgs.Empty);

        if (PlayerHUDUIManager.Instance != null)
        {
            PlayerHUDUIManager.Instance.ShowDeathPopup();
        }
    }

    // Executes world respawn operation.
    public void WorldRespawn(Vector3 pos)
    {
        currentHealth = Mathf.Max(1, maxHealth / 10);
        transform.position = pos;
        Debug.Log($"[PlayerEntity] Player respawned in world at {pos} with 10% HP.");

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (syncHpCoroutine != null) StopCoroutine(syncHpCoroutine);
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        syncHpCoroutine = StartCoroutine(SyncHpRoutine());
    }

    // Executes dungeon respawn operation.
    public void DungeonRespawn(Vector3 pos)
    {
        currentHealth = maxHealth;
        transform.position = pos;
        Debug.Log($"[PlayerEntity] Player respawned in dungeon at {pos} with FULL HP.");

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (syncHpCoroutine != null) StopCoroutine(syncHpCoroutine);
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        syncHpCoroutine = StartCoroutine(SyncHpRoutine());
    }


    private Coroutine syncHpCoroutine;

    // Executes sync hp routine operation.
    private System.Collections.IEnumerator SyncHpRoutine()
    {
        yield return new WaitForSeconds(1f);
        if (MysticJourney.API.Core.ApiClient.Instance.HasToken())
        {
            MysticJourney.API.Endpoints.CharacterApi.Instance.UpdateHp(
                currentHealth,
                response =>
                {
                },
                error => Debug.LogWarning($"[PlayerEntity] Sync HP failed: {error.Message}")
            );
        }
    }
}
