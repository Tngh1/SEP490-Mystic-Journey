using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using UnityEngine;

/// <summary>
/// Detects achievements completed during the current play session and routes them through
/// the shared PaperPopup queue. The first successful response is a baseline, so achievements
/// completed before this runtime started are never replayed on login.
/// </summary>
public class AchievementPopupRuntime : MonoBehaviour
{
    public static AchievementPopupRuntime Instance { get; private set; }

    [SerializeField, Min(5f)] private float pollIntervalSeconds = 15f;

    private readonly HashSet<int> completedAchievementIds = new HashSet<int>();
    private readonly HashSet<int> unlockRequestsInFlight = new HashSet<int>();
    private bool baselineInitialized;
    private bool requestInFlight;
    private bool refreshQueued;
    private int baselinePlayerProfileId;
    private Coroutine pollRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        WorldRuntimeEvents.QuestsChanged += RefreshAchievements;
        WorldRuntimeEvents.CurrencyChanged += RefreshAchievements;
        WorldRuntimeEvents.LevelChanged += RefreshAchievements;
        WorldRuntimeEvents.MapChanged += OnMapChanged;

        if (pollRoutine == null)
            pollRoutine = StartCoroutine(PollAchievements());
    }

    private void OnDisable()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshAchievements;
        WorldRuntimeEvents.CurrencyChanged -= RefreshAchievements;
        WorldRuntimeEvents.LevelChanged -= RefreshAchievements;
        WorldRuntimeEvents.MapChanged -= OnMapChanged;

        if (pollRoutine != null)
        {
            StopCoroutine(pollRoutine);
            pollRoutine = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Handles an explicit successful unlock response immediately. Recording the id before
    /// showing it prevents the next poll from enqueueing the same achievement again.
    /// </summary>
    public void NotifyAchievementUnlocked(PlayerAchievementResponse achievement)
    {
        if (achievement == null || achievement.AchievementId <= 0)
            return;

        if (!completedAchievementIds.Add(achievement.AchievementId))
            return;

        ShowAchievementPopup(achievement);
    }

    public void RefreshAchievements()
    {
        if (ApiClient.Instance == null || !ApiClient.Instance.HasToken())
            return;

        if (requestInFlight)
        {
            refreshQueued = true;
            return;
        }

        requestInFlight = true;
        AchievementApi.Instance.GetMyAchievements(
            response =>
            {
                if (this == null)
                    return;

                requestInFlight = false;
                ProcessResponse(response);

                if (refreshQueued)
                {
                    refreshQueued = false;
                    RefreshAchievements();
                }
            },
            error =>
            {
                if (this == null)
                    return;

                requestInFlight = false;
                refreshQueued = false;
                Debug.LogWarning($"[AchievementPopupRuntime] Refresh failed: {error.Message}");
            });
    }

    private IEnumerator PollAchievements()
    {
        yield return null;
        RefreshAchievements();

        while (true)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(5f, pollIntervalSeconds));
            RefreshAchievements();
        }
    }

    private void OnMapChanged(string mapName)
    {
        RefreshAchievements();
    }

    private void ProcessResponse(PlayerMeAchievementsResponse response)
    {
        if (response == null)
            return;

        if (baselineInitialized &&
            baselinePlayerProfileId > 0 &&
            response.PlayerProfileId > 0 &&
            baselinePlayerProfileId != response.PlayerProfileId)
        {
            completedAchievementIds.Clear();
            unlockRequestsInFlight.Clear();
            baselineInitialized = false;
        }

        if (response.PlayerProfileId > 0)
            baselinePlayerProfileId = response.PlayerProfileId;

        var achievements = response.Achievements;
        bool establishingBaseline = !baselineInitialized;
        if (establishingBaseline)
        {
            if (achievements != null)
            {
                foreach (var achievement in achievements)
                {
                    if (IsCompleted(achievement))
                        completedAchievementIds.Add(achievement.AchievementId);
                }
            }

            baselineInitialized = true;
        }

        if (achievements == null)
            return;

        foreach (var achievement in achievements)
        {
            if (IsCompleted(achievement))
            {
                if (!establishingBaseline && completedAchievementIds.Add(achievement.AchievementId))
                    ShowAchievementPopup(achievement);
                continue;
            }

            if (achievement.PlayerAchievementId > 0 &&
                achievement.RequiredValue > 0 &&
                achievement.Progress >= achievement.RequiredValue)
            {
                BeginUnlock(achievement.PlayerAchievementId);
            }
        }
    }

    private void BeginUnlock(int playerAchievementId)
    {
        if (!unlockRequestsInFlight.Add(playerAchievementId))
            return;

        AchievementApi.Instance.UnlockAchievement(
            playerAchievementId,
            achievement =>
            {
                if (this == null)
                    return;

                unlockRequestsInFlight.Remove(playerAchievementId);
                NotifyAchievementUnlocked(achievement);
                WorldRuntimeEvents.RaiseCurrencyChanged();
            },
            error =>
            {
                if (this == null)
                    return;

                unlockRequestsInFlight.Remove(playerAchievementId);
                Debug.LogWarning($"[AchievementPopupRuntime] Unlock failed for {playerAchievementId}: {error.Message}");
            });
    }

    private static bool IsCompleted(PlayerAchievementResponse achievement)
    {
        return achievement != null &&
               achievement.AchievementId > 0 &&
               achievement.IsCompleted;
    }

    private static void ShowAchievementPopup(PlayerAchievementResponse achievement)
    {
        var popup = MainQuestPanelRuntime.Instance;
        if (popup == null)
        {
            Debug.LogWarning("[AchievementPopupRuntime] PaperPopup runtime is not ready.");
            return;
        }

        string achievementName = string.IsNullOrWhiteSpace(achievement.AchievementName)
            ? $"Achievement #{achievement.AchievementId}"
            : achievement.AchievementName;

        popup.ShowPaperPopup(
            achievementName,
            UIPaperPopupView.PaperPopupKind.AchievementUnlocked);
    }
}
