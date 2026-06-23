using System;
using UnityEngine;

public class EnemyEntity : MonoBehaviour
{
    private PolygonCollider2D polyColl;
    private CapsuleCollider2D capsuleColl;
    private BoxCollider2D boxColl;
    private EnemyBehaviour enemyBehaviour;

    private int currentHealth;
    [SerializeField] private int maxHealth;
    private bool isDead = false;

    public event EventHandler OnTakeHit;
    public event EventHandler OnDeath;

    private void Start()
    {
        polyColl = GetComponent<PolygonCollider2D>();
        capsuleColl = GetComponent<CapsuleCollider2D>();
        boxColl = GetComponent<BoxCollider2D>();
        enemyBehaviour = GetComponent<EnemyBehaviour>();
        currentHealth = maxHealth * 2;
    }
    //private void OnTriggerEnter2D(Collider2D collision)
    //{
    //    Debug.Log("Attack");
    //}

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        
        currentHealth -= damage;
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
