using UnityEngine;
using TMPro;

public class UIDailyLoginSlot : UIBaseItemSlot
{
    [Header("Daily Login Specifics")]
    [SerializeField] private TMP_Text dayText; // VD: "Ngày 1", "Ngày 2"
    [SerializeField] private GameObject claimedOverlay; // ?ã ?i?m danh
    [SerializeField] private GameObject todayHighlight; // Vi?n sáng nh?p nháy cho ngày hi?n t?i

    public void SetupDailyLogin(UIItemDisplayData data, bool isToday)
    {
        if (data == null)
        {
            ClearSlot();
            return;
        }

        base.SetupCore(data);

        if (dayText != null)
        {
            dayText.text = $"Ngày {data.dayNumber}";
        }

        if (claimedOverlay != null)
        {
            claimedOverlay.SetActive(data.isClaimed);
        }

        // B?t highlight n?u ô này là ph?n th??ng c?a ngày hôm nay
        if (todayHighlight != null)
        {
            todayHighlight.SetActive(isToday && !data.isClaimed);
        }
    }

    public override void ClearSlot()
    {
        base.ClearSlot();
        if (claimedOverlay != null) claimedOverlay.SetActive(false);
        if (todayHighlight != null) todayHighlight.SetActive(false);
        if (dayText != null) dayText.text = string.Empty;
    }
}