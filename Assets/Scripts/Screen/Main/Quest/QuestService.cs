using System;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

public sealed class QuestService
{
    public static QuestService Instance { get; } = new QuestService();

    private QuestService()
    {
    }

    public void LoadMainQuests(Action<QuestLoadResult> onSuccess, Action<ApiException> onError)
    {
        if (!ApiClient.Instance.HasToken())
        {
            onSuccess?.Invoke(new QuestLoadResult(new List<PlayerQuestResponse>(), null, null));
            return;
        }

        WorldApi.Instance.GetState(
            state =>
            {
                var quests = NormalizeMainQuests(state?.Quests);
                if (quests.Count > 0)
                {
                    var activeQuest = FindSameQuest(quests, state?.ActiveQuest) ?? PickPreferredQuest(quests);
                    onSuccess?.Invoke(new QuestLoadResult(quests, activeQuest, state));
                    return;
                }

                PlayerQuestApi.Instance.GetMyQuests(
                    list =>
                    {
                        quests = NormalizeMainQuests(list);
                        onSuccess?.Invoke(new QuestLoadResult(quests, PickPreferredQuest(quests), state));
                    },
                    onError ?? (_ => { })
                );
            },
            worldError =>
            {
                PlayerQuestApi.Instance.GetMyQuests(
                    list =>
                    {
                        var quests = NormalizeMainQuests(list);
                        onSuccess?.Invoke(new QuestLoadResult(quests, PickPreferredQuest(quests), null));
                    },
                    onError ?? (_ => { })
                );
            }
        );
    }

    public void AcceptQuest(int questId, Action<PlayerQuestResponse> onSuccess, Action<ApiException> onError)
    {
        PlayerQuestApi.Instance.AcceptQuest(questId, onSuccess, onError);
    }

    public void CompleteQuest(int questId, Action<PlayerQuestResponse> onSuccess, Action<ApiException> onError)
    {
        PlayerQuestApi.Instance.CompleteQuest(questId, onSuccess, onError);
    }

    public void ClaimQuestReward(int questId, Action<PlayerQuestResponse> onSuccess, Action<ApiException> onError)
    {
        PlayerQuestApi.Instance.ClaimReward(questId, onSuccess, onError);
    }

    public void TalkToNpc(int npcId, Action<TalkToNpcResponse> onSuccess, Action<ApiException> onError)
    {
        WorldApi.Instance.TalkToNpc(npcId, onSuccess, onError);
    }

    public static List<PlayerQuestResponse> NormalizeMainQuests(IEnumerable<PlayerQuestResponse> source)
    {
        return (source ?? Enumerable.Empty<PlayerQuestResponse>())
            .Where(IsMainQuest)
            .OrderBy(QuestStatusPriority)
            .ThenBy(q => q.RequiredLevel)
            .ThenBy(q => q.QuestId)
            .ToList();
    }

    public static PlayerQuestResponse PickPreferredQuest(IEnumerable<PlayerQuestResponse> source)
    {
        var quests = source?.ToList() ?? new List<PlayerQuestResponse>();
        return quests.FirstOrDefault(q => IsStatus(q, "InProgress"))
               ?? quests.FirstOrDefault(q => IsStatus(q, "Completed"))
               ?? quests.FirstOrDefault(q => IsStatus(q, "NotStarted"))
               ?? quests.FirstOrDefault();
    }

    public static PlayerQuestResponse FindSameQuest(IEnumerable<PlayerQuestResponse> source, PlayerQuestResponse target)
    {
        if (target == null)
            return null;

        return source?.FirstOrDefault(q => q != null && q.QuestId == target.QuestId);
    }

    public static bool IsMainQuest(PlayerQuestResponse quest)
    {
        if (quest == null)
            return false;

        if (string.IsNullOrWhiteSpace(quest.QuestType))
            return true;

        var normalized = quest.QuestType.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        return string.Equals(normalized, "Main", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "MainQuest", StringComparison.OrdinalIgnoreCase) ||
               normalized.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsStatus(PlayerQuestResponse quest, string status)
    {
        return quest != null && string.Equals(quest.Status, status, StringComparison.OrdinalIgnoreCase);
    }

    private static int QuestStatusPriority(PlayerQuestResponse quest)
    {
        if (IsStatus(quest, "InProgress"))
            return 0;
        if (IsStatus(quest, "Completed"))
            return 1;
        if (IsStatus(quest, "NotStarted"))
            return 2;
        if (IsStatus(quest, "Claimed"))
            return 3;
        return 4;
    }
}

public sealed class QuestLoadResult
{
    public QuestLoadResult(List<PlayerQuestResponse> quests, PlayerQuestResponse activeQuest, WorldStateResponse worldState)
    {
        Quests = quests ?? new List<PlayerQuestResponse>();
        ActiveQuest = activeQuest;
        WorldState = worldState;
    }

    public List<PlayerQuestResponse> Quests { get; }
    public PlayerQuestResponse ActiveQuest { get; }
    public WorldStateResponse WorldState { get; }
}
