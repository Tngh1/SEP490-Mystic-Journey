using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NpcPrefabMapping
{
    [Tooltip("Khớp với cột Name trong Database (VD: Elder Rowan, Lyra, v.v.)")]
    public string npcName;
    public GameObject prefab;
}

[CreateAssetMenu(fileName = "NpcDatabase", menuName = "ScriptableObjects/Npc Database")]
public class NpcDatabaseSO : ScriptableObject
{
    public List<NpcPrefabMapping> npcMappings = new List<NpcPrefabMapping>();

    public GameObject GetPrefab(string name)
    {
        var mapping = npcMappings.Find(m => string.Equals(m.npcName, name, System.StringComparison.OrdinalIgnoreCase));
        return mapping?.prefab;
    }
}
