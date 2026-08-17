using UnityEngine;

// Executes ui base item slot operation.
public class UIInventorySlot : UIBaseItemSlot
{
    [Header("Inventory Specifics")]
    [SerializeField] private GameObject equippedIndicator;

    // Executes setup inventory operation.
    public void SetupInventory(UIItemDisplayData data)
    {
        if (data == null)
        {
            ClearSlot();
            return;
        }

        base.SetupCore(data);

        if (equippedIndicator != null)
        {
            equippedIndicator.SetActive(data.isEquipped);
        }
    }

    // Executes clear slot operation.
    public override void ClearSlot()
    {
        base.ClearSlot();
        if (equippedIndicator != null) equippedIndicator.SetActive(false);
    }
}
