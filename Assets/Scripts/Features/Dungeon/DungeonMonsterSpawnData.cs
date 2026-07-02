using UnityEngine;

/// <summary>
/// Aggregated, runtime-ready representation of one monster type to be spawned in a dungeon.
/// Built by DungeonSpawner from backend MonsterSpawnResponse data + local MonsterDatabaseSO prefab lookup.
/// </summary>
[System.Serializable]
public class DungeonMonsterSpawnData
{
    /// <summary>Backend database ID of the monster type.</summary>
    public int MonsterId;

    /// <summary>Backend MonsterSpawnId used by EnemyEntity to report defeats to the server.</summary>
    public int MonsterSpawnId;

    /// <summary>Display name from the backend (used for debug logging).</summary>
    public string MonsterName;

    /// <summary>Total number of this monster type to spawn in this dungeon run.</summary>
    public int Quantity;

    /// <summary>
    /// The Unity prefab resolved from MonsterDatabaseSO for this MonsterId.
    /// The prefab must have an EnemyEntity component.
    /// </summary>
    public GameObject Prefab;
}
