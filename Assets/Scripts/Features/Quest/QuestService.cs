using System;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Utilities;

// Initializes a new default instance of the QuestService class.
public sealed class QuestService
{
    // Executes core business logic for instance.
    public static QuestService Instance { get; } = new QuestService();

    // Initializes a new default instance of the QuestService class.
    private QuestService() { }

    // Executes core business logic for load main quests.
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

    // Executes core business logic for accept quest.
    public void AcceptQuest(int questId, Action<PlayerQuestResponse> onSuccess, Action<ApiException> onError)
        => PlayerQuestApi.Instance.AcceptQuest(questId, onSuccess, onError);

    // Executes core business logic for complete quest.
    public void CompleteQuest(int questId, Action<PlayerQuestResponse> onSuccess, Action<ApiException> onError)
        => PlayerQuestApi.Instance.CompleteQuest(questId, onSuccess, onError);

    // Executes core business logic for claim quest reward.
    public void ClaimQuestReward(int questId, Action<PlayerQuestResponse> onSuccess, Action<ApiException> onError)
        => PlayerQuestApi.Instance.ClaimReward(questId, onSuccess, onError);

    // Executes core business logic for talk to npc.
    public void TalkToNpc(int npcId, Action<TalkToNpcResponse> onSuccess, Action<ApiException> onError)
        => WorldApi.Instance.TalkToNpc(npcId, onSuccess, onError);

    // Executes core business logic for normalize main quests.
    public static List<PlayerQuestResponse> NormalizeMainQuests(IEnumerable<PlayerQuestResponse> source)
        => QuestUtils.NormalizeMainQuests(source);

    // Executes core business logic for pick preferred quest.
    public static PlayerQuestResponse PickPreferredQuest(IEnumerable<PlayerQuestResponse> source)
        => QuestUtils.PickPreferredQuest(source);

    // Executes core business logic for find same quest.
    public static PlayerQuestResponse FindSameQuest(IEnumerable<PlayerQuestResponse> source, PlayerQuestResponse target)
        => QuestUtils.FindSameQuest(source, target);

    // Executes core business logic for is main quest.
    // Returns a boolean indicating operation success.
    public static bool IsMainQuest(PlayerQuestResponse quest)
        => QuestUtils.IsMainQuest(quest);

    // Executes core business logic for is status.
    // Returns a boolean indicating operation success.
    public static bool IsStatus(PlayerQuestResponse quest, string status)
        => QuestUtils.IsStatus(quest, status);

    // Executes core business logic for status label.
    public static string StatusLabel(PlayerQuestResponse quest)
        => QuestUtils.StatusLabel(quest);

    // Executes core business logic for objective line.
    public static string ObjectiveLine(PlayerQuestResponse quest)
        => QuestUtils.ObjectiveLine(quest);

    // Executes core business logic for reward line.
    public static string RewardLine(PlayerQuestResponse quest)
        => QuestUtils.RewardLine(quest);
}

// Executes core business logic for quest load result.
public sealed class QuestLoadResult
{
    // Executes core business logic for quest load result.
    public QuestLoadResult(List<PlayerQuestResponse> quests, PlayerQuestResponse activeQuest, WorldStateResponse worldState)
    {
        Quests = quests ?? new List<PlayerQuestResponse>();
        ActiveQuest = activeQuest;
        WorldState = worldState;
    }

    // Executes core business logic for quests.
    public List<PlayerQuestResponse> Quests { get; }
    // Executes core business logic for active quest.
    public PlayerQuestResponse ActiveQuest { get; }
    // Executes core business logic for world state.
    public WorldStateResponse WorldState { get; }
}
