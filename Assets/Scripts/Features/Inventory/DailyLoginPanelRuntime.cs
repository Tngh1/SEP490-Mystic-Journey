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
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button claimButton;
    [SerializeField] private float rewardsCacheSeconds = 60f;

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
            DailyLoginApi.Instance.GetAll(
                1,
                60,
                response =>
                {
                    rewards.Clear();
                    if (response?.Items != null)
                        rewards.AddRange(response.Items.Where(item => item != null && item.IsActive));
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

        var claimedDays = Mathf.Max(0, status?.TotalDaysClaimed ?? 0);
        var availableDay = status != null && !status.IsClaimedToday ? claimedDays + 1 : -1;
        var list = new List<UIItemDisplayData>();

        foreach (var reward in rewards.OrderBy(r => r.DayNumber))
        {
            var isClaimed = reward.DayNumber <= claimedDays;
            var isAvailable = reward.DayNumber == availableDay;
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
                dayNumber = reward.DayNumber,
                rawData = reward
            });
        }

        uiDailyLogin.RefreshDaily(list);
        UpdateStatusText(claimedDays, availableDay);
    }

    private void ClaimAvailableReward()
    {
        if (requestInFlight || status != null && status.IsClaimedToday)
            return;

        requestInFlight = true;
        SetLoading(true);
        SetError(null);

        DailyLoginApi.Instance.Claim(
            response =>
            {
                requestInFlight = false;
                SetLoading(false);

                if (status == null)
                    status = new PlayerDailyLoginResponse();

                status.CurrentStreak = response?.CurrentStreak ?? status.CurrentStreak;
                status.TotalDaysClaimed = response?.TotalDaysClaimed ?? status.TotalDaysClaimed + 1;
                status.IsClaimedToday = true;
                Render();
            },
            error =>
            {
                requestInFlight = false;
                SetLoading(false);
                SetError($"Daily claim failed: {error.Message}");
            });
    }

    private void HandleDailySlotClicked(UIBaseItemSlot slot)
    {
        if (slot?.RawData is not UIItemDisplayData data)
            return;

        if (!data.isAvailable || data.isClaimed)
            return;

        ClaimAvailableReward();
    }

    private void BindUi()
    {
        if (uiDailyLogin == null)
            uiDailyLogin = GetComponentInChildren<UIDailyLogin>(true) ?? UIDailyLogin.Instance;
        if (statusText == null)
            statusText = FindText("StatusText", "DailyStatusText", "MessageText");
        if (errorText == null)
            errorText = FindText("ErrorText", "ErrorMessageText");
        if (loadingIndicator == null)
            loadingIndicator = FindObject("LoadingIndicator", "Loading", "Spinner");
        if (refreshButton == null)
            refreshButton = FindButton("RefreshButton");
        if (claimButton == null)
            claimButton = FindButton("ClaimButton", "ClaimDailyButton");
    }

    private void BindEvents()
    {
        if (eventsBound)
            return;

        if (uiDailyLogin != null)
            uiDailyLogin.OnDailyItemClaimed += HandleDailySlotClicked;
        if (refreshButton != null)
            refreshButton.onClick.AddListener(() => LoadDaily(true));
        if (claimButton != null)
            claimButton.onClick.AddListener(ClaimAvailableReward);

        eventsBound = uiDailyLogin != null || refreshButton != null || claimButton != null;
    }

    private Sprite ResolveRewardIcon(DailyLoginRewardResponse reward)
    {
        if (reward?.RewardItemId != null && ItemIconDatabase.Instance != null && ItemIconDatabase.Instance.TryGetIcon(reward.RewardItemId.Value, out var icon))
            return icon;

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