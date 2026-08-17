using System.Collections;
using UnityEngine;

// Executes mono behaviour operation.
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

    // Initializes internal component caches and dependencies for DragonAttackShooter upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _enemyBehaviour = GetComponent<EnemyBehaviour>();
        if (spawnPoint == null)
        {
            Transform foundPoint = transform.Find("SkillSpawn") ?? transform.Find("FirePoint");
            spawnPoint = foundPoint != null ? foundPoint : transform;
        }
        GetComponent<NetworkEnemy>()?.RegisterSkillPrefab(fireballPrefab);
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        if (_enemyBehaviour != null)
        {
            _enemyBehaviour.OnEnemyAttack += HandleEnemyAttack;
        }
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        if (_enemyBehaviour != null)
        {
            _enemyBehaviour.OnEnemyAttack -= HandleEnemyAttack;
        }
    }

    // Executes handle enemy attack operation.
    private void HandleEnemyAttack(object sender, System.EventArgs e)
    {
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(SpawnFireballRoutine());
    }

    // Create fireball routine; it creates fireball.
    private IEnumerator SpawnFireballRoutine()
    {
        yield return new WaitForSeconds(fireballSpawnDelay);

        SpawnFireball();
    }

    // Executes spawn fireball operation.
    public void SpawnFireball()
    {
        if (fireballPrefab == null)
        {
            Debug.LogWarning($"[DragonAttackShooter] Chưa gán fireballPrefab trên {gameObject.name}!");
            return;
        }

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        var networkEnemy = GetComponent<NetworkEnemy>();
        GameObject fireballObj = networkEnemy != null
            ? networkEnemy.SpawnEnemySkill(fireballPrefab, spawnPos)
            : Instantiate(fireballPrefab, spawnPos, Quaternion.identity);

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
