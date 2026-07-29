using UnityEngine;

/// <summary>
/// Skill Cột Sáng Hồi Máu (HealBoss Prefab) do IceFairy triệu hồi lên GolemBoss.
/// Khi xuất hiện tại vị trí GolemBoss:
/// 1. Căn chỉnh vị trí cột sáng lên giữa thân người Boss (bằng Offset Y).
/// 2. Hồi % Máu tối đa cho GolemBoss.
/// 3. Hiển thị Popup HP màu xanh lá.
/// 4. Tự hủy sau khi hiệu ứng cột sáng chạy xong.
/// </summary>
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

    private void Start()
    {
        // 1. Phát âm thanh hồi máu
        if (healSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(healSound, soundVolume);
        }

        // 2. Tìm Boss GolemBoss tại vị trí xuất hiện hoặc gần nhất
        EnemyEntity targetBoss = FindTargetBoss();
        if (targetBoss != null)
        {
            // Căn chỉnh vị trí cột sáng lên giữa thân Boss
            AlignPositionToBossCenter(targetBoss);
            ApplyHeal(targetBoss);
        }

        // 3. Tự động dọn dẹp Cột sáng sau lifeTime
        Destroy(gameObject, lifeTime);
    }

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

    private void ApplyHeal(EnemyEntity boss)
    {
        if (boss.IsDead) return;

        int healAmount = Mathf.RoundToInt(boss.MaxHealth * (healPercent / 100f));
        boss.Heal(healAmount);

        // Hiển thị Popup HP màu Xanh Lá
        if (DamagePopupManager.Instance != null && healAmount > 0)
        {
            DamagePopupManager.Instance.Create(boss.transform.position + new Vector3(0f, 1.5f, 0f), healAmount, false, false, true);
        }
    }

    private EnemyEntity FindTargetBoss()
    {
        // Ưu tiên lấy từ Parent nếu Prefab được Instantiate làm con của Boss
        EnemyEntity parentBoss = GetComponentInParent<EnemyEntity>();
        if (parentBoss != null) return parentBoss;

        // Tìm quái gần nhất trong bán kính 4m
        EnemyEntity[] enemies = FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None);
        EnemyEntity nearestBoss = null;
        float minDistance = float.MaxValue;

        foreach (var enemy in enemies)
        {
            if (enemy.gameObject != this.gameObject && !enemy.IsDead)
            {
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist < 4f && dist < minDistance)
                {
                    minDistance = dist;
                    nearestBoss = enemy;
                }
            }
        }

        return nearestBoss;
    }
}
