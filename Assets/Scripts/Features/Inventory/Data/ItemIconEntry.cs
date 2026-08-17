using UnityEngine;

// Initializes a new default instance of the ItemIconEntry class.
[System.Serializable]
public class ItemIconEntry
{
    [Tooltip("Use item.Name (e.g. '[ITEM] Gold Coin') for specific icon, " +
             "or item.Type (e.g. 'Currency', 'Weapon') for fallback icon.")]
    public string itemKey;

    public Sprite icon;
}
