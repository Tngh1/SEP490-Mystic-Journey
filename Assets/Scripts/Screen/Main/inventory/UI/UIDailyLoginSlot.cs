using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIDailySlot : UIBaseItemSlot
{
    [Header("Daily Specifics")]
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private GameObject claimedOverlay; // Kh?i m? ?è lên khi ?ã nh?n
    [SerializeField] private Button claimButton;

    private void Awake()
    {
        if (claimButton != null)
        {
            claimButton.onClick.AddListener(OnClaimButtonClicked);
        }
    }

    public void SetupDaily(UIItemDisplayData data)
    {
        if (data == null)
        {
            ClearSlot();
            return;
        }

        // G?i Lõi ?? v? Icon, Vi?n, S? l??ng
        base.SetupCore(data);

        // V? ngày
        if (dayText != null)
        {
            dayText.text = "Day " + data.dayNumber;
        }

        // B?t/t?t l?p m? "?ã nh?n"
        if (claimedOverlay != null)
        {
            claimedOverlay.SetActive(data.isClaimed);
        }

        // Khóa nút b?m n?u ?ã nh?n r?i (Không cho click n?a)
        if (claimButton != null)
        {
            claimButton.interactable = !data.isClaimed;
        }
    }

    private void OnClaimButtonClicked()
    {
        // Truy?n tín hi?u click ra ngoài
        OnSlotClicked?.Invoke(this);
    }
}