using System;
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

        int currentDay = DateTime.UtcNow.Day;
        bool isPastExpired = !data.isClaimed && !data.isAvailable && !data.isMissed && data.dayNumber < currentDay;
        bool isFutureLocked = !data.isClaimed && !data.isAvailable && !data.isMissed && data.dayNumber > currentDay;

        if (lockOverlay != null)
            lockOverlay.SetActive(isFutureLocked);

        if (claimButton != null)
        {
            bool canClick = (data.isAvailable || data.isMissed) && !data.isClaimed;
            claimButton.interactable = canClick;

            if ((data.isMissed || isPastExpired) && !data.isClaimed)
            {
                if (claimButton.targetGraphic != null)
                    claimButton.targetGraphic.color = new Color(0.45f, 0.45f, 0.45f, 1f);
                if (iconImage != null)
                    iconImage.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            }
            else
            {
                if (claimButton.targetGraphic != null)
                    claimButton.targetGraphic.color = Color.white;
                if (iconImage != null)
                    iconImage.color = Color.white;
            }
        }

        ApplyResponsiveLayout();
    }

    // Executes core business logic for apply responsive layout.
    private void ApplyResponsiveLayout()
    {
        if (dayText != null)
        {
            var rect = dayText.rectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.05f, 0.76f);
                rect.anchorMax = new Vector2(0.95f, 0.96f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                dayText.enableAutoSizing = true;
                dayText.fontSizeMin = 8f;
                dayText.fontSizeMax = 28f;
                dayText.alignment = TextAlignmentOptions.Center;
            }
        }

        if (iconImage != null)
        {
            var rect = iconImage.rectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.15f, 0.24f);
                rect.anchorMax = new Vector2(0.85f, 0.74f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                iconImage.preserveAspect = true;
            }
        }

        if (lockOverlay != null)
        {
            var rect = lockOverlay.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.35f, -0.05f);
                rect.anchorMax = new Vector2(0.65f, 0.25f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;

                var img = lockOverlay.GetComponent<Image>();
                if (img != null) img.preserveAspect = true;
            }
        }

        if (missedOverlay != null)
        {
            var rect = missedOverlay.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.35f, -0.05f);
                rect.anchorMax = new Vector2(0.65f, 0.25f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;

                var img = missedOverlay.GetComponent<Image>();
                if (img != null) img.preserveAspect = true;
            }
        }

        if (claimedOverlay != null)
        {
            var rect = claimedOverlay.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.35f, -0.05f);
                rect.anchorMax = new Vector2(0.65f, 0.25f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;

                var img = claimedOverlay.GetComponent<Image>();
                if (img != null) img.preserveAspect = true;
            }
        }

        if (todayOverlay != null)
        {
            var rect = todayOverlay.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
            }
        }

        if (quantityText != null)
        {
            var rect = quantityText.rectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.55f, 0.04f);
                rect.anchorMax = new Vector2(0.95f, 0.28f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                quantityText.enableAutoSizing = true;
                quantityText.fontSizeMin = 8f;
                quantityText.fontSizeMax = 28f;
                quantityText.alignment = TextAlignmentOptions.BottomRight;
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
