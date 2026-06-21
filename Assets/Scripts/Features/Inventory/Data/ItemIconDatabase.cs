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

        cache = items.ToDictionary(
            x => x.itemId,
            x => x.icon
        );

        Debug.Log("CACHE COUNT: " + cache.Count);
    }

    public Sprite GetIcon(int itemId)
    {
        Debug.Log("REQUEST ICON ID: " + itemId);

        if (cache.TryGetValue(itemId, out Sprite icon))
        {
            Debug.Log("FOUND ICON");

            return icon;
        }

        Debug.LogError("ICON NOT FOUND");

        return null;
    }
}