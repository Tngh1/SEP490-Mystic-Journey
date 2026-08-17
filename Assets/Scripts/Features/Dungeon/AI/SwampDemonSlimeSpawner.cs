using UnityEngine;

// Executes mono behaviour operation.
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

    // Initializes internal component caches and dependencies for SwampDemonSlimeSpawner upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _enemyEntity = GetComponent<EnemyEntity>();
        GetComponent<NetworkEnemy>()?.RegisterSkillPrefab(slimeMiniPrefab);
    }

    // Per-frame update loop for SwampDemonSlimeSpawner.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (_enemyEntity != null && (_enemyEntity.IsDead || _enemyEntity.CurrentHealth <= 0)) return;

        Transform targetPlayer = FindPlayerTarget();
        if (targetPlayer == null) return;

        float distance = Vector3.Distance(transform.position, targetPlayer.position);
        if (distance > combatDetectionRange) return;

        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            SummonSlimeMinis();
        }
    }

    // Executes find player target operation.
    private Transform FindPlayerTarget()
    {
        if (PlayerMovement.Instance != null && PlayerMovement.Instance.gameObject.activeInHierarchy)
        {
            return PlayerMovement.Instance.transform;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null && player.activeInHierarchy ? player.transform : null;
    }

    // Executes summon slime minis operation.
    public void SummonSlimeMinis()
    {
        if (slimeMiniPrefab == null)
        {
            Debug.LogWarning($"[SwampDemonSlimeSpawner] Chưa gán slimeMiniPrefab trên {gameObject.name}!");
            return;
        }

        GetComponent<NetworkEnemy>()?.NotifySkillAnimation();

        if (summonSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(summonSound, soundVolume);
        }

        for (int i = 0; i < slimeCount; i++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            // Randomize the eligible candidates before selecting this gameplay result.
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
