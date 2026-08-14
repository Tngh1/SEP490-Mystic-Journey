using UnityEngine;

/// <summary>
/// Spawner triệu hồi Slime Mini xung quanh Boss SwampDemon.
/// Cứ mỗi `spawnInterval` (mặc định 2s), Boss sẽ triệu hồi `slimeCount` (mặc định 3 con)
/// SlimeMini xuất hiện ngẫu nhiên trong bán kính xung quanh Boss.
/// </summary>
public class SwampDemonSlimeSpawner : MonoBehaviour
{
    [Header("Summon Settings")]
    [Tooltip("Prefab của con SlimeMini")]
    [SerializeField] private GameObject slimeMiniPrefab;

    [Tooltip("Khoảng thời gian giữa mỗi lần triệu hồi (giây)")]
    [SerializeField] private float spawnInterval = 2f;

    [Tooltip("Số lượng SlimeMini triệu hồi mỗi lần")]
    [SerializeField] private int slimeCount = 3;

    [Tooltip("Bán kính tối thiểu xuất hiện xung quanh Boss")]
    [SerializeField] private float minRadius = 1.5f;

    [Tooltip("Bán kính tối đa xuất hiện xung quanh Boss")]
    [SerializeField] private float maxRadius = 4.5f;

    [Header("Combat Settings")]
    [Tooltip("Phạm vi nhận diện giao tranh với Player (mét) - Chỉ triệu hồi khi Player trong phạm vi này")]
    [SerializeField] private float combatDetectionRange = 8.0f;

    [Header("Audio")]
    [SerializeField] private AudioClip summonSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private float _timer = 0f;
    private EnemyEntity _enemyEntity;

    private void Awake()
    {
        _enemyEntity = GetComponent<EnemyEntity>();
        GetComponent<NetworkEnemy>()?.RegisterSkillPrefab(slimeMiniPrefab);
    }

    private void Update()
    {
        // Nếu Boss đã chết thì ngừng triệu hồi
        if (_enemyEntity != null && (_enemyEntity.IsDead || _enemyEntity.CurrentHealth <= 0)) return;

        Transform targetPlayer = FindPlayerTarget();
        if (targetPlayer == null) return;

        // Chỉ triệu hồi khi Player tiến vào phạm vi giao tranh
        float distance = Vector3.Distance(transform.position, targetPlayer.position);
        if (distance > combatDetectionRange) return;

        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            SummonSlimeMinis();
        }
    }

    private Transform FindPlayerTarget()
    {
        if (PlayerMovement.Instance != null && PlayerMovement.Instance.gameObject.activeInHierarchy)
        {
            return PlayerMovement.Instance.transform;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null && player.activeInHierarchy ? player.transform : null;
    }

    /// <summary>
    /// Hàm thực hiện triệu hồi N con SlimeMini xung quanh vị trí Boss
    /// </summary>
    public void SummonSlimeMinis()
    {
        if (slimeMiniPrefab == null)
        {
            Debug.LogWarning($"[SwampDemonSlimeSpawner] Chưa gán slimeMiniPrefab trên {gameObject.name}!");
            return;
        }

        if (summonSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(summonSound, soundVolume);
        }

        for (int i = 0; i < slimeCount; i++)
        {
            // Tính vị trí ngẫu nhiên trong khoảng bán kính minRadius -> maxRadius
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomDist = Random.Range(minRadius, maxRadius);
            Vector3 spawnPos = transform.position + (Vector3)(randomDir * randomDist);

            var networkEnemy = GetComponent<NetworkEnemy>();
            if (networkEnemy != null)
                networkEnemy.SpawnEnemySkill(slimeMiniPrefab, spawnPos);
            else
                Instantiate(slimeMiniPrefab, spawnPos, Quaternion.identity);
        }
    }
}
