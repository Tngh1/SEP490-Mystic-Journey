using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Executes mono behaviour operation.
public class UIQuestRewardSlot : MonoBehaviour
{
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TMP_Text rewardNameText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private GameObject claimOverlay;

    // Initializes internal component caches and dependencies for UIQuestRewardSlot upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        Bind();
    }

    // Executes bind operation.
    private void Bind()
    {
        if (rewardIcon == null)
            rewardIcon = FindChild("Image")?.GetComponent<Image>();

        if (rewardNameText == null)
        {
            rewardNameText = FindChild("RewardName")?.GetComponent<TMP_Text>()
                ?? FindChild("Name")?.GetComponent<TMP_Text>()
                ?? FindChild("Title")?.GetComponent<TMP_Text>()
                ?? FindChild("TitleText")?.GetComponent<TMP_Text>();
        }

        if (quantityText == null)
            quantityText = FindChild("Quantity")?.GetComponent<TMP_Text>();

        if (claimOverlay == null)
            claimOverlay = FindChild("OverlayClaim", "OverlayReward", "ClaimOverlay", "RewardOverlay")?.gameObject;
    }

    // Executes setup operation.
    public void Setup(string rewardName, string amount, Sprite sprite = null)
    {
        Bind();

        if (rewardIcon != null)
        {
            if (sprite != null)
            {
                rewardIcon.sprite = sprite;
                rewardIcon.enabled = true;
            }
            else
            {
                rewardIcon.enabled = false;
            }
        }

        if (rewardNameText != null)
            rewardNameText.text = rewardName ?? string.Empty;

        if (quantityText != null)
            quantityText.text = amount;
    }

    // Executes set claimed operation.
    public void SetClaimed(bool claimed)
    {
        Bind();

        if (claimOverlay != null)
            claimOverlay.SetActive(claimed);
    }

    // Executes find child operation.
    private Transform FindChild(params string[] names)
    {
        if (names == null || names.Length == 0)
            return null;

        var children = GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            for (var j = 0; j < names.Length; j++)
            {
                if (children[i] != null && children[i].name == names[j])
                    return children[i];
            }
        }

        return null;
    }
}
