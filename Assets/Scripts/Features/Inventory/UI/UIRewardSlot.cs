using UnityEngine;

public class UIRewardSlot : UIBaseItemSlot
{
    [Header("Reward Specifics")]
    [SerializeField] private GameObject claimedOverlay; // Màng ?en m? + Ch? "?ã nh?n"

    public void SetupReward(UIItemDisplayData data)
    {
        if (data == null)
        {
            ClearSlot();
            return;
        }

        base.SetupCore(data);

        // Làm m? ô n?u ?ã nh?n ph?n th??ng này (Dùng cho Quest)
        if (claimedOverlay != null)
        {
            claimedOverlay.SetActive(data.isClaimed);
        }
    }

    public override void ClearSlot()
    {
        base.ClearSlot();
        if (claimedOverlay != null) claimedOverlay.SetActive(false);
    }
}