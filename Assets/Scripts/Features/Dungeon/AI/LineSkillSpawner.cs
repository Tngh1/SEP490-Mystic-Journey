using System.Collections;
using UnityEngine;

// Executes mono behaviour operation.
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

    [Header("Audio Settings")]
    [SerializeField] private AudioClip castSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    // Performs startup initialization for LineSkillSpawner on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(SpawnLineCoroutine());
    }

    // Executes spawn line coroutine operation.
    private IEnumerator SpawnLineCoroutine()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) yield break;

        Vector3 startPosition = transform.position;
        Vector3 playerPos = player.transform.position;

        Vector3 direction = (playerPos - startPosition).normalized;

        for (int i = 1; i <= numberOfBlocks; i++)
        {
            Vector3 spawnPos = startPosition + direction * (i * distanceBetweenBlocks);

            if (blockPrefab != null)
            {
                var spawned = Instantiate(blockPrefab, spawnPos, Quaternion.identity);
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
