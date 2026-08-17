// Initializes a new default instance of the InventoryItemData class.
[System.Serializable]
public class InventoryItemData
{
    public int inventoryId;

    public int itemId;

    public string itemName;

    public int quantity;

    // Supported rarity values: Common, Uncommon, Rare, Epic, Legendary, or Mythic; rarity controls quality, visuals, and sorting priority.
    public string rarity;

    public string iconUrl;

    public string slotType;
}
