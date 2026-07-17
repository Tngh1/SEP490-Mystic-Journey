using System.Collections;
using UnityEngine;

public class LineSkillSpawner : MonoBehaviour
{
    [Header("Line Skill Settings")]
    [Tooltip("Prefab của khối băng (hoặc skill) thực sự sẽ được sinh ra")]
    [SerializeField] private GameObject blockPrefab;
    
    [Tooltip("Số lượng khối băng trên đường thẳng")]
    [SerializeField] private int numberOfBlocks = 5;
    
    [Tooltip("Khoảng cách giữa các khối băng")]
    [SerializeField] private float distanceBetweenBlocks = 1.5f;
    
    [Tooltip("Thời gian trễ giữa mỗi lần sinh ra khối băng tiếp theo (để tạo hiệu ứng chạy tới)")]
    [SerializeField] private float spawnDelay = 0.1f;

    private void Start()
    {
        StartCoroutine(SpawnLineCoroutine());
    }

    private IEnumerator SpawnLineCoroutine()
    {
        // Tìm mục tiêu là Player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) yield break;

        Vector3 startPosition = transform.position;
        Vector3 playerPos = player.transform.position;
        
        // Tính toán hướng từ Spawner (đặt ở Boss) tới Player
        Vector3 direction = (playerPos - startPosition).normalized;

        for (int i = 1; i <= numberOfBlocks; i++)
        {
            // Tính toán vị trí sinh ra cho từng khối băng
            Vector3 spawnPos = startPosition + direction * (i * distanceBetweenBlocks);
            
            // Spawn khối băng
            if (blockPrefab != null)
            {
                Instantiate(blockPrefab, spawnPos, Quaternion.identity);
            }
            
            // Nếu có delay thì chờ một chút để khối băng mọc lên dần dần
            if (spawnDelay > 0)
            {
                yield return new WaitForSeconds(spawnDelay);
            }
        }

        // Tự huỷ Spawner sau khi đã hoàn thành việc gọi ra các khối băng
        Destroy(gameObject, 2f);
    }
}
