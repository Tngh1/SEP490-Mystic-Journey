using UnityEngine;

// Executes ui base item slot operation.
public class UIInventorySkinSlot : UIBaseItemSlot
{
    [Header("Skin Specifics")]
    [SerializeField] private GameObject inUseContainer;
    [SerializeField] private GameObject lockedContainer;

    // Executes setup skin operation.
    public void SetupSkin(UIItemDisplayData data)
    {
        if (data == null)
        {
            ClearSlot();
            return;
        }

        base.SetupCore(data);

        if (inUseContainer != null)
        {
            inUseContainer.SetActive(data.isEquipped);
        }

        if (lockedContainer != null)
        {
            lockedContainer.SetActive(data.itemId <= 0 && !data.isEquipped);
        }
    }

    // Executes clear slot operation.
    public override void ClearSlot()
    {
        base.ClearSlot();
        if (inUseContainer != null) inUseContainer.SetActive(false);
        if (lockedContainer != null) lockedContainer.SetActive(false);
    }
}
