using System;
using Fusion;
using MysticJourney.API.Core;
using UnityEngine;

// Executes mono behaviour operation.
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

    [SerializeField] private int def = 0;

    // Executes def operation.
    public int Def => def;

    // Executes monster id operation.
    public int MonsterId => monsterId;
    // Executes current health operation.
    public int CurrentHealth => currentHealth;
    // Executes max health operation.
    public int MaxHealth => maxHealth;
    // Executes monster spawn id operation.
    public int MonsterSpawnId => monsterSpawnId;

    // Executes set spawn data operation.
    public void SetSpawnData(int id, int spawnId)
    {
        monsterId = id;
        monsterSpawnId = spawnId;
    }


    public event EventHandler OnTakeHit;
    public event EventHandler OnDeath;
    public event Action<int, int> OnHealthChanged;

    // Initializes internal component caches and dependencies for EnemyEntity upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        currentHealth = maxHealth;
    }

    // Performs startup initialization for EnemyEntity on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        polyColl = GetComponent<PolygonCollider2D>();
        capsuleColl = GetComponent<CapsuleCollider2D>();
        boxColl = GetComponent<BoxCollider2D>();
        enemyBehaviour = GetComponent<EnemyBehaviour>();

        if (monsterId <= 0)
        {
            string nameForId = gameObject.name.Replace("(Clone)", "").Replace(" ", "").Trim();
            if (nameForId.IndexOf("IceFairy", StringComparison.OrdinalIgnoreCase) >= 0 || nameForId.IndexOf("Fairy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                monsterId = 21;
            }
            else if (nameForId.IndexOf("GolemBoss", StringComparison.OrdinalIgnoreCase) >= 0 || nameForId.IndexOf("Golem", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                monsterId = 10;
            }
        }

        Debug.Log($"[EnemyEntity] Start: {gameObject.name} | UseApi={useApiStats} | ID={monsterId} | ManagerNull?={MonsterManager.Instance == null}");

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (useApiStats && monsterId > 0 && MonsterManager.Instance != null)
        {
            var cached = MonsterManager.Instance.GetCachedMonster(monsterId);
            Debug.Log($"[EnemyEntity] Cached for {monsterId} is null? {cached == null}");
            if (cached != null)
            {
                ApplyApiStats(cached.MaxHp, cached.Atk, cached.MoveSpeed, cached.Def, cached.CritRate, cached.CritDamage);
            }
            else
            {
                Debug.Log($"[EnemyEntity] Calling LoadMonsterDetail for {monsterId}");
                MonsterManager.Instance.LoadMonsterDetail(monsterId, false, detail =>
                {
                    Debug.Log($"[EnemyEntity] LoadMonsterDetail callback for {monsterId}. detail is null? {detail == null}");
                    if (detail != null && !isDead)
                    {
                        ApplyApiStats(detail.MaxHp, detail.Atk, detail.MoveSpeed, detail.Def, detail.CritRate, detail.CritDamage);
                    }
                });
            }
        }
    }

    // Executes apply api stats operation.
    private void ApplyApiStats(int apiMaxHp, int apiAtk, int apiMoveSpeed, int apiDef, int apiCritRate, int apiCritDamage)
    {
        Debug.Log($"[EnemyEntity] {gameObject.name} ApplyApiStats: HP={apiMaxHp}, ATK={apiAtk}, SPD={apiMoveSpeed}, DEF={apiDef}, CRIT={apiCritRate}/{apiCritDamage}");
        maxHealth = apiMaxHp;
        currentHealth = maxHealth;
        def = Mathf.Max(0, apiDef);
        if (enemyBehaviour != null)
        {
            enemyBehaviour.UpdateStatsFromAPI(apiAtk, apiMoveSpeed, apiCritRate, apiCritDamage);
        }
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }


    private NetworkEnemy _network;

    // Executes is dead operation.
    public bool IsDead => isDead;

    // Executes bind network operation.
    public void BindNetwork(NetworkEnemy network) => _network = network;

    // Executes network operation.
    public NetworkEnemy Network => (_network != null && _network.IsNetworkActive) ? _network : null;

    // Executes heal operation.
    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // Executes take damage operation.
    public void TakeDamage(int damage, PlayerRef attacker = default)
    {
        if (isDead) return;

        var cocoonShield = GetComponent<ResurrectionCocoonShield>();
        if (cocoonShield != null && cocoonShield.TryBlockHit())
        {
            return;
        }

        if (_network != null && _network.IsNetworkActive)
        {
            _network.RequestDamage(damage, attacker);
            return;
        }

        ApplyDamageAuthoritative(damage);
    }

    // Executes apply damage authoritative operation.
    public void ApplyDamageAuthoritative(int damage)
    {
        if (isDead) return;

        var cocoonShield = GetComponent<ResurrectionCocoonShield>();
        if (cocoonShield != null && cocoonShield.TryBlockHit())
        {
            return;
        }

        int reduced = Mathf.RoundToInt(def / 5f);
        int finalDamage = Mathf.Max(Mathf.RoundToInt(damage * 0.5f), damage - reduced);
        if (finalDamage < 1) finalDamage = 1;

        currentHealth -= finalDamage;
        if (currentHealth < 0) currentHealth = 0;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnTakeHit?.Invoke(this, EventArgs.Empty);
        DetectDeath();
    }

    // Executes sync networked health operation.
    public void SyncNetworkedHealth(int networkedCurrent, int networkedMax)
    {
        if (networkedMax > 0) maxHealth = networkedMax;

        bool tookHit = networkedCurrent < currentHealth;
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        currentHealth = Mathf.Clamp(networkedCurrent, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (tookHit) OnTakeHit?.Invoke(this, EventArgs.Empty);
    }

    // Executes sync networked death operation.
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

    // Executes poly coll turn off operation.
    public void PolyCollTurnOff()
    {
        if (polyColl != null && (capsuleColl != null || boxColl != null))
        {
            polyColl.enabled = false;
        }
    }
    // Executes poly coll turn on operation.
    // Validates input parameters against null or empty values.
    public void PolyCollTurnOn()
    {
        if (polyColl != null) polyColl.enabled = true;
    }


    // Executes detect death operation.
    // Validates input parameters against null or empty values.
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

            if ((_network == null || !_network.IsNetworkActive) &&
                MysticJourney.Features.Monster.MonsterDropVisualManager.Instance != null)
            {
                MysticJourney.Features.Monster.MonsterDropVisualManager.Instance.RegisterMonsterDeathPosition(monsterId, transform.position);
            }

            if (monsterId > 0 && MonsterManager.Instance != null && ApiClient.Instance.HasToken())
            {
                if (_network != null && _network.IsNetworkActive)
                {
                    _network.NotifyKillerReward();
                }
                else
                {
                    MonsterManager.Instance.ReportDefeat(
                        monsterId,
                        monsterSpawnId > 0 ? monsterSpawnId : null,
                        DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon
                            ? DungeonManager.Instance.CurrentSessionId
                            : (int?)null);
                }
            }

            if (QuestUIManager.Instance != null)
            {
                string cleanName = gameObject.name.Replace("(Clone)", "").Trim();
                int spaceIndex = cleanName.IndexOf(" (");
                if (spaceIndex > 0) cleanName = cleanName.Substring(0, spaceIndex);

                string normCleanName = cleanName.Replace(" ", "").Trim();

                foreach (var quest in QuestUIManager.Instance.GetMainQuests())
                {
                    if (!QuestUIManager.IsStatus(quest, "InProgress")) continue;
                    if (!string.Equals(quest.ObjectiveType, "Kill", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(quest.ObjectiveType, "Defeat", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.IsNullOrWhiteSpace(quest.ObjectiveTarget)) continue;

                    foreach (var target in quest.ObjectiveTarget.Split('/'))
                    {
                        string t = target.Trim();
                        string normTarget = t.Replace(" ", "").Trim();

                        bool isMatch = cleanName.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       normCleanName.IndexOf(normTarget, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       normTarget.IndexOf(normCleanName, StringComparison.OrdinalIgnoreCase) >= 0;

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
                        QuestUIManager.Instance.AddProgress(quest.QuestId, 1);
                        break;
                    }
                }
            }

            OnDeath?.Invoke(this, EventArgs.Empty);

            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(DespawnAfterDelay(2f));
        }
    }

    // Executes despawn after delay operation.
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
