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
            // itemId is PlayerSkinId, which is 0 for implicitly granted default skins.
            // An equipped skin is always owned, so never lock it.
            lockedContainer.SetActive(data.itemId <= 0 && !data.isEquipped);
        }
    }

    public override void ClearSlot()
    {
        base.ClearSlot();
        if (inUseContainer != null) inUseContainer.SetActive(false);
        if (lockedContainer != null) lockedContainer.SetActive(false);
    }
}
