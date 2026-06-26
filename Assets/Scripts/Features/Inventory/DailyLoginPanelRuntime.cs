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

    [Header("Retro Claim Confirmation")]
    [SerializeField] private GameObject confirmRetroPanel;
    [SerializeField] private TMP_Text confirmRetroText;
    [SerializeField] private Button confirmRetroYesBtn;
    [SerializeField] private Button confirmRetroNoBtn;

    private readonly List<DailyLoginRewardResponse> rewards = new List<DailyLoginRewardResponse>();
    private PlayerDailyLoginResponse status;
    private bool rewardsLoaded;
    private bool requestInFlight;
    private bool eventsBound;
    private float rewardsLoadedAt = -999f;
    private int pendingRetroClaimDay = -1;

    private void Awake()
    {
        BindUi();
        BindEvents();
        CloseRetroClaimPopup();
    }

    private void OnEnable()
    {
        LoadDaily(false);
    }

    public void LoadDaily(bool force)
    {
        BindUi();
        BindEvents();

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

    private void Render()
    {
        if (uiDailyLogin == null)
            return;

        var claimedDaysList = status?.ClaimedDays ?? new List<int>();
        var currentDay = DateTime.UtcNow.Day; // Đồng bộ múi giờ UTC với backend
        var list = new List<UIItemDisplayData>();

        if (monthsText != null)
        {
            var m = status?.CurrentMonth ?? DateTime.UtcNow.Month;
            var y = status?.CurrentYear ?? DateTime.UtcNow.Year;
            monthsText.text = $"Month {m}/{y}";
        }

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
        if (requestInFlight || status != null && status.ClaimedDays != null && status.ClaimedDays.Contains(DateTime.Now.Day))
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
                status.ClaimedDays.Add(DateTime.Now.Day);
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

        if (confirmRetroYesBtn != null)
            confirmRetroYesBtn.onClick.AddListener(ExecuteRetroClaim);

        if (confirmRetroNoBtn != null)
            confirmRetroNoBtn.onClick.AddListener(CloseRetroClaimPopup);

        if (claimButton != null)
            claimButton.onClick.AddListener(ClaimAvailableReward);

        eventsBound = uiDailyLogin != null || refreshButton != null || claimButton != null || confirmRetroYesBtn != null;
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

        pendingRetroClaimDay = dayNumber;

        if (confirmRetroPanel != null)
        {
            if (confirmRetroText != null)
            {
                int remainingClaims = 5 - (status?.RetroClaimCount ?? 0);
                confirmRetroText.text = $"Do you want to spend 20 Gems to retro-claim Day {dayNumber}?\n(Remaining this month: {remainingClaims})";
            }
            
            // Đảm bảo Popup nằm trên cùng của DailyLoginPanel để không bị che mất
            if (confirmRetroPanel.transform.parent != this.transform)
            {
                confirmRetroPanel.transform.SetParent(this.transform, false);
            }
            confirmRetroPanel.transform.SetAsLastSibling();
            confirmRetroPanel.SetActive(true);
        }
        else
        {
            // Fallback nếu panel bị null
            ExecuteRetroClaim();
        }
    }

    private void CloseRetroClaimPopup()
    {
        pendingRetroClaimDay = -1;
        if (confirmRetroPanel != null)
            confirmRetroPanel.SetActive(false);
    }

    private void ExecuteRetroClaim()
    {
        if (pendingRetroClaimDay <= 0) return;
        int dayToClaim = pendingRetroClaimDay;
        CloseRetroClaimPopup();
        RetroClaimReward(dayToClaim);
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

        if (confirmRetroPanel == null)
            confirmRetroPanel = FindSceneObject("ConfirmRetroClaimPanel");
        if (confirmRetroText == null && confirmRetroPanel != null)
        {
            var tmp = confirmRetroPanel.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) confirmRetroText = tmp;
        }
        if (confirmRetroYesBtn == null && confirmRetroPanel != null)
        {
            var btnYes = FindDescendant(confirmRetroPanel.transform, "ConfirmButton", "YesButton", "OkButton");
            if (btnYes != null) confirmRetroYesBtn = btnYes.GetComponent<Button>();
        }
        if (confirmRetroNoBtn == null && confirmRetroPanel != null)
        {
            var btnNo = FindDescendant(confirmRetroPanel.transform, "CancelButton", "NoButton", "CloseButton");
            if (btnNo != null) confirmRetroNoBtn = btnNo.GetComponent<Button>();
        }
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

        // Ưu tiên icon từ ItemIconDatabase (khi có RewardItemId)
        if (reward.RewardItemId != null && ItemIconDatabase.Instance != null &&
            ItemIconDatabase.Instance.TryGetIcon(reward.RewardItemId.Value, out var icon))
            return icon;

        // Fallback: dùng icon mặc định theo loại phần thưởng
        var type = reward.RewardType ?? string.Empty;
        if (string.Equals(type, "Gold", System.StringComparison.OrdinalIgnoreCase) && goldIcon != null)
            return goldIcon;
        if ((string.Equals(type, "EXP", System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(type, "Experience", System.StringComparison.OrdinalIgnoreCase)) && expIcon != null)
            return expIcon;
        if ((string.Equals(type, "Gem", System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(type, "Gems", System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(type, "Diamond", System.StringComparison.OrdinalIgnoreCase)) && gemIcon != null)
            return gemIcon;

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