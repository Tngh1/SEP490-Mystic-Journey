using UnityEngine;

// Executes mono behaviour operation.
public class HealBossSkill : MonoBehaviour
{
    [Header("Heal Settings")]
    [Tooltip("Tỷ lệ % Máu tối đa hồi phục cho Boss (mặc định 15%)")]
    [SerializeField] private float healPercent = 15f;

    [Tooltip("Thời gian tồn tại của cột sáng hồi máu trước khi biến mất (giây)")]
    [SerializeField] private float lifeTime = 1.2f;

    [Header("Positioning Offset")]
    [Tooltip("Offset chiều cao để cột sáng rọi vào giữa thân Boss (mặc định Y = 1.5m)")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Audio Settings")]
    [SerializeField] private AudioClip healSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    // Performs startup initialization for HealBossSkill on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (healSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(healSound, soundVolume);
        }

        EnemyEntity targetBoss = FindTargetBoss();
        if (targetBoss != null)
        {
            AlignPositionToBossCenter(targetBoss);
            ApplyHeal(targetBoss);
        }

        Destroy(gameObject, lifeTime);
    }

    // Executes align position to boss center operation.
    private void AlignPositionToBossCenter(EnemyEntity boss)
    {
        Transform foundPoint = boss.transform.Find("SpawnPoint") ?? boss.transform.Find("SkillSpawn");
        if (foundPoint != null)
        {
            transform.position = foundPoint.position;
        }
        else
        {
            transform.position = boss.transform.position + localOffset;
        }
    }

    // Restores player health clamped to MaxHp and triggers combat popup visual effects.
    private void ApplyHeal(EnemyEntity boss)
    {
        if (boss.IsDead) return;

        int healAmount = Mathf.RoundToInt(boss.MaxHealth * (healPercent / 100f));
        boss.Heal(healAmount);

        if (DamagePopupManager.Instance != null && healAmount > 0)
        {
            DamagePopupManager.Instance.Create(boss.transform.position + new Vector3(0f, 1.5f, 0f), healAmount, false, false, true);
        }
    }

    // Executes find target boss operation.
    private EnemyEntity FindTargetBoss()
    {
        EnemyEntity parentBoss = GetComponentInParent<EnemyEntity>();
        if (parentBoss != null && parentBoss.GetComponent<IceFairySupportAI>() == null) return parentBoss;

        EnemyEntity[] enemies = FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None);
        EnemyEntity nearestBoss = null;
        float minDistance = float.MaxValue;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.gameObject == this.gameObject || enemy.IsDead) continue;

            if (enemy.GetComponent<IceFairySupportAI>() != null || enemy.gameObject.name.Contains("IceFairy")) continue;

            float dist = Vector3.Distance(transform.position, enemy.transform.position);

            bool isBossCandidate = enemy.gameObject.name.IndexOf("Golem", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  enemy.gameObject.name.IndexOf("Boss", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isBossCandidate && dist < 6f && dist < minDistance)
            {
                minDistance = dist;
                nearestBoss = enemy;
            }
            else if (nearestBoss == null && dist < 5f)
            {
                nearestBoss = enemy;
            }
        }

        return nearestBoss;
    }
}
