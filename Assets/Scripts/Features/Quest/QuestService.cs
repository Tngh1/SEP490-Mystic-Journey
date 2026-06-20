using System;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Utilities;

public sealed class QuestService
{
    public static QuestService Instance { get; } = new QuestService();

    private QuestService() { }

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
        => PlayerQuestApi.Instance.AcceptQuest(questId, onSuccess, onError);

    public void CompleteQuest(int questId, Action<PlayerQuestResponse> onSuccess, Action<ApiException> onError)
        => PlayerQuestApi.Instance.CompleteQuest(questId, onSuccess, onError);

    public void ClaimQuestReward(int questId, Action<PlayerQuestResponse> onSuccess, Action<ApiException> onError)
        => PlayerQuestApi.Instance.ClaimReward(questId, onSuccess, onError);

    public void TalkToNpc(int npcId, Action<TalkToNpcResponse> onSuccess, Action<ApiException> onError)
        => WorldApi.Instance.TalkToNpc(npcId, onSuccess, onError);

    // ── Static Utility Methods (delegated to QuestUtils) ─────────────────────────
    public static List<PlayerQuestResponse> NormalizeMainQuests(IEnumerable<PlayerQuestResponse> source)
        => QuestUtils.NormalizeMainQuests(source);

    public static PlayerQuestResponse PickPreferredQuest(IEnumerable<PlayerQuestResponse> source)
        => QuestUtils.PickPreferredQuest(source);

    public static PlayerQuestResponse FindSameQuest(IEnumerable<PlayerQuestResponse> source, PlayerQuestResponse target)
        => QuestUtils.FindSameQuest(source, target);

    public static bool IsMainQuest(PlayerQuestResponse quest)
        => QuestUtils.IsMainQuest(quest);

    public static bool IsStatus(PlayerQuestResponse quest, string status)
        => QuestUtils.IsStatus(quest, status);

    public static string StatusLabel(PlayerQuestResponse quest)
        => QuestUtils.StatusLabel(quest);

    public static string ObjectiveLine(PlayerQuestResponse quest)
        => QuestUtils.ObjectiveLine(quest);

    public static string RewardLine(PlayerQuestResponse quest)
        => QuestUtils.RewardLine(quest);
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
