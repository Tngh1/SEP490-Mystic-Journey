using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    // Danh sách lưu trữ những con quái đang sống
    private readonly List<GameObject> aliveEnemies = new();

    // Dùng để đánh số nếu đặt Object Name
    private int spawnCounter = 0;

    private void Start()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogWarning($"[MapEnemySpawner] Chưa có Enemy Prefab nào được gán vào spawner ở {gameObject.name}");
            return;
        }

        // Bắt đầu gọi quái lần đầu tiên
        StartCoroutine(InitialSpawnRoutine());
    }

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

    /// <summary>
    /// Routine sinh ra đợt quái đầu tiên một cách từ từ
    /// </summary>
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

    /// <summary>
    /// Sinh ra một con quái
    /// </summary>
    private void SpawnSingleEnemy()
    {
        if (this == null || !gameObject.activeInHierarchy || !enabled)
            return;

        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
            return;

        // Chọn ngẫu nhiên một prefab
        int randomIndex = Random.Range(0, enemyPrefabs.Count);
        GameObject prefabToSpawn = enemyPrefabs[randomIndex];
        if (prefabToSpawn == null) return;

        // Lấy vị trí sinh quái hợp lệ (không bị kẹt trong vật thể / tường và nằm trên NavMesh đi được)
        Vector3 spawnPosition = GetValidSpawnPosition();

        // Sinh quái GẮN LÀM CON CỦA SPAWNER (transform) để quái thuộc cùng Scene với Spawner.
        // Nhờ đó khi Unload Scene map cũ, toàn bộ quái do spawner sinh ra sẽ tự động bị huỷ theo scene,
        // không bị sót lại hay rơi sang scene persistent (Main) làm xuất hiện ở map khác.
        GameObject newEnemy = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity, transform);

        // Snap NavMeshAgent của quái vào vị trí NavMesh chuẩn để tránh kẹt
        var navAgent = newEnemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null && navAgent.enabled)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(spawnPosition, out var navHit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                navAgent.Warp(navHit.position);
            }
        }

        // Đổi tên nếu có nhập Object Name
        if (!string.IsNullOrWhiteSpace(objectName))
        {
            spawnCounter++;
            newEnemy.name = $"{objectName} {spawnCounter}";
        }

        aliveEnemies.Add(newEnemy);

        // Đăng ký sự kiện chết
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

    /// <summary>
    /// Được gọi khi một con quái chết
    /// </summary>
    private void HandleEnemyDeath(object sender, System.EventArgs e)
    {
        if (this == null || !gameObject.activeInHierarchy || !enabled)
            return;

        EnemyEntity deadEntity = sender as EnemyEntity;

        if (deadEntity == null)
            return;

        // Hủy đăng ký sự kiện
        deadEntity.OnDeath -= HandleEnemyDeath;

        // Xóa khỏi danh sách
        aliveEnemies.Remove(deadEntity.gameObject);

        // Chờ rồi sinh lại (chỉ chạy coroutine nếu spawner vẫn đang sống và active)
        if (gameObject.activeInHierarchy && enabled)
        {
            StartCoroutine(RespawnRoutine());
        }
    }

    /// <summary>
    /// Chờ rồi sinh bù 1 con quái
    /// </summary>
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

    /// <summary>
    /// Tìm vị trí sinh quái hợp lệ: Không bị đè lên vật cản (Colliders) và nằm trên NavMesh đi được
    /// </summary>
    private Vector3 GetValidSpawnPosition()
    {
        Vector3 bestPosition = transform.position;
        int maxAttempts = 15;
        float checkRadius = 0.4f;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomPoint = Random.insideUnitCircle * spawnRadius;
            Vector3 candidatePos = transform.position + new Vector3(randomPoint.x, randomPoint.y, 0f);

            // 1. Kiểm tra va chạm Physics2D với các vật thể cản cứng (không tính Trigger, Player, Enemy)
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

            // 2. Kiểm tra vị trí candidate xem có nằm trên vùng NavMesh chuẩn không
            if (UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out UnityEngine.AI.NavMeshHit navHit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                return navHit.position;
            }
            else if (hitColliders.Length == 0)
            {
                return candidatePos;
            }
        }

        // Fallback: nếu sau 15 lần lấy vị trí ngẫu nhiên không được, lấy vị trí NavMesh gần nhất xung quanh Spawner
        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit fallbackHit, spawnRadius + 3f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return fallbackHit.position;
        }

        return bestPosition;
    }

    /// <summary>
    /// Vẽ vùng spawn trong Scene View
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}