using System;
using MysticJourney.API.Core;
using UnityEngine;

public class EnemyEntity : MonoBehaviour
{
    private PolygonCollider2D polyColl;
    private CapsuleCollider2D capsuleColl;
    private BoxCollider2D boxColl;
    private EnemyBehaviour enemyBehaviour;

    private int currentHealth;
    [SerializeField] private int maxHealth;
    [SerializeField] private int monsterId;
    [SerializeField] private int monsterSpawnId;
    [SerializeField] private bool useApiStats = true;
    private bool isDead = false;

    public int MonsterId => monsterId;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    /// <summary>
    /// Called by DungeonSpawner immediately after Instantiate to inject the
    /// backend MonsterId and MonsterSpawnId into a dynamically spawned enemy.
    /// Prefab-placed enemies use the serialized Inspector values instead.
    /// </summary>
    public void SetSpawnData(int id, int spawnId)
    {
        monsterId = id;
        monsterSpawnId = spawnId;
    }


    public event EventHandler OnTakeHit;
    public event EventHandler OnDeath;
    public event Action<int, int> OnHealthChanged;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        polyColl = GetComponent<PolygonCollider2D>();
        capsuleColl = GetComponent<CapsuleCollider2D>();
        boxColl = GetComponent<BoxCollider2D>();
        enemyBehaviour = GetComponent<EnemyBehaviour>();

        Debug.Log($"[EnemyEntity] Start: {gameObject.name} | UseApi={useApiStats} | ID={monsterId} | ManagerNull?={MonsterManager.Instance == null}");

        // Fallback to inspector stats initially
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (useApiStats && monsterId > 0 && MonsterManager.Instance != null)
        {
            var cached = MonsterManager.Instance.GetCachedMonster(monsterId);
            Debug.Log($"[EnemyEntity] Cached for {monsterId} is null? {cached == null}");
            if (cached != null)
            {
                ApplyApiStats(cached.MaxHp, cached.Atk, cached.MoveSpeed);
            }
            else
            {
                Debug.Log($"[EnemyEntity] Calling LoadMonsterDetail for {monsterId}");
                MonsterManager.Instance.LoadMonsterDetail(monsterId, false, detail =>
                {
                    Debug.Log($"[EnemyEntity] LoadMonsterDetail callback for {monsterId}. detail is null? {detail == null}");
                    if (detail != null && !isDead)
                    {
                        ApplyApiStats(detail.MaxHp, detail.Atk, detail.MoveSpeed);
                    }
                });
            }
        }
    }

    private void ApplyApiStats(int apiMaxHp, int apiAtk, int apiMoveSpeed)
    {
        Debug.Log($"[EnemyEntity] {gameObject.name} ApplyApiStats: HP={apiMaxHp}, ATK={apiAtk}, SPD={apiMoveSpeed}");
        maxHealth = apiMaxHp;
        currentHealth = maxHealth;
        if (enemyBehaviour != null)
        {
            enemyBehaviour.UpdateStatsFromAPI(apiAtk, apiMoveSpeed);
        }
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Networking bridge
    //
    // When a Photon session is running, this enemy is spawned as a NetworkObject
    // and NetworkEnemy binds itself here. Damage is then applied authoritatively
    // on the state-authority client and the resulting HP / death replicates to
    // every other client. Offline, _network stays null and everything runs
    // locally exactly as before.
    // ─────────────────────────────────────────────────────────────────────────

    private NetworkEnemy _network;

    /// <summary>True once the enemy has died (drives the networked death mirror).</summary>
    public bool IsDead => isDead;

    /// <summary>Called by NetworkEnemy.Spawned to enable the networked damage route.</summary>
    public void BindNetwork(NetworkEnemy network) => _network = network;

    /// <summary>The bound NetworkEnemy when in a live session, else null. Lets callers
    /// broadcast networked effects (e.g. melee damage popups) to every client.</summary>
    public NetworkEnemy Network => (_network != null && _network.IsNetworkActive) ? _network : null;

    /// <summary>
    /// Public damage entry point. Projectiles / AoE / melee call this without
    /// knowing whether we are online. When networked, the request is routed to
    /// the enemy's state authority (applied once, replicated to all). Offline it
    /// applies immediately.
    /// </summary>
    /// <summary>
    /// Restores current HP by amount (up to maxHealth).
    /// </summary>
    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Public damage entry point. Projectiles / AoE / melee call this without
    /// knowing whether we are online. When networked, the request is routed to
    /// the enemy's state authority (applied once, replicated to all). Offline it
    /// applies immediately.
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        // Block hit if Resurrection Cocoon Shield is active
        var cocoonShield = GetComponent<ResurrectionCocoonShield>();
        if (cocoonShield != null && cocoonShield.TryBlockHit())
        {
            return;
        }

        if (_network != null && _network.IsNetworkActive)
        {
            _network.RequestDamage(damage);
            return;
        }

        ApplyDamageAuthoritative(damage);
    }

    /// <summary>
    /// The real HP maths + death detection. Runs on the state authority (online)
    /// or directly (offline). NEVER call this from a proxy — use TakeDamage.
    /// </summary>
    public void ApplyDamageAuthoritative(int damage)
    {
        if (isDead) return;

        var cocoonShield = GetComponent<ResurrectionCocoonShield>();
        if (cocoonShield != null && cocoonShield.TryBlockHit())
        {
            return;
        }

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnTakeHit?.Invoke(this, EventArgs.Empty);
        DetectDeath();
    }

    /// <summary>
    /// Proxy-side mirror: push the authority's replicated HP into this local copy
    /// so the health bar and hit flash match. Does NOT report to the backend
    /// (only the authority does that in DetectDeath).
    /// </summary>
    public void SyncNetworkedHealth(int networkedCurrent, int networkedMax)
    {
        if (networkedMax > 0) maxHealth = networkedMax;

        bool tookHit = networkedCurrent < currentHealth;
        currentHealth = Mathf.Clamp(networkedCurrent, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (tookHit) OnTakeHit?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Proxy-side mirror: replicated IsAlive went false. Play the local death
    /// visuals (colliders off, death animation) WITHOUT the server report /
    /// quest progress, which the authority already handled in DetectDeath.
    /// </summary>
    public void SyncNetworkedDeath()
    {
        if (isDead) return;
        isDead = true;

        if (boxColl != null) boxColl.enabled = false;
        if (polyColl != null) polyColl.enabled = false;
        if (capsuleColl != null) capsuleColl.enabled = false;

        if (enemyBehaviour != null) enemyBehaviour.SetDeathState();

        OnDeath?.Invoke(this, EventArgs.Empty);
    }

    public void PolyCollTurnOff()
    {
        Debug.Log("Disabled");

        if (polyColl != null) polyColl.enabled = false;
    }
    public void PolyCollTurnOn()
    {
        Debug.Log("Enabled");

        if (polyColl != null) polyColl.enabled = true;
    }


    private void DetectDeath()
    {
        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            if (boxColl != null) boxColl.enabled = false;
            if (polyColl != null) polyColl.enabled = false;
            if (capsuleColl != null) capsuleColl.enabled = false;

            if (enemyBehaviour != null) enemyBehaviour.SetDeathState();
            Debug.Log("Destroy");

            // Báo server khi hạ quái (XP, gold, drop random, khám phá bestiary)
            if (monsterId > 0 && MonsterManager.Instance != null && ApiClient.Instance.HasToken())
            {
                MonsterManager.Instance.ReportDefeat(monsterId, monsterSpawnId > 0 ? monsterSpawnId : null);
            }

            // Cộng dồn tiến độ cho Quest giết quái
            if (QuestManager.Instance != null)
            {
                // Lọc bỏ "(Clone)" hoặc các số phía sau nếu quái được sinh ra từ prefab
                string cleanName = gameObject.name.Replace("(Clone)", "").Trim();
                int spaceIndex = cleanName.IndexOf(" (");
                if (spaceIndex > 0) cleanName = cleanName.Substring(0, spaceIndex);

                string normCleanName = cleanName.Replace(" ", "").Trim();

                foreach (var quest in QuestManager.Instance.GetMainQuests())
                {
                    if (!QuestManager.IsStatus(quest, "InProgress")) continue;
                    if (!string.Equals(quest.ObjectiveType, "Kill", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(quest.ObjectiveType, "Defeat", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.IsNullOrWhiteSpace(quest.ObjectiveTarget)) continue;

                    // ObjectiveTarget có thể liệt kê nhiều loại quái, phân tách bằng '/'
                    foreach (var target in quest.ObjectiveTarget.Split('/'))
                    {
                        string t = target.Trim();
                        string normTarget = t.Replace(" ", "").Trim();

                        bool isMatch = cleanName.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       normCleanName.IndexOf(normTarget, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       normTarget.IndexOf(normCleanName, StringComparison.OrdinalIgnoreCase) >= 0;

                        // Fallback đặc biệt cho Quest 26 "[Chapter 3] The Sealed Guardians" (yêu cầu hạ 2 Boss: GolemBoss & IceFairy)
                        if (!isMatch && quest.QuestId == 26)
                        {
                            if (cleanName.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                cleanName.IndexOf("Fairy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                cleanName.IndexOf("Golem", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isMatch = true;
                            }
                        }

                        if (!isMatch) continue;

                        Debug.Log($"[EnemyEntity] Adding progress to Quest {quest.QuestId} for killing {cleanName}");
                        QuestManager.Instance.AddProgress(quest.QuestId, 1);
                        break;
                    }
                }
            }

            OnDeath?.Invoke(this, EventArgs.Empty);

            StartCoroutine(DespawnAfterDelay(2f));
        }
    }

    private System.Collections.IEnumerator DespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_network != null && _network.IsNetworkActive)
        {
            if (_network.HasStateAuthority)
            {
                _network.Runner.Despawn(_network.Object);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
