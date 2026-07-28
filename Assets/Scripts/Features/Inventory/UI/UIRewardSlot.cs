using UnityEngine;

public class UIRewardSlot : UIBaseItemSlot
{
    [Header("Reward Specifics")]
    [SerializeField] private GameObject claimedOverlay; // M�ng ?en m? + Ch? "?� nh?n"

    public void SetupReward(UIItemDisplayData data)
    {
        if (data == null)
        {
            ClearSlot();
            return;
        }

        base.SetupCore(data);

        // L�m m? � n?u ?� nh?n ph?n th??ng n�y (D�ng cho Quest)
        if (claimedOverlay != null)
        {
            claimedOverlay.SetActive(data.isClaimed);
        }
    }

    // Quest rewards carry a formatted amount string ("+500", "x1") instead of an int quantity,
    // so set icon/name/amount directly rather than going through the "xN" quantity path.
    public void SetupQuestReward(string rewardName, string amount, Sprite sprite, bool claimed)
    {
        BindCore();

        RawData = null;

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
        }

        if (itemNameText != null)
            itemNameText.text = rewardName ?? string.Empty;

        if (quantityText != null)
            quantityText.text = amount ?? string.Empty;

        SetHighlight(false);

        if (claimedOverlay != null)
            claimedOverlay.SetActive(claimed);
    }

    public override void ClearSlot()
    {
        base.ClearSlot();
        if (claimedOverlay != null) claimedOverlay.SetActive(false);
    }
}