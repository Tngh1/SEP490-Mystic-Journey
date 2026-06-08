using UnityEngine;

public class UIInventorySlot : UIBaseItemSlot
{
    [Header("Inventory Specifics")]
    [SerializeField] private GameObject equippedIndicator;

    public void SetupInventory(UIItemDisplayData data)
    {
        if (data == null)
        {
            ClearSlot();
            return;
        }

        // G?i logic v? Lõi t? Class Cha
        base.SetupCore(data);

        // B?t/T?t d?u tích V n?u ?? ?ang m?c ho?c Skin ?ang trang b?
        if (equippedIndicator != null)
        {
            equippedIndicator.SetActive(data.isEquipped);
        }
    }

    public override void ClearSlot()
    {
        base.ClearSlot();
        if (equippedIndicator != null) equippedIndicator.SetActive(false);
    }
}