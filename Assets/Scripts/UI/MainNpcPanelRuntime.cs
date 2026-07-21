using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class MainNpcPanelRuntime : MonoBehaviour
{
    public static MainNpcPanelRuntime Instance { get; private set; }

    [Header("Scene UI")]
    [SerializeField] private GameObject npcPanel;
    [SerializeField] private Image portraitImage;

    private static readonly Dictionary<string, Sprite> RemoteSprites = new Dictionary<string, Sprite>();

    private TextSlot nameText;
    private TextSlot roleText;
    private TextSlot dialogueText;
    private TextSlot questHintText;
    private readonly List<Button> actionButtons = new List<Button>();
    private readonly List<TextSlot> actionButtonLabels = new List<TextSlot>();
    private RectTransform actionAreaRect;
    private readonly List<NPCDialogueResponse> currentDialogues = new List<NPCDialogueResponse>();
    private readonly List<PlayerQuestResponse> currentLinkedQuests = new List<PlayerQuestResponse>();
    private readonly HashSet<int> processingQuestIds = new HashSet<int>();

    private NPCDialogueResponse currentStoryDialogue;
    private Button closeButton;
    private int firstQuestId;
    private int currentNpcId;
    private Coroutine imageRoutine;
    private bool didBind;
    private int storyDialogueIndex = 0;
    
    private Coroutine typewriterRoutine;
    private bool isTyping;
    private string fullDialogueText;
    private static readonly string[] NextPhrases = { "Tell me more...", "I'm listening...", "Go on...", "What happened next?", "I see..." };

    public bool IsOpen => npcPanel != null && npcPanel.activeInHierarchy;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private IEnumerator Start()
    {
        yield return null;
        BindUi();
    }

    private void OnDestroy()
    {
        if (imageRoutine != null) StopCoroutine(imageRoutine);
        if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);

        if (Instance == this)
            Instance = null;
    }

    public void OpenForNpc(WorldInteractable interactable)
    {
        if (interactable == null)
            return;

        BindUi();
        if (npcPanel == null)
            return;

        RenderLocal(interactable);
        ShowPanel();

        if (!ApiClient.Instance.HasToken() || interactable.NpcId <= 0)
            return;

        var manager = GetQuestManager();
        if (manager == null)
        {
            Debug.LogWarning("[MainNpcPanelRuntime] QuestManager was not found in Main scene.");
            return;
        }

        manager.TalkToNpc(
            interactable.NpcId,
            response => RenderApiResponse(response, interactable),
            error =>
            {
                StartTypewriter(dialogueText, string.IsNullOrWhiteSpace(interactable.GreetingText) ? error : interactable.GreetingText);
                Debug.LogWarning($"[MainNpcPanelRuntime] TalkToNpc failed: {error}");
            }
        );
    }

    private void BindUi()
    {
        npcPanel = npcPanel != null ? npcPanel : FindSceneObject("NPCPanel");
        if (npcPanel == null)
        {
            if (!didBind)
                Debug.LogWarning("[MainNpcPanelRuntime] NPCPanel was not found in Main scene.");
            didBind = true;
            return;
        }

        portraitImage = portraitImage != null ? portraitImage : FindPortraitImage();
        nameText = nameText.IsValid ? nameText : FindTextSlot(npcPanel.transform, "NameNPC", "NpcNameText", "NPCNameText", "NameText");
        roleText = roleText.IsValid ? roleText : FindTextSlot(npcPanel.transform, "NpcRoleText", "RoleText", "DescriptionText", skip: nameText);

        var dialogueArea = FindDescendant(npcPanel.transform, "DialogueTextArea")?.transform ?? npcPanel.transform;
        dialogueText = dialogueText.IsValid ? dialogueText : FindTextSlot(dialogueArea, "NPCText", "DialogueText", "DialogText", "ContentText");
        if (!dialogueText.IsValid)
            dialogueText = FindTextSlot(npcPanel.transform, "NPCText", "DialogueText", "DialogText", "ContentText", skip: nameText, skip2: roleText);

        var actionArea = FindDescendant(npcPanel.transform, "ActionArea")?.transform ?? npcPanel.transform;
        actionAreaRect = actionArea as RectTransform ?? actionArea.GetComponent<RectTransform>();
        questHintText = questHintText.IsValid ? questHintText : FindTextSlot(actionArea, "QuestHintText", "HintText", "QuestText", skip: nameText, skip2: roleText, skip3: dialogueText);

        BindFixedActionButtons(actionArea);

        var closeObject = FindDescendant(npcPanel.transform, "CloseNpcButton") ?? FindDescendant(npcPanel.transform, "CloseButton");
        closeButton = BindButton(closeObject, ClosePanel);

        SetActionsVisible(false);
        npcPanel.SetActive(false);
        didBind = true;
    }

    private void RenderLocal(WorldInteractable interactable)
    {
        firstQuestId = interactable.QuestId ?? 0;
        currentNpcId = interactable.NpcId;
        currentStoryDialogue = null;
        currentDialogues.Clear();
        currentLinkedQuests.Clear();

        SetText(nameText, CleanName(Safe(interactable.DisplayName, "Elder Rowan")));
        SetText(roleText, Safe(interactable.Description, "Tutorial elder and main quest giver."));
        StartTypewriter(dialogueText, Safe(interactable.GreetingText, "Welcome to ElfLand. Talk to me when you are ready for your first quest."));
        SetText(questHintText, firstQuestId > 0 ? "Quest available" : string.Empty);
        ConfigureDefaultActions();
        ApplyPortrait(null, interactable);
    }

    private static string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Elder Rowan";
        string clean = name.Trim();
        int parenIdx = clean.IndexOf('(');
        if (parenIdx > 0)
        {
            clean = clean.Substring(0, parenIdx).Trim();
        }
        return clean;
    }

    private void RenderApiResponse(TalkToNpcResponse response, WorldInteractable fallback)
    {
        var npc = response?.Npc;
        var linkedQuests = response?.LinkedQuests?
            .Where(q => q != null && !QuestManager.IsStatus(q, "Claimed"))
            .OrderBy(q => QuestManager.IsStatus(q, "InProgress") ? 0 : QuestManager.IsStatus(q, "Completed") ? 1 : 2)
            .ThenBy(q => q.RequiredLevel)
            .ThenBy(q => q.QuestId)
            .ToList() ?? new List<PlayerQuestResponse>();

        var dialogues = response?.Npc?.Dialogues?
            .Where(d => d != null && d.IsActive)
            .OrderBy(d => d.DisplayOrder)
            .ToList() ?? new List<NPCDialogueResponse>();

        currentDialogues.Clear();
        currentDialogues.AddRange(dialogues);
        currentLinkedQuests.Clear();
        currentLinkedQuests.AddRange(linkedQuests);
        storyDialogueIndex = 0;
        currentStoryDialogue = PickQuestDialogue(dialogues, linkedQuests, storyDialogueIndex) ?? dialogues.FirstOrDefault();
        firstQuestId = currentStoryDialogue?.LinkedQuestId ?? linkedQuests.FirstOrDefault()?.QuestId ?? 0;
        currentNpcId = npc?.NPCId ?? fallback.NpcId;

        SetText(nameText, CleanName(Safe(npc?.Name, fallback.DisplayName)));
        SetText(roleText, Safe(npc?.Description, fallback.Description));
        StartTypewriter(dialogueText, BuildIntroDialogue(currentStoryDialogue, dialogues, fallback));
        SetText(questHintText, BuildQuestHint(linkedQuests));
        ConfigureNpcActions();
        ApplyPortrait(npc, fallback);
    }

    private static string BuildIntroDialogue(NPCDialogueResponse currentStoryDialogue, List<NPCDialogueResponse> dialogues, WorldInteractable fallback)
    {
        var intro = currentStoryDialogue ?? dialogues?.FirstOrDefault(d => !d.LinkedQuestId.HasValue) ?? dialogues?.FirstOrDefault();
        return Safe(intro?.Content, Safe(fallback.GreetingText, "Welcome to ElfLand. Talk to me when you are ready for your first quest."));
    }

    private static NPCDialogueResponse PickQuestDialogue(List<NPCDialogueResponse> dialogues, List<PlayerQuestResponse> linkedQuests, int index = 0)
    {
        if (dialogues == null || dialogues.Count == 0)
            return null;

        if (linkedQuests != null && linkedQuests.Count > 0)
        {
            var activeQuestId = linkedQuests[0].QuestId;
            var linked = dialogues.Where(d => d.LinkedQuestId == activeQuestId).ToList();
            if (linked.Count > 0)
            {
                if (index >= 0 && index < linked.Count)
                    return linked[index];
                return linked.LastOrDefault();
            }
        }

        return dialogues.FirstOrDefault(d => d.LinkedQuestId.HasValue);
    }

    private static string BuildQuestHint(List<PlayerQuestResponse> linkedQuests)
    {
        if (linkedQuests == null || linkedQuests.Count == 0)
            return "No linked quest available.";

        var quest = linkedQuests[0];
        var status = string.IsNullOrWhiteSpace(quest.Status) ? "Available" : quest.Status;
        return $"{quest.QuestTitle} [{status}]";
    }

    private void BindFixedActionButtons(Transform actionArea)
    {
        actionButtons.Clear();
        actionButtonLabels.Clear();

        if (actionArea == null)
            return;

        var buttons = actionArea.GetComponentsInChildren<Button>(true)
            .Where(button => button != null && button.transform != actionArea)
            .OrderBy(button => button.transform.GetSiblingIndex())
            .Take(4)
            .ToList();

        for (var i = 0; i < buttons.Count; i++)
        {
            actionButtons.Add(buttons[i]);
            actionButtonLabels.Add(FindTextSlot(buttons[i].transform, "Text (TMP)", "Text", "Label", "TitleText"));
        }
    }

    private void ConfigureDefaultActions()
    {
        SetActionButton(0, "Greetings.", true, OnStoryDialogueAction);
        SetActionButton(1, "I need some guidance.", false, OnQuestionAction);
        SetActionButton(2, "Do you have any advice for me?", firstQuestId > 0, OnGiftHintAction);
        SetActionButton(3, "Farewell.", true, ClosePanel);
    }

    private void ConfigureNpcActions()
    {
        var hasQuestion = HasDialogueType("Question") || HasDialogueType("Help");
        var hasGiftOrHint = currentLinkedQuests.Count > 0 || HasDialogueType("Gift") || HasDialogueType("Hint");

        var activeQuestId = currentLinkedQuests.Count > 0 ? currentLinkedQuests[0].QuestId : (currentStoryDialogue?.LinkedQuestId ?? 0);
        var questDialogues = currentDialogues.Where(d => d.LinkedQuestId == activeQuestId).ToList();
        var isMultiLine = questDialogues.Count > 1 && storyDialogueIndex < questDialogues.Count - 1;
        var storyLabel = isMultiLine ? NextPhrases[storyDialogueIndex % NextPhrases.Length] : BuildStoryActionLabel(currentStoryDialogue, FindLinkedQuest(currentStoryDialogue?.LinkedQuestId));

        SetActionButton(0, storyLabel, true, OnStoryDialogueAction);
        SetActionButton(1, "I need some guidance.", hasQuestion, OnQuestionAction);
        SetActionButton(2, BuildGiftHintActionLabel(), hasGiftOrHint, OnGiftHintAction);
        SetActionButton(3, "Farewell.", true, ClosePanel);
    }

    private void SetActionButton(int index, string label, bool visible, UnityEngine.Events.UnityAction action)
    {
        if (index < 0 || index >= actionButtons.Count)
            return;

        var button = actionButtons[index];
        if (button == null)
            return;

        button.gameObject.SetActive(visible);
        button.interactable = visible;
        if (index < actionButtonLabels.Count)
            SetText(actionButtonLabels[index], label);

        if (button.onClick == null)
            button.onClick = new Button.ButtonClickedEvent();
        button.onClick.RemoveAllListeners();
        if (action != null)
            button.onClick.AddListener(action);

        RebuildActionLayout();
    }

    private void SetActionsVisible(bool visible)
    {
        for (var i = 0; i < actionButtons.Count; i++)
        {
            if (actionButtons[i] != null)
            {
                actionButtons[i].gameObject.SetActive(visible);
                actionButtons[i].interactable = visible;
            }
        }
    }

    private void RebuildActionLayout()
    {
        if (actionAreaRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(actionAreaRect);

            // Fix layout gap/overlap if VerticalLayoutGroup is missing in Prefab
            if (actionAreaRect.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() == null && actionButtons.Count > 0)
            {
                float spacing = 10f; // Khoảng cách giữa các nút
                var firstBtnRect = actionButtons[0] != null ? actionButtons[0].transform as RectTransform : null;
                
                if (firstBtnRect != null)
                {
                    float currentY = firstBtnRect.anchoredPosition.y;
                    for (int i = 0; i < actionButtons.Count; i++)
                    {
                        var btn = actionButtons[i];
                        if (btn != null && btn.gameObject.activeSelf)
                        {
                            var rect = btn.transform as RectTransform;
                            if (rect != null)
                            {
                                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, currentY);
                                currentY -= (rect.rect.height + spacing);
                            }
                        }
                    }
                }
            }
        }
    }

    private void OnStoryDialogueAction()
    {
        if (isTyping)
        {
            if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);
            dialogueText.Set(fullDialogueText);
            isTyping = false;
            return;
        }

        var activeQuestId = currentLinkedQuests.Count > 0 ? currentLinkedQuests[0].QuestId : (currentStoryDialogue?.LinkedQuestId ?? 0);
        var questDialogues = currentDialogues.Where(d => d.LinkedQuestId == activeQuestId).ToList();

        if (questDialogues.Count > 0 && storyDialogueIndex < questDialogues.Count - 1)
        {
            storyDialogueIndex++;
            currentStoryDialogue = questDialogues[storyDialogueIndex];
            StartTypewriter(dialogueText, Safe(currentStoryDialogue.Content, "Listen closely. Your path begins here."));
            ConfigureNpcActions();
            return;
        }

        var dialogue = currentStoryDialogue ?? currentDialogues.FirstOrDefault();
        if (dialogue == null)
        {
            StartTypewriter(dialogueText, "I have no tale for you right now.");
            return;
        }

        firstQuestId = dialogue.LinkedQuestId ?? firstQuestId;
        StartTypewriter(dialogueText, Safe(dialogue.Content, "Listen closely. Your path begins here."));

        var linkedQuest = FindLinkedQuest(dialogue.LinkedQuestId);
        if (linkedQuest != null)
            SetText(questHintText, BuildQuestHint(new List<PlayerQuestResponse> { linkedQuest }));

        HandleLinkedQuestFromStory(dialogue, linkedQuest);
    }

    private void HandleLinkedQuestFromStory(NPCDialogueResponse dialogue, PlayerQuestResponse linkedQuest)
    {
        var questId = dialogue?.LinkedQuestId ?? linkedQuest?.QuestId ?? 0;
        if (questId <= 0 || processingQuestIds.Contains(questId))
            return;

        var manager = GetQuestManager();
        if (manager == null)
        {
            SetText(questHintText, "Quest system is not ready.");
            return;
        }

        var quest = ResolveQuest(questId, linkedQuest, manager);
        if (QuestManager.IsStatus(quest, "Claimed"))
        {
            SetText(questHintText, "Reward already claimed.");
            return;
        }

        if (QuestManager.IsStatus(quest, "Completed"))
        {
            RouteToQuestReward(questId, "Quest completed. Claim your reward.");
            return;
        }

        if (QuestManager.IsStatus(quest, "InProgress"))
        {
            if (ShouldAutoCompleteNpcTalkQuest(quest))
            {
                CompleteTalkQuestAndRouteToReward(manager, questId, quest);
                return;
            }

            SetText(questHintText, BuildQuestHint(new List<PlayerQuestResponse> { quest }));
            OpenFirstQuest();
            return;
        }

        AcceptLinkedQuest(manager, questId, quest, dialogue);
    }

    private void AcceptLinkedQuest(QuestManager manager, int questId, PlayerQuestResponse quest, NPCDialogueResponse dialogue)
    {
        processingQuestIds.Add(questId);
        manager.AcceptQuest(
            questId,
            () =>
            {
                var acceptedQuest = ResolveQuest(questId, quest, manager);
                if (acceptedQuest != null)
                {
                    acceptedQuest.Status = string.IsNullOrWhiteSpace(acceptedQuest.Status) ? "InProgress" : acceptedQuest.Status;
                    acceptedQuest.Progress = Mathf.Max(acceptedQuest.Progress, 0);
                    SetText(questHintText, BuildQuestHint(new List<PlayerQuestResponse> { acceptedQuest }));
                }

                if (ShouldAutoCompleteNpcTalkQuest(acceptedQuest))
                {
                    CompleteTalkQuestAndRouteToReward(manager, questId, acceptedQuest);
                    return;
                }

                processingQuestIds.Remove(questId);
                NotifyQuestAccepted(acceptedQuest ?? quest, dialogue);
                ClosePanel();
                WorldRuntimeEvents.RaiseQuestsChanged();
            },
            error =>
            {
                processingQuestIds.Remove(questId);
                SetText(questHintText, Safe(error, "Could not accept quest."));
            }
        );
    }

    private void CompleteTalkQuestAndRouteToReward(QuestManager manager, int questId, PlayerQuestResponse quest)
    {
        if (manager == null || questId <= 0)
        {
            processingQuestIds.Remove(questId);
            WorldRuntimeEvents.RaiseQuestsChanged();
            return;
        }

        processingQuestIds.Add(questId);
        manager.CompleteQuest(
            questId,
            () =>
            {
                var completedQuest = ResolveQuest(questId, quest, manager);
                if (completedQuest != null)
                {
                    completedQuest.Status = "Completed";
                    completedQuest.Progress = Mathf.Max(1, completedQuest.TargetAmount);
                }

                var qp = MainQuestPanelRuntime.Instance;
                if (qp != null) qp.ShowQuestPopup("Quest completed!");

                // Auto-claim after completing talk quest
                manager.ClaimReward(
                    questId,
                    onSuccess: () =>
                    {
                        processingQuestIds.Remove(questId);
                        if (qp != null) qp.ShowQuestPopup("Reward claimed! Your next quest is ready.");
                        WorldRuntimeEvents.RaiseQuestsChanged();
                        ClosePanel();
                    },
                    onError: err =>
                    {
                        processingQuestIds.Remove(questId);
                        Debug.LogWarning($"[MainNpcPanelRuntime] Auto claim failed: {err}");
                        RouteToQuestReward(questId, "Quest completed. Claim your reward.");
                    });
            },
            error =>
            {
                processingQuestIds.Remove(questId);
                Debug.LogWarning($"[MainNpcPanelRuntime] Auto complete talk quest failed: {error}");
                WorldRuntimeEvents.RaiseQuestsChanged();
            }
        );
    }

    private void RouteToQuestReward(int questId, string message)
    {
        ClosePanel();

        var questPanelRuntime = MainQuestPanelRuntime.Instance ?? FindQuestPanelRuntime();
        if (questPanelRuntime != null)
        {
            questPanelRuntime.OpenQuestPanelForReward(questId);
            if (!string.IsNullOrWhiteSpace(message))
                questPanelRuntime.ShowQuestPopup(message);
        }
        else if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenQuestPanel();
        }

        WorldRuntimeEvents.RaiseQuestsChanged();
    }

    private static PlayerQuestResponse ResolveQuest(int questId, PlayerQuestResponse fallback, QuestManager manager)
    {
        if (questId <= 0)
            return fallback;

        return manager?.GetQuestResponse(questId) ?? fallback;
    }

    private static bool ShouldAutoCompleteNpcTalkQuest(PlayerQuestResponse quest)
    {
        return quest != null && string.Equals(quest.ObjectiveType, "Talk", StringComparison.OrdinalIgnoreCase);
    }


    private void TurnInQuestItemAndRoute(PlayerQuestResponse quest)
    {
        if (quest == null || quest.QuestId <= 0)
            return;

        if (currentNpcId <= 0)
        {
            SetText(dialogueText, "I cannot receive quest items right now.");
            return;
        }

        if (processingQuestIds.Contains(quest.QuestId))
            return;

        var manager = GetQuestManager();
        if (manager == null)
        {
            SetText(questHintText, "Quest system is not ready.");
            return;
        }

        processingQuestIds.Add(quest.QuestId);
        SetText(dialogueText, "Let me take a look at what you brought.");

        manager.TurnInQuestItem(
            currentNpcId,
            quest.QuestId,
            response =>
            {
                processingQuestIds.Remove(quest.QuestId);

                if (response?.Quest != null)
                {
                    ReplaceLinkedQuest(response.Quest);
                    SetText(questHintText, BuildQuestHint(new List<PlayerQuestResponse> { response.Quest }));
                }

                if (response == null)
                {
                    SetText(dialogueText, "I could not receive those items.");
                    return;
                }

                SetText(dialogueText, Safe(response.Message, "Quest item handed over."));
                WorldRuntimeEvents.RaiseQuestsChanged();

                // Khi quest Collect hoàn thành → tự động Claim luôn, không cần player bấm tay.
                // Quest tiếp theo sẽ unlock ngay lập tức.
                if (response.Success && QuestManager.IsStatus(response.Quest, "Completed"))
                {
                    var completedQuestId = quest.QuestId;
                    var qp = MainQuestPanelRuntime.Instance;
                    if (qp != null) qp.ShowQuestPopup("Quest completed! Claiming your reward...");
                    manager.ClaimReward(
                        completedQuestId,
                        onSuccess: () =>
                        {
                            Debug.Log($"[MainNpcPanelRuntime] Auto-claimed questId={completedQuestId}");
                            if (qp != null) qp.ShowQuestPopup("Reward claimed! Your next quest is ready.");
                            WorldRuntimeEvents.RaiseQuestsChanged();
                            ClosePanel();
                        },
                        onError: err =>
                        {
                            // Fallback: route sang Reward panel để player claim tay nếu auto-claim thất bại
                            Debug.LogWarning($"[MainNpcPanelRuntime] Auto-claim failed ({err}), routing to panel.");
                            RouteToQuestReward(completedQuestId, "Quest completed. Claim your reward.");
                        });
                }

            },
            error =>
            {
                processingQuestIds.Remove(quest.QuestId);
                SetText(dialogueText, Safe(error, "Could not hand over quest items."));
            }
        );
    }

    private void ReplaceLinkedQuest(PlayerQuestResponse quest)
    {
        if (quest == null)
            return;

        for (var i = 0; i < currentLinkedQuests.Count; i++)
        {
            if (currentLinkedQuests[i] != null && currentLinkedQuests[i].QuestId == quest.QuestId)
            {
                currentLinkedQuests[i] = quest;
                return;
            }
        }

        currentLinkedQuests.Add(quest);
    }

    private string BuildGiftHintActionLabel()
    {
        var quest = QuestManager.PickPreferredQuest(currentLinkedQuests);
        if (quest == null)
            return "Do you have any advice for me?";

        if (QuestManager.IsStatus(quest, "Completed"))
            return "I have finished this quest.";

        if (QuestManager.IsStatus(quest, "InProgress"))
        {
            if (IsCollectQuest(quest))
                return HasEnoughQuestProgress(quest) ? "I have the items you need." : "Any hints for this task?";

            if (ShouldAutoCompleteNpcTalkQuest(quest))
                return "Could we discuss my journey?";

            return "Any hints for this task?";
        }

        return "Do you have any advice for me?";
    }

    private static bool IsCollectQuest(PlayerQuestResponse quest)
    {
        return quest != null && string.Equals(quest.ObjectiveType, "Collect", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasEnoughQuestProgress(PlayerQuestResponse quest)
    {
        if (quest == null)
            return false;

        var target = Mathf.Max(1, quest.TargetAmount);
        return quest.Progress >= target;
    }

    private static string BuildMissingQuestItemHint(PlayerQuestResponse quest, NPCDialogueResponse dialogue)
    {
        var target = Mathf.Max(1, quest?.TargetAmount ?? 1);
        var progress = Mathf.Clamp(quest?.Progress ?? 0, 0, target);
        var missing = Mathf.Max(0, target - progress);
        if (missing <= 0)
            return "You have gathered everything! Hand them over, and I will reward you.";

        var targetName = Safe(quest?.ObjectiveTarget, "quest item");
        var location = Safe(quest?.ObjectiveLocation, Safe(quest?.RegionName, quest?.MapName));
        var baseHint = !string.IsNullOrWhiteSpace(dialogue?.Content)
            ? dialogue.Content
            : $"You need to collect {targetName} at {location}.";
        return $"{baseHint}\nI still need {missing} more {targetName} from you.";
    }
    private void NotifyQuestAccepted(PlayerQuestResponse quest, NPCDialogueResponse dialogue)
    {
        var title = Safe(quest?.QuestTitle, Safe(dialogue?.LinkedQuestTitle, "New quest"));
        var questPanelRuntime = MainQuestPanelRuntime.Instance ?? FindQuestPanelRuntime();
        if (questPanelRuntime != null)
            questPanelRuntime.ShowQuestPopup($"Quest Accepted!\n{title} has been added to your quest log.");
        else
            Debug.Log($"[QuestPopup] Quest Accepted! {title} has been added to your quest log.");
    }
    private void OnQuestionAction()
    {
        var dialogue = FindDialogueByType("Question", "Help");
        SetText(dialogueText, Safe(dialogue?.Content, "Feel free to ask. Press E to talk to others, P to gather items, and always keep an eye on your Quest Tracker to know what to do next."));
    }

    private void OnGiftHintAction()
    {
        var dialogue = FindDialogueByType("Gift", "Hint");
        var quest = QuestManager.PickPreferredQuest(currentLinkedQuests);
        if (quest == null)
        {
            SetText(dialogueText, Safe(dialogue?.Content, "I have no gift for you right now. Come back when your path has moved forward."));
            SetText(questHintText, string.Empty);
            return;
        }

        firstQuestId = quest.QuestId;
        SetText(questHintText, BuildQuestHint(new List<PlayerQuestResponse> { quest }));

        if (QuestManager.IsStatus(quest, "Completed"))
        {
            RouteToQuestReward(quest.QuestId, "Quest completed. Claim your reward.");
            return;
        }

        if (QuestManager.IsStatus(quest, "InProgress"))
        {
            if (ShouldAutoCompleteNpcTalkQuest(quest))
            {
                var manager = GetQuestManager();
                CompleteTalkQuestAndRouteToReward(manager, quest.QuestId, quest);
                return;
            }

            if (IsCollectQuest(quest))
            {
                if (HasEnoughQuestProgress(quest))
                {
                    TurnInQuestItemAndRoute(quest);
                    return;
                }

                SetText(dialogueText, BuildMissingQuestItemHint(quest, dialogue));
                OpenFirstQuest();
                return;
            }

            var hint = !string.IsNullOrWhiteSpace(dialogue?.Content) ? dialogue.Content : $"Hint: {BuildObjectiveHint(quest)}";
            SetText(dialogueText, hint);
            OpenFirstQuest();
            return;
        }

        SetText(dialogueText, "There is something I can entrust to you. Listen to my story first, then follow your Quest Tracker.");
    }
    private void OpenFirstQuest()
    {
        if (firstQuestId <= 0)
            return;

        var questPanelRuntime = MainQuestPanelRuntime.Instance ?? FindQuestPanelRuntime();
        if (questPanelRuntime != null)
            questPanelRuntime.OpenQuestPanelForQuest(firstQuestId);
        else if (UIManager.Instance != null)
            UIManager.Instance.OpenQuestPanel();
    }

    private void ShowPanel()
    {
        WorldInteractionPromptRuntime.Hide();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowPanel(npcPanel);
        else
            npcPanel.SetActive(true);
    }

    private void ClosePanel()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ClosePanel(npcPanel);
        else if (npcPanel != null)
            npcPanel.SetActive(false);
    }



    private bool HasDialogueType(string responseType)
    {
        return FindDialogueByType(responseType) != null;
    }

    private NPCDialogueResponse FindDialogueByType(params string[] responseTypes)
    {
        if (responseTypes == null || responseTypes.Length == 0)
            return null;

        for (var i = 0; i < responseTypes.Length; i++)
        {
            var responseType = responseTypes[i];
            if (string.IsNullOrWhiteSpace(responseType))
                continue;

            var dialogue = currentDialogues.FirstOrDefault(d => d != null && string.Equals(d.ResponseType, responseType, StringComparison.OrdinalIgnoreCase));
            if (dialogue != null)
                return dialogue;
        }

        return null;
    }
    private PlayerQuestResponse FindLinkedQuest(int? questId)
    {
        if (!questId.HasValue)
            return null;

        return currentLinkedQuests.FirstOrDefault(q => q != null && q.QuestId == questId.Value);
    }

    private static string BuildStoryActionLabel(NPCDialogueResponse dialogue, PlayerQuestResponse linkedQuest)
    {
        if (linkedQuest != null)
            return BuildPlayerQuestActionLabel(linkedQuest);

        if (dialogue != null && string.Equals(dialogue.ResponseType, "Dialogue", StringComparison.OrdinalIgnoreCase))
            return "Tell me about this place.";

        return "Tell me about the story.";
    }

    private static string BuildPlayerQuestActionLabel(PlayerQuestResponse quest)
    {
        if (QuestManager.IsStatus(quest, "Claimed"))
            return "Thank you for your guidance.";

        if (QuestManager.IsStatus(quest, "Completed"))
            return "I completed the task.";

        if (QuestManager.IsStatus(quest, "InProgress"))
        {
            if (ShouldAutoCompleteNpcTalkQuest(quest))
                return "Let's talk about my task.";

            if (IsCollectQuest(quest))
                return HasEnoughQuestProgress(quest) ? "I brought the items." : "I am still gathering them.";

            if (IsObjectiveType(quest, "EquipSkill"))
                return "I am ready to learn a skill.";

            if (IsObjectiveType(quest, "Defeat"))
                return "I will defeat the monsters.";

            return "I am working on it.";
        }

        if (IsCollectQuest(quest))
            return "I will gather the items.";

        if (ShouldAutoCompleteNpcTalkQuest(quest))
            return "I am ready to listen.";

        if (IsObjectiveType(quest, "EquipSkill"))
            return "Teach me a new skill.";

        if (IsObjectiveType(quest, "Defeat"))
            return "I will handle the threat.";

        return "I will help.";
    }

    private static bool IsObjectiveType(PlayerQuestResponse quest, string objectiveType)
    {
        return quest != null && string.Equals(quest.ObjectiveType, objectiveType, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildObjectiveHint(PlayerQuestResponse quest)
    {
        if (quest == null)
            return "Check your Quest Tracker for the next step.";

        var target = Mathf.Max(1, quest.TargetAmount);
        var progress = Mathf.Clamp(quest.Progress, 0, target);
        var objective = Safe(quest.ObjectiveType, "Explore");
        var targetName = Safe(quest.ObjectiveTarget, "the marked target");
        var location = Safe(quest.ObjectiveLocation, Safe(quest.RegionName, quest.MapName));
        return $"{objective}: {targetName} at {location} ({progress}/{target}).";
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
            return trimmed;

        return trimmed.Substring(0, Mathf.Max(0, maxLength - 3)).TrimEnd() + "...";
    }
    private void ApplyPortrait(NPCResponse npc, WorldInteractable fallback)
    {
        if (portraitImage == null)
            return;

        if (fallback != null && fallback.PortraitSprite != null)
        {
            portraitImage.sprite = fallback.PortraitSprite;
            portraitImage.enabled = true;
            portraitImage.gameObject.SetActive(true);
            return;
        }

        if (npc == null)
        {
            portraitImage.gameObject.SetActive(false);
            return;
        }

        var local = GetLibrarySprite($"npc:{npc.NPCId}", npc.Name);
        if (local != null)
        {
            portraitImage.sprite = local;
            portraitImage.enabled = true;
            portraitImage.gameObject.SetActive(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(npc.IconUrl))
            return;

        if (imageRoutine != null)
            StopCoroutine(imageRoutine);
        imageRoutine = StartCoroutine(LoadSprite(npc.IconUrl, portraitImage));
    }

    private IEnumerator LoadSprite(string rawUrl, Image target)
    {
        var url = ResolveUrl(rawUrl);
        if (string.IsNullOrWhiteSpace(url) || target == null)
            yield break;

        if (RemoteSprites.TryGetValue(url, out var cached) && cached != null)
        {
            target.sprite = cached;
            target.enabled = true;
            target.gameObject.SetActive(true);
            yield break;
        }

        using var request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[MainNpcPanelRuntime] Load NPC icon failed: {request.error}");
            yield break;
        }

        var texture = DownloadHandlerTexture.GetContent(request);
        if (texture == null)
            yield break;

        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        RemoteSprites[url] = sprite;

        if (target != null)
        {
            target.sprite = sprite;
            target.enabled = true;
            target.gameObject.SetActive(true);
        }
    }

    private Sprite GetLibrarySprite(params string[] ids)
    {
        if (ids == null)
            return null;

        if (ItemIconDatabase.Instance != null)
        {
            for (var i = 0; i < ids.Length; i++)
            {
                if (ItemIconDatabase.Instance.TryGetIcon(ids[i], out var dbSprite))
                    return dbSprite;
            }
        }

        return null;
    }

    private Image FindPortraitImage()
    {
        var portrait = FindDescendant(npcPanel.transform, "PortraitSlot");
        if (portrait == null)
            return null;

        return portrait.GetComponent<Image>() ?? portrait.GetComponentInChildren<Image>(true);
    }

    private static QuestManager GetQuestManager()
    {
        if (QuestManager.Instance != null)
            return QuestManager.Instance;

        var managers = Resources.FindObjectsOfTypeAll<QuestManager>();
        for (var i = 0; i < managers.Length; i++)
        {
            var manager = managers[i];
            if (manager != null && manager.gameObject.scene.IsValid() && !string.IsNullOrEmpty(manager.gameObject.scene.name))
                return manager;
        }

        return null;
    }
    private static MainQuestPanelRuntime FindQuestPanelRuntime()
    {
        var runtimes = Resources.FindObjectsOfTypeAll<MainQuestPanelRuntime>();
        for (var i = 0; i < runtimes.Length; i++)
        {
            var runtime = runtimes[i];
            if (runtime != null && runtime.gameObject.scene.IsValid() && !string.IsNullOrEmpty(runtime.gameObject.scene.name))
                return runtime;
        }

        return null;
    }

    private static Button BindButton(GameObject target, UnityEngine.Events.UnityAction action)
    {
        if (target == null)
            return null;

        var button = target.GetComponent<Button>();
        if (button == null)
            button = target.AddComponent<Button>();
        if (button == null)
            return null;

        if (button.onClick == null)
            button.onClick = new Button.ButtonClickedEvent();

        button.onClick.RemoveAllListeners();
        if (action != null)
            button.onClick.AddListener(action);
        return button;
    }

    private static string ResolveUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        var trimmed = rawUrl.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (trimmed.StartsWith("/"))
            return ApiConfig.BaseUrl.TrimEnd('/') + trimmed;

        return ApiConfig.BaseUrl.TrimEnd('/') + "/" + trimmed;
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


    private static Button FindActionButton(Transform root, params string[] names)
    {
        if (root == null || names == null)
            return null;

        for (var i = 0; i < names.Length; i++)
        {
            var child = FindDescendant(root, names[i]);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button != null)
                return button;
        }

        return null;
    }
    private static GameObject FindDescendant(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        var all = root.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == objectName)
                return all[i].gameObject;
        }

        return null;
    }

    private static TextSlot FindTextSlot(Transform root, string name1, string name2 = null, string name3 = null, string name4 = null, TextSlot skip = default, TextSlot skip2 = default, TextSlot skip3 = default)
    {
        if (root == null)
            return default;

        var names = new[] { name1, name2, name3, name4 }.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
        for (var i = 0; i < names.Length; i++)
        {
            var child = FindDescendant(root, names[i]);
            var slot = TextSlot.From(child);
            if (slot.IsValid && !slot.Equals(skip) && !slot.Equals(skip2) && !slot.Equals(skip3))
                return slot;
        }

        return default;
    }

    private static void SetText(TextSlot slot, string value)
    {
        slot.Set(value);
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private void StartTypewriter(TextSlot slot, string text, float speed = 0.03f)
    {
        if (typewriterRoutine != null)
            StopCoroutine(typewriterRoutine);
        
        fullDialogueText = text ?? string.Empty;
        if (!slot.IsValid) return;
        
        typewriterRoutine = StartCoroutine(TypewriterCoroutine(slot, fullDialogueText, speed));
    }

    private IEnumerator TypewriterCoroutine(TextSlot slot, string text, float speed)
    {
        isTyping = true;
        slot.Set("");
        for (int i = 0; i < text.Length; i++)
        {
            slot.Set(text.Substring(0, i + 1));
            yield return new WaitForSeconds(speed);
        }
        isTyping = false;
    }

    private readonly struct TextSlot : IEquatable<TextSlot>
    {
        private readonly TMP_Text tmp;
        private readonly Text text;

        public TextSlot(TMP_Text tmp, Text text)
        {
            this.tmp = tmp;
            this.text = text;
        }

        public bool IsValid => tmp != null || text != null;

        public static TextSlot From(GameObject target)
        {
            if (target == null)
                return default;

            return new TextSlot(target.GetComponent<TMP_Text>(), target.GetComponent<Text>());
        }

        public void Set(string value)
        {
            if (tmp != null)
            {
                tmp.text = value ?? string.Empty;
                return;
            }

            if (text != null)
                text.text = value ?? string.Empty;
        }

        public bool Equals(TextSlot other)
        {
            return tmp == other.tmp && text == other.text;
        }

        public override bool Equals(object obj)
        {
            return obj is TextSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((tmp != null ? tmp.GetHashCode() : 0) * 397) ^ (text != null ? text.GetHashCode() : 0);
            }
        }
    }
}