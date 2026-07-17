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

    // Danh sách lưu trữ những con quái đang sống
    private List<GameObject> aliveEnemies = new List<GameObject>();

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
    /// Hàm sinh ra 1 con quái
    /// </summary>
    private void SpawnSingleEnemy()
    {
        if (enemyPrefabs.Count == 0) return;

        // Chọn 1 loại quái ngẫu nhiên
        int randomIndex = Random.Range(0, enemyPrefabs.Count);
        GameObject prefabToSpawn = enemyPrefabs[randomIndex];

        // Tìm một vị trí ngẫu nhiên trong vòng tròn bán kính spawnRadius
        Vector2 randomPoint = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPosition = transform.position + new Vector3(randomPoint.x, randomPoint.y, 0f);

        // Sinh ra quái
        GameObject newEnemy = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        aliveEnemies.Add(newEnemy);

        // Đăng ký theo dõi sự kiện chết của quái
        EnemyEntity entity = newEnemy.GetComponent<EnemyEntity>();
        if (entity != null)
        {
            entity.OnDeath += HandleEnemyDeath;
        }
        else
        {
            Debug.LogWarning($"[MapEnemySpawner] Prefab {prefabToSpawn.name} không có component EnemyEntity, hệ thống không thể biết khi nào nó chết!");
        }
    }

    /// <summary>
    /// Hàm được gọi tự động khi 1 con quái do Spawner này tạo ra bị chết
    /// </summary>
    private void HandleEnemyDeath(object sender, System.EventArgs e)
    {
        EnemyEntity deadEntity = sender as EnemyEntity;
        if (deadEntity != null)
        {
            // Hủy theo dõi sự kiện để tránh lỗi bộ nhớ (memory leak)
            deadEntity.OnDeath -= HandleEnemyDeath;
            aliveEnemies.Remove(deadEntity.gameObject);

            // Bắt đầu đếm ngược để sinh con mới thay thế
            StartCoroutine(RespawnRoutine());
        }
    }

    /// <summary>
    /// Chờ một khoảng thời gian rồi sinh bù 1 con quái mới
    /// </summary>
    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnCooldown);

        // Kiểm tra xem số lượng quái còn sống có đang thấp hơn mức tối đa không
        // (đề phòng trường hợp maxEnemies bị giảm đi trong lúc đang chơi)
        if (aliveEnemies.Count < maxEnemies)
        {
            SpawnSingleEnemy();
        }
    }

    // Vẽ một vòng tròn màu đỏ trong Editor để bạn dễ hình dung vùng mọc quái
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
