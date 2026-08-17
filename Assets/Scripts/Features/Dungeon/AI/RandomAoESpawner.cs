using System.Collections;
using UnityEngine;

// Executes mono behaviour operation.
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

    // Performs startup initialization for RandomAoESpawner on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(SpawnRandomAoECoroutine());
    }

    // Executes spawn random ao e coroutine operation.
    private IEnumerator SpawnRandomAoECoroutine()
    {
        Transform targetTransform = transform;

        if (spawnAroundPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                targetTransform = player.transform;
            }
        }

        for (int i = 0; i < numberOfSpawns; i++)
        {
            Vector3 centerPos = targetTransform != null ? targetTransform.position : transform.position;

            Vector2 randomDir = Random.insideUnitCircle;
            if (randomDir == Vector2.zero) randomDir = Vector2.up;
            randomDir.Normalize();

            // Randomize the eligible candidates before selecting this gameplay result.
            float randomDist = Random.Range(minSpawnRadius, spawnRadius);

            Vector3 spawnPos = centerPos + new Vector3(randomDir.x, randomDir.y, 0f) * randomDist;

            if (aoePrefab != null)
            {
                var spawned = Instantiate(aoePrefab, spawnPos, Quaternion.identity);
                if (EnemySkillVisualReplica.IsReplica(this))
                    spawned.AddComponent<EnemySkillVisualReplica>();
            }

            if (spawnDelay > 0)
            {
                yield return new WaitForSeconds(spawnDelay);
            }
        }

        Destroy(gameObject, 2f);
    }
}
