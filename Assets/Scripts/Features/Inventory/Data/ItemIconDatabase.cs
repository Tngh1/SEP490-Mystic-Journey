using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ScriptableObject-style MonoBehaviour that maps item sprite icons.
/// 
/// Lookup priority (highest to lowest):
///   1. item.Name  – e.g. "[ITEM] Gold Coin"   → specific icon
///   2. item.Type  – e.g. "Currency"            → fallback icon for that type
///   3. null       – caller should show a default/placeholder sprite
///
/// In the Inspector, add entries using either the item's exact Name or its Type.
/// Name-based entries always win over Type-based entries.
/// </summary>
public class ItemIconDatabase : MonoBehaviour
{
    public static ItemIconDatabase Instance;

    [SerializeField]
    private List<ItemIconEntry> items;

    // key → sprite (string key is either item.Name or item.Type)
    private Dictionary<string, Sprite> _cache;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Unity chỉ nhận DontDestroyOnLoad trên root GameObject. Object này là con của
        // "Managers" trong Main.unity, nên phải detach trước — không thì nó bị destroy
        // khi đổi map và mọi GetIcon() sau đó ném NullReference.
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        BuildCache();
    }

    private void BuildCache()
    {
        _cache = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);

        if (items == null) return;

        foreach (var entry in items)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemKey) || entry.icon == null)
                continue;

            // Last write wins per key — allows overriding fallback types with specific names
            _cache[entry.itemKey.Trim()] = entry.icon;
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Lookup by item Name first, then by item Type as fallback.
    /// </summary>
    public Sprite GetIcon(string itemName, string itemType)
    {
        if (_cache == null) BuildCache();

        // 1. Exact name match
        if (!string.IsNullOrEmpty(itemName) && _cache.TryGetValue(itemName, out var byName) && byName != null)
            return byName;

        // 2. Type fallback
        if (!string.IsNullOrEmpty(itemType) && _cache.TryGetValue(itemType, out var byType) && byType != null)
            return byType;

        return null;
    }

    /// <summary>
    /// Legacy: lookup by itemId (kept for backward compatibility).
    /// Prefer GetIcon(name, type) instead.
    /// </summary>
    [System.Obsolete("Use GetIcon(itemName, itemType) instead. itemId-based lookup is fragile with auto-increment IDs.")]
    public Sprite GetIcon(int itemId)
    {
        Debug.LogWarning($"[ItemIconDatabase] GetIcon(int) called with id={itemId}. " +
                         "Switch to GetIcon(itemName, itemType) for reliable lookups.");
        return null;
    }

    /// <summary>
    /// Lookup by key directly (item.Name or item.Type string).
    /// </summary>
    public bool TryGetIcon(string key, out Sprite icon)
    {
        if (_cache == null) BuildCache();
        return _cache.TryGetValue(key ?? string.Empty, out icon) && icon != null;
    }
}