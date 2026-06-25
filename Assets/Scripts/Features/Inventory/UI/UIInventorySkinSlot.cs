using UnityEngine;

public class UIInventorySkinSlot : UIBaseItemSlot
{
    [Header("Skin Specifics")]
    [SerializeField] private GameObject inUseContainer;
    [SerializeField] private GameObject lockedContainer;

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
            // Lock is active if itemId is 0 (meaning unowned)
            // or we use a separate flag. For now, assuming itemId <= 0 means unowned.
            lockedContainer.SetActive(data.itemId <= 0);
        }
    }

    public override void ClearSlot()
    {
        base.ClearSlot();
        if (inUseContainer != null) inUseContainer.SetActive(false);
        if (lockedContainer != null) lockedContainer.SetActive(false);
    }
}
