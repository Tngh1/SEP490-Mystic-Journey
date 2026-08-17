using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Initializes a new default instance of the NpcPrefabMapping class.
[System.Serializable]
public class NpcPrefabMapping
{
    [Tooltip("Khớp với cột Name trong Database (VD: Elder Rowan, Lyra, v.v.)")]
    public string npcName;
    public GameObject prefab;
}

// Executes scriptable object operation.
// Validates input parameters against null or empty values.
[CreateAssetMenu(fileName = "NpcDatabase", menuName = "ScriptableObjects/Npc Database")]
public class NpcDatabaseSO : ScriptableObject
{
    public List<NpcPrefabMapping> npcMappings = new List<NpcPrefabMapping>();

    // Executes get prefab operation.
    // Validates input parameters against null or empty values.
    public GameObject GetPrefab(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return GetDefaultPrefab();

        string cleaned = CleanName(name);

        var mapping = npcMappings.Find(m => m != null && (
            string.Equals(m.npcName, name, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CleanName(m.npcName), cleaned, System.StringComparison.OrdinalIgnoreCase)));

        if (mapping != null && mapping.prefab != null)
            return mapping.prefab;

        mapping = npcMappings.Find(m => m != null && !string.IsNullOrWhiteSpace(m.npcName) && (
            name.IndexOf(m.npcName, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            m.npcName.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            (!string.IsNullOrWhiteSpace(cleaned) && cleaned.IndexOf(CleanName(m.npcName), System.StringComparison.OrdinalIgnoreCase) >= 0) ||
            (!string.IsNullOrWhiteSpace(cleaned) && CleanName(m.npcName).IndexOf(cleaned, System.StringComparison.OrdinalIgnoreCase) >= 0)));

        if (mapping != null && mapping.prefab != null)
            return mapping.prefab;

        return GetDefaultPrefab();
    }

    // Load default prefab; it selects the matching record.
    private GameObject GetDefaultPrefab()
    {
        var mapping = npcMappings.FirstOrDefault(m => m != null && m.prefab != null);
        return mapping?.prefab;
    }

    // Executes clean name operation.
    // Validates input parameters against null or empty values.
    private static string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        string clean = name.Trim();
        int parenIdx = clean.IndexOf('(');
        if (parenIdx > 0) clean = clean.Substring(0, parenIdx).Trim();
        return clean;
    }
}
