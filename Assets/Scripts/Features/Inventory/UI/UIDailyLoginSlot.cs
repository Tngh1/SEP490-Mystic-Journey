using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Executes ui base item slot operation.
public class UIDailySlot : UIBaseItemSlot
{
    [Header("Daily Specifics")]
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private GameObject claimedOverlay;
    [SerializeField] private GameObject missedOverlay;
    [SerializeField] private GameObject todayOverlay;
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private Button claimButton;

    private UIItemDisplayData currentData;

    // Initializes internal component caches and dependencies for UIDailySlot upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        BindDailyReferences();
        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaimButtonClicked);
    }

    // Executes setup daily operation.
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

        if (quantityText != null)
            quantityText.text = data.quantity > 1 ? data.quantity.ToString() : string.Empty;

        if (claimedOverlay != null)
            claimedOverlay.SetActive(data.isClaimed);

        if (missedOverlay != null)
            missedOverlay.SetActive(data.isMissed && !data.isClaimed);

        if (todayOverlay != null)
            todayOverlay.SetActive(data.isAvailable && !data.isClaimed);

        bool isLocked = !data.isClaimed && !data.isAvailable && !data.isMissed;
        if (lockOverlay != null)
            lockOverlay.SetActive(isLocked);

        if (claimButton != null)
        {
            bool canClick = (data.isAvailable || data.isMissed) && !data.isClaimed;
            claimButton.interactable = canClick;

            if (data.isMissed && !data.isClaimed)
            {
                if (claimButton.targetGraphic != null)
                    claimButton.targetGraphic.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                if (iconImage != null)
                    iconImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            }
            else
            {
                if (claimButton.targetGraphic != null)
                    claimButton.targetGraphic.color = Color.white;
                if (iconImage != null)
                    iconImage.color = Color.white;
            }
        }
    }

    // Executes clear slot operation.
    public override void ClearSlot()
    {
        currentData = null;
        base.ClearSlot();

        if (dayText != null)
            dayText.text = string.Empty;
        if (claimedOverlay != null)
            claimedOverlay.SetActive(false);
        if (missedOverlay != null)
            missedOverlay.SetActive(false);
        if (todayOverlay != null)
            todayOverlay.SetActive(false);
        if (lockOverlay != null)
            lockOverlay.SetActive(false);
        if (claimButton != null)
            claimButton.interactable = false;
    }

    // Executes on claim button clicked operation.
    private void OnClaimButtonClicked()
    {
        if (currentData != null && (currentData.isAvailable || currentData.isMissed) && !currentData.isClaimed)
            OnSlotClicked?.Invoke(this);
    }

    // Executes bind daily references operation.
    private void BindDailyReferences()
    {
        if (dayText == null)
            dayText = FindChild("DayText", "Day", "TitleText")?.GetComponent<TMP_Text>();
        if (claimedOverlay == null)
            claimedOverlay = FindChild("ClaimedIcon", "ClaimedOverlay", "OverlayClaim", "OverlayReward", "Claimed")?.gameObject;
        if (missedOverlay == null)
            missedOverlay = FindChild("ReClaim", "MissedOverlay", "OverlayMissed", "ReclaimOverlay", "Reclaim")?.gameObject;
        if (todayOverlay == null)
            todayOverlay = FindChild("Today", "TodayOverlay", "TodayHighlight", "DailyItemToday")?.gameObject;
        if (lockOverlay == null)
            lockOverlay = FindChild("Lock", "LockOverlay", "Locked", "DailyLock")?.gameObject;
        if (claimButton == null)
            claimButton = GetComponent<Button>() ?? FindChild("ClaimButton", "Button")?.GetComponent<Button>();
    }

    // Executes find child operation.
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
