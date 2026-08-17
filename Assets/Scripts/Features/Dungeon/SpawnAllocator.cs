using System.Collections.Generic;
using UnityEngine;

// Initializes a new default instance of the SpawnAllocator class.
public static class SpawnAllocator
{
    // Process allocate using spawn queue and groups; it removes all, processes claim random point, creates add, and removes at and guards invalid or unavailable states and processes each matching entry.
    public static List<SpawnRequest> Allocate(
        List<DungeonMonsterSpawnData> spawnQueue,
        List<SpawnGroupController> groups)
    {
        var requests = new List<SpawnRequest>();

        var availableGroups = new List<SpawnGroupController>(groups);
        availableGroups.RemoveAll(g => g == null || !g.HasAvailablePoint);

        if (availableGroups.Count == 0)
        {
            Debug.LogWarning("[SpawnAllocator] No SpawnGroups with available points. Allocation aborted.");
            return requests;
        }

        foreach (var monsterData in spawnQueue)
        {
            if (monsterData.Prefab == null)
            {
                Debug.LogWarning($"[SpawnAllocator] {monsterData.MonsterName} has no prefab — skipped.");
                continue;
            }

            int remaining = monsterData.Quantity;

            while (remaining > 0)
            {
                availableGroups.RemoveAll(g => !g.HasAvailablePoint);

                if (availableGroups.Count == 0)
                {
                    Debug.LogWarning($"[SpawnAllocator] All SpawnGroups exhausted. " +
                                     $"{remaining}x '{monsterData.MonsterName}' could not be allocated. " +
                                     "Add more SpawnGroups or SpawnPoints to the scene.");
                    break;
                }

                // Randomize the eligible candidates before selecting this gameplay result.
                int randomIdx = Random.Range(0, availableGroups.Count);
                SpawnGroupController chosenGroup = availableGroups[randomIdx];

                while (remaining > 0 && chosenGroup.HasAvailablePoint)
                {
                    Transform spawnPoint = chosenGroup.ClaimRandomPoint();
                    if (spawnPoint == null) break;

                    requests.Add(new SpawnRequest
                    {
                        MonsterId     = monsterData.MonsterId,
                        MonsterSpawnId = monsterData.MonsterSpawnId,
                        MonsterName   = monsterData.MonsterName,
                        Prefab        = monsterData.Prefab,
                        Position      = spawnPoint.position,
                        GroupName     = chosenGroup.name
                    });

                    remaining--;
                }

                if (!chosenGroup.HasAvailablePoint)
                    availableGroups.RemoveAt(randomIdx);
            }

            if (availableGroups.Count > 1)
            {
                int leastFreeIdx = 0;
                int leastFreeCount = availableGroups[0].Available;
                for (int i = 1; i < availableGroups.Count; i++)
                {
                    if (availableGroups[i].Available < leastFreeCount)
                    {
                        leastFreeCount = availableGroups[i].Available;
                        leastFreeIdx = i;
                    }
                }
                var leastFree = availableGroups[leastFreeIdx];
                availableGroups.RemoveAt(leastFreeIdx);
                availableGroups.Add(leastFree);
            }
        }

        Debug.Log($"[SpawnAllocator] Allocation complete: {requests.Count} slots assigned.");
        return requests;
    }
}
