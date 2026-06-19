using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIQuestRewardSlot : MonoBehaviour
{
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private GameObject claimOverlay;

    private void Awake()
    {
        Bind();
    }

    private void Bind()
    {
        if (rewardIcon == null)
            rewardIcon = transform.Find("Image")?.GetComponent<Image>();

        if (quantityText == null)
            quantityText = transform.Find("Quantity")?.GetComponent<TMP_Text>();

        if (claimOverlay == null)
            claimOverlay = transform.Find("OverlayClaim")?.gameObject;
    }

    // KHỚP với MainQuestPanelRuntime
    public void Setup(string rewardName, string amount, Sprite sprite = null)
    {
        Bind();

        if (rewardIcon != null && sprite != null)
            rewardIcon.sprite = sprite;

        if (quantityText != null)
            quantityText.text = amount;
    }

    public void SetClaimed(bool claimed)
    {
        if (claimOverlay != null)
            claimOverlay.SetActive(claimed);
    }
}