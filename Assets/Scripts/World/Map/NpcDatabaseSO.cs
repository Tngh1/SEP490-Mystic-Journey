using System.Collections.Generic;
using System.Linq;
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
        if (string.IsNullOrWhiteSpace(name))
            return GetDefaultPrefab();

        string cleaned = CleanName(name);

        // 1. Exact or case-insensitive match
        var mapping = npcMappings.Find(m => m != null && (
            string.Equals(m.npcName, name, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CleanName(m.npcName), cleaned, System.StringComparison.OrdinalIgnoreCase)));

        if (mapping != null && mapping.prefab != null)
            return mapping.prefab;

        // 2. Partial / fuzzy match (e.g. "Elder Rowan (Pumpkin)" matches "Elder Rowan")
        mapping = npcMappings.Find(m => m != null && !string.IsNullOrWhiteSpace(m.npcName) && (
            name.IndexOf(m.npcName, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            m.npcName.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            (!string.IsNullOrWhiteSpace(cleaned) && cleaned.IndexOf(CleanName(m.npcName), System.StringComparison.OrdinalIgnoreCase) >= 0) ||
            (!string.IsNullOrWhiteSpace(cleaned) && CleanName(m.npcName).IndexOf(cleaned, System.StringComparison.OrdinalIgnoreCase) >= 0)));

        if (mapping != null && mapping.prefab != null)
            return mapping.prefab;

        // 3. Fallback to first non-null prefab in database to ensure NPC is always spawned
        return GetDefaultPrefab();
    }

    private GameObject GetDefaultPrefab()
    {
        var mapping = npcMappings.FirstOrDefault(m => m != null && m.prefab != null);
        return mapping?.prefab;
    }

    private static string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        string clean = name.Trim();
        int parenIdx = clean.IndexOf('(');
        if (parenIdx > 0) clean = clean.Substring(0, parenIdx).Trim();
        return clean;
    }
}
