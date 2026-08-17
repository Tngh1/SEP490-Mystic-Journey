using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Executes mono behaviour operation.
public class ItemIconDatabase : MonoBehaviour
{
    public static ItemIconDatabase Instance;

    [SerializeField]
    private List<ItemIconEntry> items;

    private Dictionary<string, Sprite> _cache;

    // Initializes internal component caches and dependencies for ItemIconDatabase upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        BuildCache();
    }

    // Executes build cache operation.
    // Validates input parameters against null or empty values.
    private void BuildCache()
    {
        _cache = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);

        if (items == null) return;

        foreach (var entry in items)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemKey) || entry.icon == null)
                continue;

            _cache[entry.itemKey.Trim()] = entry.icon;
        }
    }


    // Executes get icon operation.
    // Validates input parameters against null or empty values.
    public Sprite GetIcon(string itemName, string itemType)
    {
        if (_cache == null) BuildCache();

        if (!string.IsNullOrEmpty(itemName) && _cache.TryGetValue(itemName, out var byName) && byName != null)
            return byName;

        if (!string.IsNullOrEmpty(itemType) && _cache.TryGetValue(itemType, out var byType) && byType != null)
            return byType;

        return null;
    }

    [System.Obsolete("Use GetIcon(itemName, itemType) instead. itemId-based lookup is fragile with auto-increment IDs.")]
    // Executes get icon operation.
    public Sprite GetIcon(int itemId)
    {
        Debug.LogWarning($"[ItemIconDatabase] GetIcon(int) called with id={itemId}. " +
                         "Switch to GetIcon(itemName, itemType) for reliable lookups.");
        return null;
    }

    // Executes try get icon operation.
    public bool TryGetIcon(string key, out Sprite icon)
    {
        if (_cache == null) BuildCache();
        return _cache.TryGetValue(key ?? string.Empty, out icon) && icon != null;
    }
}
