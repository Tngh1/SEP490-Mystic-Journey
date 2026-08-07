using System;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyLoginPanelRuntime : MonoBehaviour
{
    [SerializeField] private UIDailyLogin uiDailyLogin;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private TMP_Text monthsText;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button claimButton;
    [SerializeField] private float rewardsCacheSeconds = 60f;

    [Header("Default Reward Icons")]
    [Tooltip("Icon hiển thị cho phần thưởng Gold khi không có ItemId")]
    [SerializeField] private Sprite goldIcon;
    [Tooltip("Icon hiển thị cho phần thưởng EXP")]
    [SerializeField] private Sprite expIcon;
    [Tooltip("Icon hiển thị cho phần thưởng Gems/Diamond")]
    [SerializeField] private Sprite gemIcon;
    [Tooltip("Icon hiển thị cho phần thưởng Energy")]
    [SerializeField] private Sprite energyIcon;

    private readonly List<DailyLoginRewardResponse> rewards = new List<DailyLoginRewardResponse>();
    private PlayerDailyLoginResponse status;
    private bool rewardsLoaded;
    private bool requestInFlight;
    private bool eventsBound;
    private float rewardsLoadedAt = -999f;

    private void Awake()
    {
        BindUi();
        BindEvents();
        UpdateMonthText();
    }

    private void OnEnable()
    {
        UpdateMonthText();
        LoadDaily(false);
    }

    public void LoadDaily(bool force)
    {
        BindUi();
        BindEvents();
        UpdateMonthText();

        if (requestInFlight)
            return;

        requestInFlight = true;
        SetLoading(true);
        SetError(null);

        var needsRewards = force || !rewardsLoaded || Time.unscaledTime - rewardsLoadedAt > rewardsCacheSeconds;
        var pending = needsRewards ? 2 : 1;

        void Done()
        {
            pending--;
            if (pending > 0)
                return;

            requestInFlight = false;
            SetLoading(false);
            Render();
        }

        if (needsRewards)
        {
            // Gọi endpoint current-month: tự động trả về đúng số ngày tháng hiện tại
            DailyLoginApi.Instance.GetCurrentMonth(
                response =>
                {
                    rewards.Clear();
                    if (response != null)
                        rewards.AddRange(response.Where(item => item != null && item.IsActive));
                    rewardsLoaded = true;
                    rewardsLoadedAt = Time.unscaledTime;
                    Done();
                },
                error =>
                {
                    SetError($"Daily rewards load failed: {error.Message}");
                    Done();
                });
        }

        WorldApi.Instance.GetState(
            state =>
            {
                status = state?.DailyLogin;
                Done();
            },
            error =>
            {
                SetError($"Daily status load failed: {error.Message}");
                Done();
            });
    }

    private void UpdateMonthText()
    {
        if (monthsText == null)
            return;

        int m = (status != null && status.CurrentMonth > 0) ? status.CurrentMonth : DateTime.UtcNow.Month;
        int y = (status != null && status.CurrentYear > 0) ? status.CurrentYear : DateTime.UtcNow.Year;
        monthsText.text = $"Month {m}/{y}";
    }

    private void Render()
    {
        if (uiDailyLogin == null)
            return;

        var claimedDaysList = status?.ClaimedDays ?? new List<int>();
        var currentDay = DateTime.UtcNow.Day; // Đồng bộ múi giờ UTC với backend
        var list = new List<UIItemDisplayData>();

        UpdateMonthText();

        foreach (var reward in rewards.OrderBy(r => r.DayNumber))
        {
            var isClaimed = claimedDaysList.Contains(reward.DayNumber);
            var isAvailable = !isClaimed && reward.DayNumber == currentDay;
            var isMissed = !isClaimed && reward.DayNumber < currentDay;

            var itemId = reward.RewardItemId ?? reward.DailyLoginRewardId;
            list.Add(new UIItemDisplayData
            {
                itemId = itemId,
                itemName = BuildRewardName(reward),
                icon = ResolveRewardIcon(reward),
                quantity = BuildRewardQuantity(reward),
                rarity = string.Empty,
                isClaimed = isClaimed,
                isAvailable = isAvailable,
                isMissed = isMissed,
                dayNumber = reward.DayNumber,
                rawData = reward
            });
        }

        uiDailyLogin.RefreshDaily(list);
        UpdateStatusText(status?.TotalDaysClaimed ?? 0, currentDay);
    }

    private void ClaimAvailableReward()
    {
        // UtcNow, không phải Now: backend chốt ngày theo UTC (DailyLoginRewardService).
        // Ở UTC+7 thì 07:00-24:00 giờ VN đã sang ngày mới theo local nhưng vẫn là ngày cũ
        // theo UTC, nên dùng Now sẽ so sai ngày và guard chống nhận trùng bị hỏng.
        if (requestInFlight || status != null && status.ClaimedDays != null && status.ClaimedDays.Contains(DateTime.UtcNow.Day))
            return;

        requestInFlight = true;
        SetLoading(true);
        SetError(null);

        DailyLoginApi.Instance.Claim(
            response =>
            {
                requestInFlight = false;
                SetLoading(false);

                if (response != null && !response.Success)
                {
                    SetError($"Daily claim failed: {response.Message}");
                    return;
                }

                if (status == null)
                {
                    status = new PlayerDailyLoginResponse();
                    status.ClaimedDays = new List<int>();
                }

                if (status.ClaimedDays == null) status.ClaimedDays = new List<int>();
                status.ClaimedDays.Add(DateTime.UtcNow.Day);
                status.TotalDaysClaimed = response?.TotalDaysClaimed ?? status.TotalDaysClaimed + 1;
                Render();
            },
            error =>
            {
                requestInFlight = false;
                SetLoading(false);
                SetError($"Daily claim failed: {error.Message}");
            });
    }

    private void RetroClaimReward(int dayNumber)
    {
        if (requestInFlight) return;

        requestInFlight = true;
        SetLoading(true);
        SetError(null);

        DailyLoginApi.Instance.RetroClaim(
            dayNumber,
            response =>
            {
                requestInFlight = false;
                SetLoading(false);

                if (response != null && !response.Success)
                {
                    Debug.LogWarning($"[DailyLogin] Retro claim failed: {response.Message}");
                    SetError($"Retro claim failed: {response.Message}");
                    return;
                }

                if (status == null)
                {
                    status = new PlayerDailyLoginResponse();
                    status.ClaimedDays = new List<int>();
                }

                if (status.ClaimedDays == null) status.ClaimedDays = new List<int>();
                status.ClaimedDays.Add(dayNumber);
                status.TotalDaysClaimed = response?.TotalDaysClaimed ?? status.TotalDaysClaimed + 1;
                status.RetroClaimCount += 1; // Tăng số lần bù trên UI
                Render();
            },
            error =>
            {
                requestInFlight = false;
                SetLoading(false);
                Debug.LogWarning($"[DailyLogin] Retro claim failed. Not enough gems? API Error: {error.Message}");
                SetError($"Retro claim failed (Not enough Gems?): {error.Message}");
            });
    }

    private void BindEvents()
    {
        if (eventsBound)
            return;

        if (refreshButton != null)
            refreshButton.onClick.AddListener(() => LoadDaily(true));

        if (uiDailyLogin != null)
            uiDailyLogin.OnDailyItemClaimed += HandleDailySlotClicked;

        if (claimButton != null)
            claimButton.onClick.AddListener(ClaimAvailableReward);

        eventsBound = uiDailyLogin != null || refreshButton != null || claimButton != null;
    }

    private void HandleDailySlotClicked(UIBaseItemSlot slot)
    {
        if (slot?.RawData is not UIItemDisplayData data)
            return;

        if (data.isClaimed)
            return;

        if (data.isAvailable)
        {
            ClaimAvailableReward();
        }
        else if (data.isMissed)
        {
            ShowRetroClaimPopup(data.dayNumber);
        }
    }

    private void ShowRetroClaimPopup(int dayNumber)
    {
        // Kiểm tra xem đã vượt quá giới hạn 5 lần 1 tháng chưa
        if (status != null && status.RetroClaimCount >= 5)
        {
            SetError("You have reached the limit of 5 retro-claims this month.");
            return;
        }

        // Tìm ngày gần nhất bị lỡ
        int maxMissedDay = -1;
        var claimedSet = status?.ClaimedDays?.ToHashSet() ?? new HashSet<int>();
        var currentDay = DateTime.UtcNow.Day;
        
        for (int d = currentDay - 1; d >= 1; d--)
        {
            if (!claimedSet.Contains(d))
            {
                maxMissedDay = d;
                break;
            }
        }

        // Nếu ngày bấm vào KHÔNG PHẢI là ngày lỡ gần nhất thì chặn lại
        if (dayNumber != maxMissedDay)
        {
            SetError("You must retro-claim the most recent missed day first.");
            return;
        }

        int remainingClaims = 5 - (status?.RetroClaimCount ?? 0);
        UIPopupBox.Show(
            caller: transform,
            titleText: "Retro Claim",
            message: $"Do you want to spend 20 Gems to retro-claim Day {dayNumber}?\n(Remaining this month: {remainingClaims})",
            onConfirm: () => RetroClaimReward(dayNumber),
            onCancel: null,
            confirmText: "Claim (20 Gems)",
            cancelText: "Cancel"
        );
    }

    private void BindUi()
    {
        if (uiDailyLogin == null)
            uiDailyLogin = GetComponentInChildren<UIDailyLogin>(true) ?? UIDailyLogin.Instance;
        if (statusText == null)
            statusText = FindText("StatusText", "DailyStatusText", "MessageText");
        if (errorText == null)
            errorText = FindText("ErrorText", "ErrorMessageText");
        if (monthsText == null)
            monthsText = FindText("MonthsText", "MonthText", "Month");
        if (loadingIndicator == null)
            loadingIndicator = FindObject("LoadingIndicator", "Loading", "Spinner");
        if (refreshButton == null)
            refreshButton = FindButton("RefreshButton");
        if (claimButton == null)
            claimButton = FindButton("ClaimButton", "ClaimDailyButton");


    }

    private static GameObject FindSceneObject(string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (var i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj != null && obj.name == objectName && obj.scene.IsValid() && !string.IsNullOrEmpty(obj.scene.name))
                return obj;
        }
        return null;
    }

    private static GameObject FindDescendant(Transform root, params string[] names)
    {
        if (root == null || names == null)
            return null;

        var all = root.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < all.Length; i++)
        {
            for (var j = 0; j < names.Length; j++)
            {
                if (all[i] != null && all[i].name == names[j])
                    return all[i].gameObject;
            }
        }
        return null;
    }


    private Sprite ResolveRewardIcon(DailyLoginRewardResponse reward)
    {
        if (reward == null)
            return null;

        // 1. If reward is an Item (has RewardItemName) -> Lookup in ItemIconDatabase
        if (!string.IsNullOrWhiteSpace(reward.RewardItemName) && ItemIconDatabase.Instance != null)
        {
            var icon = ItemIconDatabase.Instance.GetIcon(reward.RewardItemName, null);
            if (icon != null) return icon;
        }

        var type = reward.RewardType ?? string.Empty;

        // 2. Lookup in ItemIconDatabase by exact RewardType or synonyms (pass null for itemType to prevent unwanted "Currency" fallback to Gold)
        if (!string.IsNullOrWhiteSpace(type) && ItemIconDatabase.Instance != null)
        {
            // Direct lookup by key (case-insensitive in ItemIconDatabase)
            var typeIcon = ItemIconDatabase.Instance.GetIcon(type, null);
            if (typeIcon != null) return typeIcon;

            // Synonym key lookups
            if (string.Equals(type, "Gems", System.StringComparison.OrdinalIgnoreCase) || string.Equals(type, "Gem", System.StringComparison.OrdinalIgnoreCase))
            {
                typeIcon = ItemIconDatabase.Instance.GetIcon("Gem", null) ?? ItemIconDatabase.Instance.GetIcon("Gems", null) ?? ItemIconDatabase.Instance.GetIcon("Diamond", null);
                if (typeIcon != null) return typeIcon;
            }
            else if (string.Equals(type, "EXP", System.StringComparison.OrdinalIgnoreCase) || string.Equals(type, "Experience", System.StringComparison.OrdinalIgnoreCase))
            {
                typeIcon = ItemIconDatabase.Instance.GetIcon("Exp", null) ?? ItemIconDatabase.Instance.GetIcon("EXP", null) ?? ItemIconDatabase.Instance.GetIcon("Experience", null);
                if (typeIcon != null) return typeIcon;
            }
            else if (string.Equals(type, "Energy", System.StringComparison.OrdinalIgnoreCase))
            {
                typeIcon = ItemIconDatabase.Instance.GetIcon("Energy", null) ?? ItemIconDatabase.Instance.GetIcon("Stamina", null);
                if (typeIcon != null) return typeIcon;
            }
        }

        // 3. Fallback: Check serialized inspector fields if assigned
        if (string.Equals(type, "Gold", System.StringComparison.OrdinalIgnoreCase) && goldIcon != null)
            return goldIcon;
        if ((string.Equals(type, "Energy", System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(type, "Stamina", System.StringComparison.OrdinalIgnoreCase)) && energyIcon != null)
            return energyIcon;
        if ((string.Equals(type, "EXP", System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(type, "Experience", System.StringComparison.OrdinalIgnoreCase)) && expIcon != null)
            return expIcon;
        if ((string.Equals(type, "Gem", System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(type, "Gems", System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(type, "Diamond", System.StringComparison.OrdinalIgnoreCase)) && gemIcon != null)
            return gemIcon;

        // 4. Resources fallback lookup: "Icons/Gold", "Icons/Energy", "Icons/Exp", "Icons/Gem"
        if (!string.IsNullOrWhiteSpace(type))
        {
            var loaded = Resources.Load<Sprite>($"Icons/{type}");
            if (loaded != null) return loaded;
            if (string.Equals(type, "Gems", System.StringComparison.OrdinalIgnoreCase))
            {
                loaded = Resources.Load<Sprite>("Icons/Gem");
                if (loaded != null) return loaded;
            }
            if (string.Equals(type, "EXP", System.StringComparison.OrdinalIgnoreCase) || string.Equals(type, "Experience", System.StringComparison.OrdinalIgnoreCase))
            {
                loaded = Resources.Load<Sprite>("Icons/Exp");
                if (loaded != null) return loaded;
            }
        }

        return null;
    }

    private static string BuildRewardName(DailyLoginRewardResponse reward)
    {
        if (reward == null)
            return string.Empty;

        if (string.Equals(reward.RewardType, "Item", System.StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(reward.RewardItemName) ? $"Item #{reward.RewardItemId}" : reward.RewardItemName;

        return string.IsNullOrWhiteSpace(reward.RewardType) ? "Reward" : reward.RewardType;
    }

    private static int BuildRewardQuantity(DailyLoginRewardResponse reward)
    {
        if (reward == null)
            return 0;

        if (string.Equals(reward.RewardType, "Item", System.StringComparison.OrdinalIgnoreCase))
            return Mathf.Max(1, reward.RewardItemQuantity);

        return Mathf.Max(1, Mathf.RoundToInt((float)reward.RewardValue));
    }

    private void UpdateStatusText(int claimedDays, int availableDay)
    {
        if (statusText != null)
            statusText.text = availableDay > 0 ? $"Day {availableDay} reward is ready." : $"Claimed {claimedDays} daily rewards.";

        if (claimButton != null)
            claimButton.interactable = availableDay > 0 && !requestInFlight;
    }

    private void SetLoading(bool value)
    {
        if (loadingIndicator != null)
            loadingIndicator.SetActive(value);
    }

    private void SetError(string value)
    {
        if (errorText == null)
            return;

        errorText.text = value ?? string.Empty;
        errorText.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
    }

    private Button FindButton(params string[] names)
    {
        var obj = FindObject(names);
        return obj == null ? null : obj.GetComponent<Button>();
    }

    private TMP_Text FindText(params string[] names)
    {
        var obj = FindObject(names);
        return obj == null ? null : obj.GetComponent<TMP_Text>();
    }

    private GameObject FindObject(params string[] names)
    {
        var children = GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            for (var j = 0; j < names.Length; j++)
            {
                if (children[i] != null && children[i].name == names[j])
                    return children[i].gameObject;
            }
        }

        return null;
    }
}