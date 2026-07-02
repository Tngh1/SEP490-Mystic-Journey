using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Placed on each SpawnGroup_X GameObject in the dungeon scene.
/// Manages a pool of child SpawnPoint Transforms using a List{Transform} for
/// efficient random-claim, removal, and full reset — supporting dungeon replay
/// without reloading the scene.
///
/// Expected scene hierarchy:
///   SpawnGroup_A   ← SpawnGroupController lives here
///     ├── SpawnPoint01
///     ├── SpawnPoint02
///     └── SpawnPoint15
/// </summary>
public class SpawnGroupController : MonoBehaviour
{
    // ── Source list — never mutated after Awake ──────────────────────────────────
    /// <summary>All child SpawnPoints discovered once at Awake. Immutable source of truth.</summary>
    private readonly List<Transform> _allPoints = new();

    // ── Working list — mutated each dungeon run ───────────────────────────────────
    /// <summary>
    /// Currently available (unclaimed) SpawnPoints for this run.
    /// Starts as a copy of _allPoints, randomly shrinks as points are claimed.
    /// freePoints.Count is the true "Available" count — no extra counter needed.
    /// </summary>
    private readonly List<Transform> _freePoints = new();

    // ═══════════════════════════════════════════════════════════════════════════
    // PUBLIC ACCESSORS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Fixed total number of SpawnPoints in this group. Never changes after Awake.</summary>
    public int Capacity => _allPoints.Count;

    /// <summary>Number of unclaimed SpawnPoints remaining this run.</summary>
    public int Available => _freePoints.Count;

    /// <summary>True when at least one SpawnPoint is still claimable.</summary>
    public bool HasAvailablePoint => _freePoints.Count > 0;

    // ═══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        DiscoverSpawnPoints();
        ResetGroup(); // prepare _freePoints for the first dungeon run
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PUBLIC API — called by SpawnAllocator / DungeonSpawner
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Claims and returns a RANDOM unclaimed SpawnPoint Transform.
    /// The claimed point is removed from _freePoints so it cannot be claimed again
    /// this run. Returns null if all points have been claimed.
    /// </summary>
    public Transform ClaimRandomPoint()
    {
        if (!HasAvailablePoint)
        {
            Debug.LogWarning($"[SpawnGroupController] '{name}' has no remaining SpawnPoints.");
            return null;
        }

        int idx = Random.Range(0, _freePoints.Count);
        Transform point = _freePoints[idx];
        _freePoints.RemoveAt(idx);   // O(n) but list is tiny; cleaner than swap-back
        return point;
    }

    /// <summary>
    /// Resets the group for a new dungeon run WITHOUT reloading the scene.
    /// Restores _freePoints to a full copy of _allPoints, then shuffles.
    /// Call this from DungeonSpawner before distributing monsters each run.
    /// </summary>
    public void ResetGroup()
    {
        _freePoints.Clear();
        _freePoints.AddRange(_allPoints);
        ShuffleFreePoints();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Discovers all direct child Transforms as SpawnPoints.
    /// Inactive children are included so designers can pre-place points
    /// without affecting scene startup state.
    /// </summary>
    private void DiscoverSpawnPoints()
    {
        _allPoints.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
                _allPoints.Add(child);
        }

        if (_allPoints.Count == 0)
            Debug.LogWarning($"[SpawnGroupController] '{name}' has no child SpawnPoints. " +
                             "Add child Transforms as spawn locations.");
    }

    /// <summary>Fisher-Yates in-place shuffle on _freePoints.</summary>
    private void ShuffleFreePoints()
    {
        for (int i = _freePoints.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Transform temp = _freePoints[i];
            _freePoints[i] = _freePoints[j];
            _freePoints[j] = temp;
        }
    }
}
