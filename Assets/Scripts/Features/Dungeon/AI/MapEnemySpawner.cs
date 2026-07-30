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

    /// <summary>
    /// Routine sinh ra đợt quái đầu tiên một cách từ từ
    /// </summary>
    private IEnumerator InitialSpawnRoutine()
    {
        for (int i = 0; i < maxEnemies; i++)
        {
            SpawnSingleEnemy();
            yield return new WaitForSeconds(initialSpawnDelay);
        }
    }

    /// <summary>
    /// Sinh ra một con quái
    /// </summary>
    private void SpawnSingleEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
            return;

        // Chọn ngẫu nhiên một prefab
        int randomIndex = Random.Range(0, enemyPrefabs.Count);
        GameObject prefabToSpawn = enemyPrefabs[randomIndex];

        // Tạo vị trí ngẫu nhiên trong bán kính
        Vector2 randomPoint = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPosition = transform.position + new Vector3(randomPoint.x, randomPoint.y, 0f);

        // Sinh quái
        GameObject newEnemy = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

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
        EnemyEntity deadEntity = sender as EnemyEntity;

        if (deadEntity == null)
            return;

        // Hủy đăng ký sự kiện
        deadEntity.OnDeath -= HandleEnemyDeath;

        // Xóa khỏi danh sách
        aliveEnemies.Remove(deadEntity.gameObject);

        // Chờ rồi sinh lại
        StartCoroutine(RespawnRoutine());
    }

    /// <summary>
    /// Chờ rồi sinh bù 1 con quái
    /// </summary>
    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnCooldown);

        if (aliveEnemies.Count < maxEnemies)
        {
            SpawnSingleEnemy();
        }
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