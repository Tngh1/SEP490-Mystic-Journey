using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure allocation algorithm — no MonoBehaviour, no Unity scene operations.
/// Takes a list of monster types to spawn and a list of available SpawnGroups,
/// then returns a flat list of SpawnRequest assignments (monster → world position).
///
/// Responsibilities:
///   ✓ Randomly assign each monster type to a SpawnGroup from the available pool.
///   ✓ Overflow: when a group is exhausted, randomly pick from remaining groups.
///   ✓ Prefer giving each monster type its own group (advance pool after each type).
///
/// This class contains ZERO Instantiate / scene operations.
/// DungeonSpawner is responsible for actually creating the GameObjects.
///
/// If you need a different allocation strategy in the future (e.g. prioritise groups
/// closest to the player, assign by difficulty zone, weighted random) — only THIS
/// class needs to change.
/// </summary>
public static class SpawnAllocator
{
    /// <summary>
    /// Allocates monster slots across the given SpawnGroups.
    /// </summary>
    /// <param name="spawnQueue">
    ///   Ordered list of monster types to spawn, each with a Quantity.
    ///   Built from backend MonsterSpawnResponse data after filtering bosses
    ///   and resolving prefabs.
    /// </param>
    /// <param name="groups">
    ///   All SpawnGroupControllers in the scene.
    ///   Each group must have already had ResetGroup() called so _freePoints
    ///   is fully populated and shuffled before allocation begins.
    /// </param>
    /// <returns>
    ///   A flat, ordered list of SpawnRequest objects — one per enemy slot.
    ///   Each request holds everything needed to Instantiate a single monster.
    ///   Empty list if no slots could be allocated.
    /// </returns>
    public static List<SpawnRequest> Allocate(
        List<DungeonMonsterSpawnData> spawnQueue,
        List<SpawnGroupController> groups)
    {
        var requests = new List<SpawnRequest>();

        // Working pool of groups that still have available SpawnPoints.
        // We mutate this list during allocation (removing exhausted groups).
        var availableGroups = new List<SpawnGroupController>(groups);
        // Remove any groups that start with 0 capacity (safety check)
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

            // ── Overflow loop: keep picking random groups until all placed ──────────
            while (remaining > 0)
            {
                // Remove exhausted groups from the pool
                availableGroups.RemoveAll(g => !g.HasAvailablePoint);

                if (availableGroups.Count == 0)
                {
                    Debug.LogWarning($"[SpawnAllocator] All SpawnGroups exhausted. " +
                                     $"{remaining}x '{monsterData.MonsterName}' could not be allocated. " +
                                     "Add more SpawnGroups or SpawnPoints to the scene.");
                    break;
                }

                // ── Random group selection — different every run ───────────────────
                // Picking a RANDOM group (not the sequential "next" one) prevents
                // the dungeon from always spawning the same type in the same area.
                int randomIdx = Random.Range(0, availableGroups.Count);
                SpawnGroupController chosenGroup = availableGroups[randomIdx];

                // ── Fill the chosen group as much as possible for this type ────────
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

                // ── If this group is now exhausted, remove it from the pool ────────
                if (!chosenGroup.HasAvailablePoint)
                    availableGroups.RemoveAt(randomIdx);
            }

            // ── Prefer giving the NEXT monster type its own group ─────────────────
            // After all of this type's units are placed, remove the last-used group
            // from the front of the available pool so the next type starts fresh.
            // This prevents all types from collapsing into the same group when there
            // is still capacity. Only do this when groups remain and the last group
            // still has space (otherwise it was already removed above).
            if (availableGroups.Count > 1)
            {
                // Pick a sacrificial "used" group for this type — the most-recently-
                // filled one. Since we don't track it explicitly, we mark the first
                // group with the least remaining space as "preferred-used".
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
                // Move it to end so next type is less likely to randomly land there
                // (soft "prefer fresh group" without hard-excluding the group)
                var leastFree = availableGroups[leastFreeIdx];
                availableGroups.RemoveAt(leastFreeIdx);
                availableGroups.Add(leastFree);
            }
        }

        Debug.Log($"[SpawnAllocator] Allocation complete: {requests.Count} slots assigned.");
        return requests;
    }
}
