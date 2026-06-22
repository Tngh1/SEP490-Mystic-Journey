using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIDailySlot : UIBaseItemSlot
{
    [Header("Daily Specifics")]
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private GameObject claimedOverlay;
    [SerializeField] private Button claimButton;

    private UIItemDisplayData currentData;

    private void Awake()
    {
        BindDailyReferences();
        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaimButtonClicked);
    }

    public void SetupDaily(UIItemDisplayData data)
    {
        BindDailyReferences();
        currentData = data;

        if (data == null)
        {
            ClearSlot();
            return;
        }

        base.SetupCore(data);
        RawData = data;

        if (dayText != null)
            dayText.text = "Day " + data.dayNumber;

        if (claimedOverlay != null)
            claimedOverlay.SetActive(data.isClaimed);

        if (claimButton != null)
            claimButton.interactable = data.isAvailable && !data.isClaimed;
    }

    public override void ClearSlot()
    {
        currentData = null;
        base.ClearSlot();

        if (dayText != null)
            dayText.text = string.Empty;
        if (claimedOverlay != null)
            claimedOverlay.SetActive(false);
        if (claimButton != null)
            claimButton.interactable = false;
    }

    private void OnClaimButtonClicked()
    {
        if (currentData != null && currentData.isAvailable && !currentData.isClaimed)
            OnSlotClicked?.Invoke(this);
    }

    private void BindDailyReferences()
    {
        if (dayText == null)
            dayText = FindChild("DayText", "Day", "TitleText")?.GetComponent<TMP_Text>();
        if (claimedOverlay == null)
            claimedOverlay = FindChild("ClaimedOverlay", "OverlayClaim", "OverlayReward")?.gameObject;
        if (claimButton == null)
            claimButton = GetComponent<Button>() ?? FindChild("ClaimButton", "Button")?.GetComponent<Button>();
    }

    private Transform FindChild(params string[] names)
    {
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