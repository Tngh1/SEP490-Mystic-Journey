using UnityEngine;

// Executes mono behaviour operation.
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

    // Performs startup initialization for ResurrectionCocoonSkill on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

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

    // Executes apply cocoon to boss operation.
    private void ApplyCocoonToBoss(EnemyEntity boss)
    {
        int healAmount = Mathf.RoundToInt(boss.MaxHealth * (healPercent / 100f));
        boss.Heal(healAmount);

        if (DamagePopupManager.Instance != null && healAmount > 0)
        {
            DamagePopupManager.Instance.Create(boss.transform.position, healAmount, false, false, true);
        }

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

        Destroy(gameObject, shieldMaxDuration);
    }

    // Executes find nearest boss operation.
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
