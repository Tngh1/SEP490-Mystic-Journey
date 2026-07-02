using UnityEngine;

/// <summary>
/// Immutable result produced by SpawnAllocator for a single enemy slot.
/// Holds everything DungeonSpawner needs to Instantiate one monster,
/// but contains NO Unity-side operations — it is purely data.
///
/// Separating allocation from instantiation lets you:
///   • Add spawn animations / portal effects between the two phases.
///   • Preview the layout in the editor before any objects are created.
///   • Replay a dungeon without rebuilding the allocation plan.
/// </summary>
public class SpawnRequest
{
    /// <summary>Backend database ID of the monster type.</summary>
    public int MonsterId;

    /// <summary>Backend MonsterSpawnId; forwarded to EnemyEntity for defeat reporting.</summary>
    public int MonsterSpawnId;

    /// <summary>Display name used for debug logs and the spawned GameObject name.</summary>
    public string MonsterName;

    /// <summary>The Unity prefab to instantiate. Must contain an EnemyEntity component.</summary>
    public GameObject Prefab;

    /// <summary>World position of the claimed SpawnPoint Transform at allocation time.</summary>
    public Vector3 Position;

    /// <summary>Name of the SpawnGroup this slot was allocated from (for debug logging only).</summary>
    public string GroupName;
}
