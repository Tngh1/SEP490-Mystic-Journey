using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemIconDatabase : MonoBehaviour
{
    public static ItemIconDatabase Instance;

    [SerializeField]
    private List<ItemIconEntry> items;

    private Dictionary<int, Sprite> cache;

    private void Awake()
    {
        Instance = this;
        cache = (items ?? new List<ItemIconEntry>())
            .Where(x => x != null && x.icon != null)
            .GroupBy(x => x.itemId)
            .ToDictionary(group => group.Key, group => group.First().icon);
    }

    public bool TryGetIcon(int itemId, out Sprite icon)
    {
        if (cache == null)
            cache = new Dictionary<int, Sprite>();

        return cache.TryGetValue(itemId, out icon) && icon != null;
    }

    public Sprite GetIcon(int itemId)
    {
        return TryGetIcon(itemId, out var icon) ? icon : null;
    }
}