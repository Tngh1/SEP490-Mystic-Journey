using UnityEngine;

/// <summary>
/// Skill Kén Phục Sinh (Resurrection Cocoon) do Boss UnderKing thi triển (mặc định 10s 1 lần).
/// Tác dụng:
/// 1. Hồi 20% máu tối đa cho UnderKing.
/// 2. Tạo Kén bao bọc UnderKing giúp chặn 2 đòn đánh / kỹ năng từ Player.
/// </summary>
public class ResurrectionCocoonSkill : MonoBehaviour
{
    [Header("Skill Settings")]
    [Tooltip("Tỷ lệ % máu tối đa được hồi phục (mặc định 20%)")]
    [SerializeField] private float healPercent = 20f;

    [Tooltip("Số đòn đánh/kỹ năng chặn được (mặc định 2 đòn)")]
    [SerializeField] private int maxBlockHits = 2;

    [Tooltip("Thời gian tồn tại tối đa của Kén nếu Player không đánh đủ 2 đòn (giây)")]
    [SerializeField] private float shieldMaxDuration = 8f;

    [Header("Positioning Offset")]
    [Tooltip("Offset vị trí của Kén để bao trọn thân người Boss (mặc định Y = 1.25m)")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.25f, 0f);

    [Header("Audio Settings")]
    [SerializeField] private AudioClip castSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private void Start()
    {
        // Phát âm thanh thi triển kỹ năng Kén Phục Sinh
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        // Tìm Boss UnderKing gần nhất hoặc parent
        EnemyEntity bossEntity = GetComponentInParent<EnemyEntity>();
        if (bossEntity == null)
        {
            bossEntity = FindNearestBoss();
        }

        if (bossEntity != null)
        {
            ApplyCocoonToBoss(bossEntity);
        }
        else
        {
            Debug.LogWarning("[ResurrectionCocoonSkill] Không tìm thấy Boss UnderKing để áp dụng Kén!");
            Destroy(gameObject, 0.5f);
        }
    }

    private void ApplyCocoonToBoss(EnemyEntity boss)
    {
        // 1. Hồi 20% Máu tối đa cho Boss UnderKing
        int healAmount = Mathf.RoundToInt(boss.MaxHealth * (healPercent / 100f));
        boss.Heal(healAmount);

        // Hiển thị Popup Hồi Máu màu Xanh Lá
        if (DamagePopupManager.Instance != null && healAmount > 0)
        {
            DamagePopupManager.Instance.Create(boss.transform.position, healAmount, false, false, true);
        }

        // 2. Gắn visual Kén bao bọc quanh Boss tại tâm thân người (Offset Y)
        transform.SetParent(boss.transform);
        Transform foundSpawnPoint = boss.transform.Find("SpawnPoint") ?? boss.transform.Find("SkillSpawn");
        if (foundSpawnPoint != null)
        {
            transform.position = foundSpawnPoint.position;
        }
        else
        {
            transform.localPosition = localOffset;
        }

        // 3. Khởi tạo/Cập nhật Khiên chặn 2 đòn đánh trên Boss UnderKing
        ResurrectionCocoonShield existingShield = boss.GetComponent<ResurrectionCocoonShield>();
        if (existingShield != null)
        {
            existingShield.Initialize(maxBlockHits, gameObject);
        }
        else
        {
            ResurrectionCocoonShield newShield = boss.gameObject.AddComponent<ResurrectionCocoonShield>();
            newShield.Initialize(maxBlockHits, gameObject);
        }

        // Tự động giải phóng kén sau khoảng thời gian tối đa nếu không bị đánh vỡ
        Destroy(gameObject, shieldMaxDuration);
    }

    private EnemyEntity FindNearestBoss()
    {
        EnemyEntity[] enemies = FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None);
        EnemyEntity nearestBoss = null;
        float minDistance = float.MaxValue;

        foreach (var enemy in enemies)
        {
            if (enemy.gameObject.name.Contains("UnderKing"))
            {
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestBoss = enemy;
                }
            }
        }

        // Fallback: Lấy quái gần nhất trong bán kính 5m
        if (nearestBoss == null)
        {
            foreach (var enemy in enemies)
            {
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist < 5f && dist < minDistance)
                {
                    minDistance = dist;
                    nearestBoss = enemy;
                }
            }
        }

        return nearestBoss;
    }
}
