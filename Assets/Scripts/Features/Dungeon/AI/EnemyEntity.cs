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

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        
        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnTakeHit?.Invoke(this, EventArgs.Empty);
        Debug.Log("Damage");
        DetectDeath();
    }

    public void PolyCollTurnOff()
    {
        Debug.Log("Disabled");

        polyColl.enabled = false;
    }
    public void PolyCollTurnOn()
    {
        Debug.Log("Enabled");

        polyColl.enabled = true;
    }


    private void DetectDeath()
    {
        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            boxColl.enabled = false;
            polyColl.enabled = false;
            capsuleColl.enabled = false;

            enemyBehaviour.SetDeathState();
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

                var quests = QuestManager.Instance.GetMainQuests();
                foreach (var quest in quests)
                {
                    if (QuestManager.IsStatus(quest, "InProgress") &&
                        (string.Equals(quest.ObjectiveType, "Kill", StringComparison.OrdinalIgnoreCase) || 
                         string.Equals(quest.ObjectiveType, "Defeat", StringComparison.OrdinalIgnoreCase)) &&
                        string.Equals(quest.ObjectiveTarget, cleanName, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"[EnemyEntity] Adding progress to Quest {quest.QuestId} for killing {cleanName}");
                        QuestManager.Instance.AddProgress(quest.QuestId, 1);
                    }
                }
            }

            OnDeath?.Invoke(this, EventArgs.Empty);
        }
    }
}
