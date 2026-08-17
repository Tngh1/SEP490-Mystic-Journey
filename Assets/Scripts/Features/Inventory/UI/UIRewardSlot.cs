using UnityEngine;

// Executes ui base item slot operation.
public class UIRewardSlot : UIBaseItemSlot
{
    [Header("Reward Specifics")]
    [SerializeField] private GameObject claimedOverlay;

    // Executes setup reward operation.
    public void SetupReward(UIItemDisplayData data)
    {
        if (data == null)
        {
            ClearSlot();
            return;
        }

        base.SetupCore(data);

        if (claimedOverlay != null)
        {
            claimedOverlay.SetActive(data.isClaimed);
        }
    }

    // Executes setup quest reward operation.
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

    // Executes clear slot operation.
    public override void ClearSlot()
    {
        base.ClearSlot();
        if (claimedOverlay != null) claimedOverlay.SetActive(false);
    }
}
