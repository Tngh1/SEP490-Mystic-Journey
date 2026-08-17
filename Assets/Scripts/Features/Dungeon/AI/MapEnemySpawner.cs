using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Executes mono behaviour operation.
public class MapEnemySpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    [Tooltip("Danh sách các loại quái có thể sinh ra tại điểm này. Hệ thống sẽ chọn ngẫu nhiên 1 trong số này mỗi lần gọi.")]
    [SerializeField] private List<GameObject> enemyPrefabs;

    [Tooltip("Số lượng quái tối đa tồn tại cùng lúc từ điểm này.")]
    [SerializeField] private int maxEnemies = 5;

    [Tooltip("Bán kính mọc quái xung quanh điểm này.")]
    [SerializeField] private float spawnRadius = 3f;

    [Tooltip("Thời gian chờ trước khi sinh bù 1 con quái mới (tính từ lúc con cũ chết).")]
    [SerializeField] private float respawnCooldown = 5f;

    [Tooltip("Thời gian chờ giữa mỗi lần gọi 1 con quái lúc mới bắt đầu (tránh giật lag).")]
    [SerializeField] private float initialSpawnDelay = 0.5f;

    [Header("Object Settings")]
    [Tooltip("Tên của GameObject sau khi sinh ra. Để trống sẽ giữ nguyên tên Prefab.")]
    [SerializeField] private string objectName = "";

    private readonly List<GameObject> aliveEnemies = new();

    private int spawnCounter = 0;

    // Performs startup initialization for MapEnemySpawner on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogWarning($"[MapEnemySpawner] Chưa có Enemy Prefab nào được gán vào spawner ở {gameObject.name}");
            return;
        }

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(InitialSpawnRoutine());
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        StopAllCoroutines();
        foreach (var enemy in aliveEnemies)
        {
            if (enemy != null)
            {
                EnemyEntity entity = enemy.GetComponent<EnemyEntity>();
                if (entity != null)
                {
                    entity.OnDeath -= HandleEnemyDeath;
                }
            }
        }
        aliveEnemies.Clear();
    }

    // Executes initial spawn routine operation.
    private IEnumerator InitialSpawnRoutine()
    {
        for (int i = 0; i < maxEnemies; i++)
        {
            if (this == null || !gameObject.activeInHierarchy || !enabled)
                yield break;

            SpawnSingleEnemy();
            yield return new WaitForSeconds(initialSpawnDelay);
        }
    }

    // Executes spawn single enemy operation.
    private void SpawnSingleEnemy()
    {
        if (this == null || !gameObject.activeInHierarchy || !enabled)
            return;

        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
            return;

        // Randomize the eligible candidates before selecting this gameplay result.
        int randomIndex = Random.Range(0, enemyPrefabs.Count);
        GameObject prefabToSpawn = enemyPrefabs[randomIndex];
        if (prefabToSpawn == null) return;

        Vector3 spawnPosition = GetValidSpawnPosition();

        GameObject newEnemy = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity, transform);

        var navAgent = newEnemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null && navAgent.enabled)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(spawnPosition, out var navHit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                navAgent.Warp(navHit.position);
            }
        }

        if (!string.IsNullOrWhiteSpace(objectName))
        {
            spawnCounter++;
            newEnemy.name = $"{objectName} {spawnCounter}";
        }

        aliveEnemies.Add(newEnemy);

        EnemyEntity entity = newEnemy.GetComponent<EnemyEntity>();
        if (entity != null)
        {
            entity.OnDeath += HandleEnemyDeath;
        }
        else
        {
            Debug.LogWarning(
                $"[MapEnemySpawner] Prefab {prefabToSpawn.name} không có component EnemyEntity, hệ thống không thể biết khi nào nó chết!");
        }
    }

    // Executes handle enemy death operation.
    private void HandleEnemyDeath(object sender, System.EventArgs e)
    {
        if (this == null || !gameObject.activeInHierarchy || !enabled)
            return;

        EnemyEntity deadEntity = sender as EnemyEntity;

        if (deadEntity == null)
            return;

        deadEntity.OnDeath -= HandleEnemyDeath;

        aliveEnemies.Remove(deadEntity.gameObject);

        if (gameObject.activeInHierarchy && enabled)
        {
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(RespawnRoutine());
        }
    }

    // Executes respawn routine operation.
    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnCooldown);

        if (this == null || !gameObject.activeInHierarchy || !enabled)
            yield break;

        if (aliveEnemies.Count < maxEnemies)
        {
            SpawnSingleEnemy();
        }
    }

    // Executes get valid spawn position operation.
    private Vector3 GetValidSpawnPosition()
    {
        Vector3 bestPosition = transform.position;
        int maxAttempts = 15;
        float checkRadius = 0.4f;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomPoint = Random.insideUnitCircle * spawnRadius;
            Vector3 candidatePos = transform.position + new Vector3(randomPoint.x, randomPoint.y, 0f);

            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(candidatePos, checkRadius);
            bool isBlocked = false;

            foreach (var col in hitColliders)
            {
                if (col == null || col.isTrigger) continue;
                if (col.gameObject.CompareTag("Player") || col.GetComponent<EnemyEntity>() != null || col.transform.IsChildOf(transform)) continue;

                isBlocked = true;
                break;
            }

            if (isBlocked) continue;

            if (UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out UnityEngine.AI.NavMeshHit navHit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                return navHit.position;
            }
            else if (hitColliders.Length == 0)
            {
                return candidatePos;
            }
        }

        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit fallbackHit, spawnRadius + 3f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return fallbackHit.position;
        }

        return bestPosition;
    }

    // Executes on draw gizmos selected operation.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
