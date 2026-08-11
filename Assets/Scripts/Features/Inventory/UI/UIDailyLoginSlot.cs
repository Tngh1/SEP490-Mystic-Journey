using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        // Trạng thái đã nhận thưởng
        if (claimedOverlay != null)
            claimedOverlay.SetActive(data.isClaimed);

        // Trạng thái nhận bù (quên đăng nhập ngày trước)
        if (missedOverlay != null)
            missedOverlay.SetActive(data.isMissed && !data.isClaimed);

        // Trạng thái ngày hôm nay (sẵn sàng nhận)
        if (todayOverlay != null)
            todayOverlay.SetActive(data.isAvailable && !data.isClaimed);

        // Trạng thái bị khóa (các ngày tương lai)
        bool isLocked = !data.isClaimed && !data.isAvailable && !data.isMissed;
        if (lockOverlay != null)
            lockOverlay.SetActive(isLocked);

        if (claimButton != null)
        {
            // Phải cho phép click (interactable = true) nếu là ngày hiện tại hoặc ngày đã lỡ
            bool canClick = (data.isAvailable || data.isMissed) && !data.isClaimed;
            claimButton.interactable = canClick;

            // Nếu là ngày lỡ (missed), ta tự làm tối màu tay để giả lập trạng thái disable
            if (data.isMissed && !data.isClaimed)
            {
                if (claimButton.targetGraphic != null)
                    claimButton.targetGraphic.color = new Color(0.5f, 0.5f, 0.5f, 1f); // Xám tối
                if (iconImage != null)
                    iconImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            }
            else
            {
                // Reset lại màu bình thường cho các ngày khác
                if (claimButton.targetGraphic != null)
                    claimButton.targetGraphic.color = Color.white;
                if (iconImage != null)
                    iconImage.color = Color.white;
            }
        }
    }

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

    private void OnClaimButtonClicked()
    {
        if (currentData != null && (currentData.isAvailable || currentData.isMissed) && !currentData.isClaimed)
            OnSlotClicked?.Invoke(this);
    }

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