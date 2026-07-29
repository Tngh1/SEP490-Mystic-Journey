using System.Collections;
using UnityEngine;

/// <summary>
/// Script bắn Cầu Lửa Truy Đuổi cho đòn Đánh Thường của Boss Rồng.
/// Lắng nghe sự kiện OnEnemyAttack của EnemyBehaviour, khi kết thúc/đạt thời điểm
/// animation đánh sẽ sinh ra quả cầu lửa bay truy đuổi người chơi.
/// </summary>
public class DragonAttackShooter : MonoBehaviour
{
    [Header("Fireball Prefab")]
    [Tooltip("Prefab quả cầu lửa truy đuổi (đã gắn script DragonHomingFireball)")]
    [SerializeField] private GameObject fireballPrefab;

    [Tooltip("Vị trí bắn ra quả cầu lửa (nếu để None sẽ lấy vị trí Rồng)")]
    [SerializeField] private Transform spawnPoint;

    [Header("Timing Settings")]
    [Tooltip("Thời gian chờ để animation đánh kết thúc mới bắn quả cầu lửa (giây)")]
    [SerializeField] private float fireballSpawnDelay = 0.6f;

    private EnemyBehaviour _enemyBehaviour;

    private void Awake()
    {
        _enemyBehaviour = GetComponent<EnemyBehaviour>();
        if (spawnPoint == null)
        {
            Transform foundPoint = transform.Find("SkillSpawn") ?? transform.Find("FirePoint");
            spawnPoint = foundPoint != null ? foundPoint : transform;
        }
    }

    private void OnEnable()
    {
        if (_enemyBehaviour != null)
        {
            _enemyBehaviour.OnEnemyAttack += HandleEnemyAttack;
        }
    }

    private void OnDisable()
    {
        if (_enemyBehaviour != null)
        {
            _enemyBehaviour.OnEnemyAttack -= HandleEnemyAttack;
        }
    }

    private void HandleEnemyAttack(object sender, System.EventArgs e)
    {
        StartCoroutine(SpawnFireballRoutine());
    }

    private IEnumerator SpawnFireballRoutine()
    {
        // Chờ animation đánh diễn ra đến thời điểm bắn
        yield return new WaitForSeconds(fireballSpawnDelay);

        SpawnFireball();
    }

    /// <summary>
    /// Hàm sinh quả cầu lửa. Có thể gọi trực tiếp từ Animation Event trong Unity Animation Window.
    /// </summary>
    public void SpawnFireball()
    {
        if (fireballPrefab == null)
        {
            Debug.LogWarning($"[DragonAttackShooter] Chưa gán fireballPrefab trên {gameObject.name}!");
            return;
        }

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject fireballObj = Instantiate(fireballPrefab, spawnPos, Quaternion.identity);

        // Đảm bảo quả cầu lửa biết được mục tiêu Player
        DragonHomingFireball homingScript = fireballObj.GetComponent<DragonHomingFireball>();
        if (homingScript != null)
        {
            Transform playerTarget = PlayerMovement.Instance != null ? PlayerMovement.Instance.transform : null;
            if (playerTarget != null)
            {
                homingScript.SetTarget(playerTarget);
            }
        }
    }
}
