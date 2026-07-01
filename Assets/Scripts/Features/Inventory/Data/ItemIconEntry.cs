using UnityEngine;

/// <summary>
/// Maps a string key to a Sprite icon.
/// Key can be item Type (e.g. "Weapon", "Currency") for a fallback icon,
/// or item Name (e.g. "[ITEM] Gold Coin") for a specific icon.
/// Name takes priority over Type when both exist.
/// </summary>
[System.Serializable]
public class ItemIconEntry
{
    [Tooltip("Use item.Name (e.g. '[ITEM] Gold Coin') for specific icon, " +
             "or item.Type (e.g. 'Currency', 'Weapon') for fallback icon.")]
    public string itemKey;

    public Sprite icon;
}