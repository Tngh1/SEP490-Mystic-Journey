using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using UnityEngine;

// Executes mono behaviour operation.
public class AchievementPopupRuntime : MonoBehaviour
{
    // Executes instance operation.
    public static AchievementPopupRuntime Instance { get; private set; }

    [SerializeField, Min(5f)] private float pollIntervalSeconds = 15f;

    private readonly HashSet<int> completedAchievementIds = new HashSet<int>();
    private readonly HashSet<int> unlockRequestsInFlight = new HashSet<int>();
    private bool baselineInitialized;
    private bool requestInFlight;
    private bool refreshQueued;
    private int baselinePlayerProfileId;
    private Coroutine pollRoutine;

    // Initializes component singleton cache on GameObject creation.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this); // Prevent duplicate achievement popups
            return;
        }

        Instance = this;
    }

    // Subscribes gameplay runtime progression events and starts background achievement polling.
    private void OnEnable()
    {
        WorldRuntimeEvents.QuestsChanged += RefreshAchievements; // Check achievements on quest completion
        WorldRuntimeEvents.CurrencyChanged += RefreshAchievements; // Check achievements on wallet mutation
        WorldRuntimeEvents.LevelChanged += RefreshAchievements; // Check achievements on level up
        WorldRuntimeEvents.MapChanged += OnMapChanged;

        if (pollRoutine == null)
            pollRoutine = StartCoroutine(PollAchievements()); // Start 15s periodic achievement status polling
    }

    // Unsubscribes event listeners and stops polling coroutines.
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

    // Cleans up singleton instance on GameObject destruction.
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Displays celebratory popup banner when a new achievement milestone unlocks.
    public void NotifyAchievementUnlocked(PlayerAchievementResponse achievement)
    {
        if (achievement == null || achievement.AchievementId <= 0)
            return;

        if (!completedAchievementIds.Add(achievement.AchievementId))
            return; // Avoid duplicate popups for already unlocked achievements

        ShowAchievementPopup(achievement); // Trigger banner slide-in animation and chime
    }

    // Queries backend for completed milestones and auto-claims eligible rewards.
    public void RefreshAchievements()
    {
        if (ApiClient.Instance == null || !ApiClient.Instance.HasToken())
            return; // Skip if offline

        if (requestInFlight)
        {
            refreshQueued = true;
            return; // Queue request if already in flight
        }

        requestInFlight = true;
        AchievementApi.Instance.GetMyAchievements(
            response =>
            {
                if (this == null)
                    return;

                requestInFlight = false;
                ProcessResponse(response); // Evaluate milestone completions and trigger unlock popups

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

    // Periodic coroutine that queries achievements every 15 seconds.
    private IEnumerator PollAchievements()
    {
        yield return null;
        RefreshAchievements();

        while (true)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(5f, pollIntervalSeconds)); // Wait 15s real-time
            RefreshAchievements();
        }
    }

    // Triggers achievement check upon changing map scene.
    private void OnMapChanged(string mapName)
    {
        RefreshAchievements(); // Check exploration achievements
    }

    // Executes process response operation.
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

    // Executes begin unlock operation.
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

    // Executes is completed operation.
    // Validates input parameters against null or empty values.
    private static bool IsCompleted(PlayerAchievementResponse achievement)
    {
        return achievement != null &&
               achievement.AchievementId > 0 &&
               achievement.IsCompleted;
    }

    // Executes show achievement popup operation.
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
