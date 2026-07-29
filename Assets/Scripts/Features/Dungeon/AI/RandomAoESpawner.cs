using System.Collections;
using UnityEngine;

public class RandomAoESpawner : MonoBehaviour
{
    [Header("Random AoE Settings")]
    [Tooltip("Prefab của chiêu thức (ví dụ: cột lửa, quả cầu lửa)")]
    [SerializeField] private GameObject aoePrefab;
    
    [Tooltip("Tổng số lượng cột lửa sẽ được sinh ra")]
    [SerializeField] private int numberOfSpawns = 5;
    
    [Tooltip("Khoảng cách tối thiểu (Vùng an toàn ở giữa để tránh rớt trúng đỉnh đầu)")]
    [SerializeField] private float minSpawnRadius = 1.5f;

    [Tooltip("Bán kính vùng ngẫu nhiên tối đa (tính từ tâm điểm)")]
    [SerializeField] private float spawnRadius = 3f;
    
    [Tooltip("Khoảng thời gian trễ giữa mỗi lần gọi cột lửa tiếp theo")]
    [SerializeField] private float spawnDelay = 0.2f;

    [Tooltip("Nếu tick: Sẽ sinh ngẫu nhiên xung quanh khu vực Player đang đứng. Nếu không tick: Sẽ sinh quanh con Boss.")]
    [SerializeField] private bool spawnAroundPlayer = true;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip castSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private void Start()
    {
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        StartCoroutine(SpawnRandomAoECoroutine());
    }

    private IEnumerator SpawnRandomAoECoroutine()
    {
        Transform targetTransform = transform; // Mặc định tâm điểm là vị trí của Spawner này (tại Boss)
        
        if (spawnAroundPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                targetTransform = player.transform; // Lấy Player làm tâm điểm
            }
        }

        for (int i = 0; i < numberOfSpawns; i++)
        {
            // Lấy tâm điểm liên tục (để lửa bám theo bước chân Player nếu Player di chuyển)
            Vector3 centerPos = targetTransform != null ? targetTransform.position : transform.position;
            
            // Lấy 1 hướng ngẫu nhiên
            Vector2 randomDir = Random.insideUnitCircle;
            if (randomDir == Vector2.zero) randomDir = Vector2.up;
            randomDir.Normalize();
            
            // Lấy 1 khoảng cách ngẫu nhiên từ viền trong (min) đến viền ngoài (max)
            float randomDist = Random.Range(minSpawnRadius, spawnRadius);
            
            Vector3 spawnPos = centerPos + new Vector3(randomDir.x, randomDir.y, 0f) * randomDist;
            
            if (aoePrefab != null)
            {
                Instantiate(aoePrefab, spawnPos, Quaternion.identity);
            }

            if (spawnDelay > 0)
            {
                yield return new WaitForSeconds(spawnDelay);
            }
        }

        // Tự huỷ Spawner sau khi kết thúc
        Destroy(gameObject, 2f);
    }
}
