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

// Executes mono behaviour operation.
public class MainNpcPanel : MonoBehaviour
{
    // Executes instance operation.
    public static MainNpcPanel Instance { get; private set; }

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

    // Executes is open operation.
    public bool IsOpen => npcPanel != null && npcPanel.activeInHierarchy;

    // Initializes internal component caches and dependencies for MainNpcPanel upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    // Performs startup initialization for MainNpcPanel on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private IEnumerator Start()
    {
        yield return null;
        BindUi();
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (imageRoutine != null) StopCoroutine(imageRoutine);
        if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);

        if (Instance == this)
            Instance = null;
    }

    // Executes open for npc operation.
    public void OpenForNpc(WorldInteractable interactable)
    {
        if (interactable == null)
            return;

        BindUi();
        if (npcPanel == null)
            return;

        if (!ApiClient.Instance.HasToken() || interactable.NpcId <= 0)
        {
            ShowPanel();
            RenderLocal(interactable);
            return;
        }

        var manager = GetQuestManager();
        if (manager == null)
        {
            Debug.LogWarning("[MainNpcPanel] QuestUIManager was not found in Main scene.");
            ShowPanel();
            RenderLocal(interactable);
            return;
        }

        WorldInteractionPromptRuntime.Hide();
        manager.TalkToNpc(
            interactable.NpcId,
            response =>
            {
                if (interactable == null) return;
                if (TryOpenAcceptedQuest(response?.LinkedQuests)) return;

                ShowPanel();
                RenderApiResponse(response, interactable);
            },
            error =>
            {
                if (interactable == null) return;
                ShowPanel();
                RenderLocal(interactable);
                StartTypewriter(dialogueText, string.IsNullOrWhiteSpace(interactable.GreetingText) ? error : interactable.GreetingText);
                Debug.LogWarning($"[MainNpcPanel] TalkToNpc failed: {error}");
            }
        );
    }

    // Executes bind ui operation.
    private void BindUi()
    {
        npcPanel = npcPanel != null ? npcPanel : FindSceneObject("NPCPanel");
        if (npcPanel == null)
        {
            if (!didBind)
                Debug.LogWarning("[MainNpcPanel] NPCPanel was not found in Main scene.");
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

    // Executes render local operation.
    // Validates input parameters against null or empty values.
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

    // Executes clean name operation.
    // Validates input parameters against null or empty values.
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

    // Executes try open accepted quest operation.
    // Evaluates conditions and returns a boolean result.
    private bool TryOpenAcceptedQuest(IEnumerable<PlayerQuestResponse> linkedQuests)
    {
        var acceptedQuest = linkedQuests?
            .Where(q => q != null &&
                        QuestUIManager.IsStatus(q, "InProgress") &&
                        !HasEnoughQuestProgress(q) &&
                        !RequiresNpcCompletionFlow(q))
            .OrderBy(q => q.QuestId)
            .FirstOrDefault();

        if (acceptedQuest == null)
            return false;

        if (npcPanel != null && npcPanel.activeSelf)
            ClosePanel();

        var questPanelRuntime = MainQuestPanelRuntime.Instance ?? FindQuestPanelRuntime();
        if (questPanelRuntime != null)
            questPanelRuntime.OpenQuestPanelForQuest(acceptedQuest.QuestId);
        else if (UIManager.Instance != null)
            UIManager.Instance.OpenQuestPanel();
        else
            return false;

        return true;
    }

    // Executes requires npc completion flow operation.
    private static bool RequiresNpcCompletionFlow(PlayerQuestResponse quest)
    {
        return IsObjectiveType(quest, "Talk") ||
               IsObjectiveType(quest, "Interact") ||
               IsObjectiveType(quest, "Explore");
    }

    // Executes render api response operation.
    private void RenderApiResponse(TalkToNpcResponse response, WorldInteractable fallback)
    {
        var npc = response?.Npc;
        var linkedQuests = response?.LinkedQuests?
            .Where(q => q != null && !QuestUIManager.IsStatus(q, "Claimed"))
            .OrderBy(q => QuestUIManager.IsStatus(q, "InProgress") ? 0 : QuestUIManager.IsStatus(q, "Completed") ? 1 : 2)
            .ThenBy(q => q.QuestId)
            .ToList() ?? new List<PlayerQuestResponse>();

        currentNpcId = npc?.NPCId ?? fallback.NpcId;
        var dialogues = response?.Npc?.Dialogues?
            .Where(d => d != null && d.IsActive && d.NPCId == currentNpcId)
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.NPCDialogueId)
            .ToList() ?? new List<NPCDialogueResponse>();

        currentDialogues.Clear();
        currentDialogues.AddRange(dialogues);
        currentLinkedQuests.Clear();
        currentLinkedQuests.AddRange(linkedQuests);
        storyDialogueIndex = 0;
        currentStoryDialogue = PickQuestDialogue(dialogues, linkedQuests, storyDialogueIndex) ?? dialogues.FirstOrDefault();
        firstQuestId = currentStoryDialogue?.LinkedQuestId ?? linkedQuests.FirstOrDefault()?.QuestId ?? 0;

        SetText(nameText, CleanName(Safe(npc?.Name, fallback.DisplayName)));
        SetText(roleText, Safe(npc?.Description, fallback.Description));
        StartTypewriter(dialogueText, BuildIntroDialogue(currentStoryDialogue, dialogues, fallback));
        SetText(questHintText, BuildQuestHint(linkedQuests));
        ConfigureNpcActions();
        ApplyPortrait(npc, fallback);
    }

    // Executes build intro dialogue operation.
    private static string BuildIntroDialogue(NPCDialogueResponse currentStoryDialogue, List<NPCDialogueResponse> dialogues, WorldInteractable fallback)
    {
        var intro = currentStoryDialogue ?? dialogues?.FirstOrDefault(d => !d.LinkedQuestId.HasValue) ?? dialogues?.FirstOrDefault();
        return Safe(intro?.Content, Safe(fallback.GreetingText, "Welcome to ElfLand. Talk to me when you are ready for your first quest."));
    }

    // Executes pick quest dialogue operation.
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

        if (linkedQuests != null && linkedQuests.Count > 0)
            return dialogues.FirstOrDefault(d => !d.LinkedQuestId.HasValue);

        return dialogues.FirstOrDefault(d => d.LinkedQuestId.HasValue);
    }

    // Executes build quest hint operation.
    // Validates input parameters against null or empty values.
    private static string BuildQuestHint(List<PlayerQuestResponse> linkedQuests)
    {
        if (linkedQuests == null || linkedQuests.Count == 0)
            return "No linked quest available.";

        var quest = linkedQuests[0];
        // Supported display states: Available, NotStarted, InProgress, Completed, Claimed, or Failed; Available is the UI fallback before acceptance.
        var status = string.IsNullOrWhiteSpace(quest.Status) ? "Available" : quest.Status;
        return $"{quest.QuestTitle} [{status}]";
    }

    // Executes bind fixed action buttons operation.
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
            AddHoverEffect(buttons[i].transform);
        }
    }

    // Executes add hover effect operation.
    private static void AddHoverEffect(Transform t)
    {
        if (t == null) return;
        if (t.GetComponent<UIHoverScaleEffect>() == null)
            t.gameObject.AddComponent<UIHoverScaleEffect>();
    }

    // Initialize or configure default actions; it updates action button.
    private void ConfigureDefaultActions()
    {
        SetActionButton(0, "Greetings.", true, OnStoryDialogueAction);
        SetActionButton(1, "I need some guidance.", false, OnQuestionAction);
        SetActionButton(2, "Do you have any advice for me?", firstQuestId > 0, OnGiftHintAction);
        SetActionButton(3, "Farewell.", true, ClosePanel);
    }

    // Initialize or configure npc actions; it builds story action label, loads linked quest, updates action button, and builds gift hint action label.
    private void ConfigureNpcActions()
    {
        var hasQuestion = HasDialogueType("Question") || HasDialogueType("Help");
        var hasGiftOrHint = currentLinkedQuests.Count > 0 || HasDialogueType("Gift") || HasDialogueType("Hint");

        var activeQuestId = currentLinkedQuests.Count > 0 ? currentLinkedQuests[0].QuestId : (currentStoryDialogue?.LinkedQuestId ?? 0);
        var questDialogues = NpcDialogueFlow.SelectSequence(currentDialogues, currentNpcId, activeQuestId > 0 ? activeQuestId : (int?)null);
        var isMultiLine = questDialogues.Count > 1 && storyDialogueIndex < questDialogues.Count - 1;
        var storyLabel = isMultiLine ? NextPhrases[storyDialogueIndex % NextPhrases.Length] : BuildStoryActionLabel(currentStoryDialogue, FindLinkedQuest(currentStoryDialogue?.LinkedQuestId));

        SetActionButton(0, storyLabel, true, OnStoryDialogueAction);
        SetActionButton(1, "I need some guidance.", hasQuestion, OnQuestionAction);
        SetActionButton(2, BuildGiftHintActionLabel(), hasGiftOrHint, OnGiftHintAction);
        SetActionButton(3, "Farewell.", true, ClosePanel);
    }

    // Executes set action button operation.
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

    // Executes set actions visible operation.
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

    // Executes rebuild action layout operation.
    private void RebuildActionLayout()
    {
        if (actionAreaRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(actionAreaRect);

            if (actionAreaRect.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() == null && actionButtons.Count > 0)
            {
                float spacing = 10f;
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

    // Executes on story dialogue action operation.
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
        var questDialogues = NpcDialogueFlow.SelectSequence(currentDialogues, currentNpcId, activeQuestId > 0 ? activeQuestId : (int?)null);

        if (currentStoryDialogue != null &&
            NpcDialogueFlow.TryAdvance(questDialogues, currentStoryDialogue.NPCDialogueId, out var nextDialogue))
        {
            storyDialogueIndex++;
            currentStoryDialogue = nextDialogue;
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

    // Executes handle linked quest from story operation.
    private void HandleLinkedQuestFromStory(NPCDialogueResponse dialogue, PlayerQuestResponse linkedQuest)
    {
        var manager = GetQuestManager();
        if (manager == null)
        {
            SetText(questHintText, "Quest system is not ready.");
            return;
        }

        var inProgressQuest = currentLinkedQuests.FirstOrDefault(q => QuestUIManager.IsStatus(q, "InProgress"));
        if (inProgressQuest != null)
        {
            if (IsCollectQuest(inProgressQuest) && HasEnoughQuestProgress(inProgressQuest))
            {
                TurnInQuestItemAndRoute(inProgressQuest);
                return;
            }

            if (ShouldAutoCompleteNpcTalkQuest(inProgressQuest))
            {
                CompleteTalkQuestAndRouteToReward(manager, inProgressQuest.QuestId, inProgressQuest);
                return;
            }
        }

        var activeQuest = currentLinkedQuests.FirstOrDefault();
        var questId = activeQuest?.QuestId ?? dialogue?.LinkedQuestId ?? linkedQuest?.QuestId ?? 0;
        if (questId <= 0 || processingQuestIds.Contains(questId))
            return;

        var quest = ResolveQuest(questId, activeQuest ?? linkedQuest, manager);
        if (QuestUIManager.IsStatus(quest, "Claimed"))
        {
            SetText(questHintText, "Reward already claimed.");
            return;
        }

        if (QuestUIManager.IsStatus(quest, "Completed"))
        {
            AutoClaimCompletedQuest(manager, questId, quest);
            return;
        }

        if (QuestUIManager.IsStatus(quest, "InProgress"))
        {
            if (IsCollectQuest(quest) && HasEnoughQuestProgress(quest))
            {
                TurnInQuestItemAndRoute(quest);
                return;
            }

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

    // Executes accept linked quest operation.
    // Validates input parameters against null or empty values.
    private void AcceptLinkedQuest(QuestUIManager manager, int questId, PlayerQuestResponse quest, NPCDialogueResponse dialogue)
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

                if (IsCollectQuest(acceptedQuest) && HasEnoughQuestProgress(acceptedQuest))
                {
                    TurnInQuestItemAndRoute(acceptedQuest);
                    return;
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

    // Executes complete talk quest and route to reward operation.
    private void CompleteTalkQuestAndRouteToReward(QuestUIManager manager, int questId, PlayerQuestResponse quest)
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

                manager.ClaimReward(
                    questId,
                    onSuccess: () =>
                    {
                        processingQuestIds.Remove(questId);
                        WorldRuntimeEvents.RaiseQuestsChanged();
                        ClosePanel();
                    },
                    onError: err =>
                    {
                        processingQuestIds.Remove(questId);
                        Debug.LogWarning($"[MainNpcPanel] Auto claim failed: {err}");
                        RouteToQuestReward(questId, null);
                    });
            },
            error =>
            {
                processingQuestIds.Remove(questId);
                Debug.LogWarning($"[MainNpcPanel] Auto complete talk quest failed: {error}");
                WorldRuntimeEvents.RaiseQuestsChanged();
            }
        );
    }

    // Executes auto claim completed quest operation.
    private void AutoClaimCompletedQuest(QuestUIManager manager, int questId, PlayerQuestResponse quest)
    {
        if (manager == null || questId <= 0 || processingQuestIds.Contains(questId))
            return;

        processingQuestIds.Add(questId);
        manager.ClaimReward(
            questId,
            onSuccess: () =>
            {
                processingQuestIds.Remove(questId);
                WorldRuntimeEvents.RaiseQuestsChanged();
                ClosePanel();
            },

            onError: err =>
            {
                processingQuestIds.Remove(questId);
                Debug.LogWarning($"[MainNpcPanel] Auto-claim completed quest failed: {err}");
                SetText(questHintText, "Come back to claim your reward.");
            });
    }

    // Executes route to quest reward operation.
    // Validates input parameters against null or empty values.
    private void RouteToQuestReward(int questId, string message)
    {
        ClosePanel();

        var questPanelRuntime = MainQuestPanelRuntime.Instance ?? FindQuestPanelRuntime();
        if (questPanelRuntime != null)
        {
            questPanelRuntime.OpenQuestPanelForReward(questId);
            if (!string.IsNullOrWhiteSpace(message))
                questPanelRuntime.ShowPaperPopup(message);
        }
        else if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenQuestPanel();
        }

        WorldRuntimeEvents.RaiseQuestsChanged();
    }

    // Executes resolve quest operation.
    // Validates input parameters against null or empty values.
    // Evaluates conditions and returns a boolean result.
    private static PlayerQuestResponse ResolveQuest(int questId, PlayerQuestResponse fallback, QuestUIManager manager)
    {
        if (questId <= 0)
            return fallback;

        return manager?.GetQuestResponse(questId) ?? fallback;
    }

    // Executes should auto complete npc talk quest operation.
    // Validates input parameters against null or empty values.
    // Evaluates conditions and returns a boolean result.
    private bool ShouldAutoCompleteNpcTalkQuest(PlayerQuestResponse quest)
    {
        if (quest == null) return false;

        var isTalkOrExplore = string.Equals(quest.ObjectiveType, "Talk", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(quest.ObjectiveType, "Explore", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(quest.ObjectiveType, "Interact", StringComparison.OrdinalIgnoreCase);

        if (!isTalkOrExplore) return false;

        string target = quest.ObjectiveTarget;
        if (string.IsNullOrWhiteSpace(target)) return true;

        string currentNpc = CleanName(nameText.Text);
        return target.IndexOf(currentNpc, StringComparison.OrdinalIgnoreCase) >= 0 ||
               currentNpc.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0;
    }


    // Executes turn in quest item and route operation.
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

                if (response.Success && QuestUIManager.IsStatus(response.Quest, "Completed"))
                {
                    var completedQuestId = quest.QuestId;
                    manager.ClaimReward(
                        completedQuestId,
                        onSuccess: () =>
                        {
                            Debug.Log($"[MainNpcPanel] Auto-claimed questId={completedQuestId}");
                            WorldRuntimeEvents.RaiseQuestsChanged();
                            ClosePanel();
                        },
                        onError: err =>
                        {
                            Debug.LogWarning($"[MainNpcPanel] Auto-claim failed ({err}), routing to panel.");
                            RouteToQuestReward(completedQuestId, null);
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

    // Executes replace linked quest operation.
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

    // Executes build gift hint action label operation.
    private string BuildGiftHintActionLabel()
    {
        var quest = QuestUIManager.PickPreferredQuest(currentLinkedQuests);
        if (quest == null)
            return "Do you have any advice for me?";

        if (QuestUIManager.IsStatus(quest, "Completed"))
            return "I have finished this quest.";

        if (QuestUIManager.IsStatus(quest, "InProgress"))
        {
            if (IsCollectQuest(quest))
                return HasEnoughQuestProgress(quest) ? "I have the items you need." : "Any hints for this task?";

            if (ShouldAutoCompleteNpcTalkQuest(quest))
                return "Could we discuss my journey?";

            return "Any hints for this task?";
        }

        return "Do you have any advice for me?";
    }

    // Executes is collect quest operation.
    private static bool IsCollectQuest(PlayerQuestResponse quest)
    {
        return quest != null && string.Equals(quest.ObjectiveType, "Collect", StringComparison.OrdinalIgnoreCase);
    }

    // Executes has enough quest progress operation.
    // Validates input parameters against null or empty values.
    private static bool HasEnoughQuestProgress(PlayerQuestResponse quest)
    {
        if (quest == null)
            return false;

        var target = Mathf.Max(1, quest.TargetAmount);
        return quest.Progress >= target;
    }

    // Executes build missing quest item hint operation.
    // Validates input parameters against null or empty values.
    private static string BuildMissingQuestItemHint(PlayerQuestResponse quest, NPCDialogueResponse dialogue)
    {
        var target = Mathf.Max(1, quest?.TargetAmount ?? 1);
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
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
    // Executes notify quest accepted operation.
    private void NotifyQuestAccepted(PlayerQuestResponse quest, NPCDialogueResponse dialogue)
    {
        if (MainQuestPanelRuntime.Instance == null && FindQuestPanelRuntime() == null)
        {
            var title = Safe(quest?.QuestTitle, Safe(dialogue?.LinkedQuestTitle, "New quest"));
            Debug.Log($"[PaperPopup] Quest Accepted! {title} has been added to your quest log.");
        }
    }
    // Executes on question action operation.
    // Validates input parameters against null or empty values.
    private void OnQuestionAction()
    {
        var dialogue = NpcDialogueFlow.FindChoice(currentDialogues, currentNpcId, "Question", "Help");
        SetText(dialogueText, Safe(dialogue?.Content, "Feel free to ask. Press E to talk to others, P to gather items, and always keep an eye on your Quest Tracker to know what to do next."));
    }

    // Executes on gift hint action operation.
    private void OnGiftHintAction()
    {
        var dialogue = NpcDialogueFlow.FindChoice(currentDialogues, currentNpcId, "Gift", "Hint");
        var quest = QuestUIManager.PickPreferredQuest(currentLinkedQuests);
        if (quest == null)
        {
            SetText(dialogueText, Safe(dialogue?.Content, "I have no gift for you right now. Come back when your path has moved forward."));
            SetText(questHintText, string.Empty);
            return;
        }

        firstQuestId = quest.QuestId;
        SetText(questHintText, BuildQuestHint(new List<PlayerQuestResponse> { quest }));

        if (QuestUIManager.IsStatus(quest, "Completed"))
        {
            AutoClaimCompletedQuest(GetQuestManager(), quest.QuestId, quest);
            return;
        }

        if (QuestUIManager.IsStatus(quest, "InProgress"))
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
    // Executes open first quest operation.
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

    // Executes show panel operation.
    private void ShowPanel()
    {
        WorldInteractionPromptRuntime.Hide();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowPanel(npcPanel);
        else
            npcPanel.SetActive(true);
    }

    // Executes close panel operation.
    private void ClosePanel()
    {
        processingQuestIds.Clear();

        MysticJourney.Features.Quest.QuestWaypointManager.IsTrackingEnabled = true;

        if (UIManager.Instance != null)
            UIManager.Instance.ClosePanel(npcPanel);
        else if (npcPanel != null)
            npcPanel.SetActive(false);

        WorldRuntimeEvents.RaiseQuestsChanged();

        if (MysticJourney.Features.Quest.QuestWaypointManager.Instance != null)
            MysticJourney.Features.Quest.QuestWaypointManager.Instance.RefreshWaypoint();
    }



    // Executes has dialogue type operation.
    // Validates input parameters against null or empty values.
    private bool HasDialogueType(string responseType)
    {
        return FindDialogueByType(responseType) != null;
    }

    // Executes find dialogue by type operation.
    // Validates input parameters against null or empty values.
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
    // Executes find linked quest operation.
    private PlayerQuestResponse FindLinkedQuest(int? questId)
    {
        if (!questId.HasValue)
            return null;

        return currentLinkedQuests.FirstOrDefault(q => q != null && q.QuestId == questId.Value);
    }

    // Executes build story action label operation.
    private string BuildStoryActionLabel(NPCDialogueResponse dialogue, PlayerQuestResponse linkedQuest)
    {
        if (linkedQuest != null)
            return BuildPlayerQuestActionLabel(linkedQuest);

        if (dialogue != null && string.Equals(dialogue.ResponseType, "Dialogue", StringComparison.OrdinalIgnoreCase))
            return "Tell me about this place.";

        return "Tell me about the story.";
    }

    // Executes build player quest action label operation.
    private string BuildPlayerQuestActionLabel(PlayerQuestResponse quest)
    {
        if (QuestUIManager.IsStatus(quest, "Claimed"))
            return "Thank you for your guidance.";

        if (QuestUIManager.IsStatus(quest, "Completed"))
            return "I completed the task.";

        if (QuestUIManager.IsStatus(quest, "InProgress"))
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

    // Executes is objective type operation.
    private static bool IsObjectiveType(PlayerQuestResponse quest, string objectiveType)
    {
        return quest != null && string.Equals(quest.ObjectiveType, objectiveType, StringComparison.OrdinalIgnoreCase);
    }

    // Executes build objective hint operation.
    private static string BuildObjectiveHint(PlayerQuestResponse quest)
    {
        if (quest == null)
            return "Check your Quest Tracker for the next step.";

        var target = Mathf.Max(1, quest.TargetAmount);
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        var progress = Mathf.Clamp(quest.Progress, 0, target);
        var objective = Safe(quest.ObjectiveType, "Explore");
        var targetName = Safe(quest.ObjectiveTarget, "the marked target");
        var location = Safe(quest.ObjectiveLocation, Safe(quest.RegionName, quest.MapName));
        return $"{objective}: {targetName} at {location} ({progress}/{target}).";
    }

    // Executes shorten operation.
    // Validates input parameters against null or empty values.
    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
            return trimmed;

        return trimmed.Substring(0, Mathf.Max(0, maxLength - 3)).TrimEnd() + "...";
    }
    // Executes apply portrait operation.
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
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        imageRoutine = StartCoroutine(LoadSprite(npc.IconUrl, portraitImage));
    }

    // Executes load sprite operation.
    // Validates input parameters against null or empty values.
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
            Debug.LogWarning($"[MainNpcPanel] Load NPC icon failed: {request.error}");
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

    // Executes get library sprite operation.
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

    // Executes find portrait image operation.
    // Validates input parameters against null or empty values.
    private Image FindPortraitImage()
    {
        var portrait = FindDescendant(npcPanel.transform, "PortraitSlot");
        if (portrait == null)
            return null;

        return portrait.GetComponent<Image>() ?? portrait.GetComponentInChildren<Image>(true);
    }

    // Executes get quest manager operation.
    // Validates input parameters against null or empty values.
    private static QuestUIManager GetQuestManager()
    {
        if (QuestUIManager.Instance != null)
            return QuestUIManager.Instance;

        var managers = Resources.FindObjectsOfTypeAll<QuestUIManager>();
        for (var i = 0; i < managers.Length; i++)
        {
            var manager = managers[i];
            if (manager != null && manager.gameObject.scene.IsValid() && !string.IsNullOrEmpty(manager.gameObject.scene.name))
                return manager;
        }

        return null;
    }
    // Executes find quest panel runtime operation.
    // Validates input parameters against null or empty values.
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

    // Executes bind button operation.
    // Validates input parameters against null or empty values.
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

    // Executes resolve url operation.
    // Validates input parameters against null or empty values.
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

    // Executes find scene object operation.
    // Validates input parameters against null or empty values.
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


    // Executes find action button operation.
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
    // Executes find descendant operation.
    // Validates input parameters against null or empty values.
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

    // Executes find text slot operation.
    // Validates input parameters against null or empty values.
    private static TextSlot FindTextSlot(Transform root, string name1, string name2 = null, string name3 = null, string name4 = null, TextSlot skip = default, TextSlot skip2 = default, TextSlot skip3 = default)
    {
        if (root == null)
            return default;

        var names = new[] { name1, name2, name3, name4 }.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
        for (var i = 0; i < names.Length; i++)
        {
            var child = FindDescendant(root, names[i]);
            // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
            var slot = TextSlot.From(child);
            if (slot.IsValid && !slot.Equals(skip) && !slot.Equals(skip2) && !slot.Equals(skip3))
                // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
                return slot;
        }

        return default;
    }

    // Executes set text operation.
    // Validates input parameters against null or empty values.
    private static void SetText(TextSlot slot, string value)
    {
        slot.Set(value);
    }

    // Executes safe operation.
    // Validates input parameters against null or empty values.
    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    // Executes start typewriter operation.
    private void StartTypewriter(TextSlot slot, string text, float speed = 0.03f)
    {
        if (typewriterRoutine != null)
            StopCoroutine(typewriterRoutine);

        fullDialogueText = text ?? string.Empty;
        if (!slot.IsValid) return;

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        typewriterRoutine = StartCoroutine(TypewriterCoroutine(slot, fullDialogueText, speed));
    }

    // Executes typewriter coroutine operation.
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

        // Executes text slot operation.
        public TextSlot(TMP_Text tmp, Text text)
        {
            this.tmp = tmp;
            this.text = text;
        }

        // Executes is valid operation.
        public bool IsValid => tmp != null || text != null;
        // Executes text operation.
        public string Text => tmp != null ? tmp.text : (text != null ? text.text : string.Empty);

        // Executes from operation.
        public static TextSlot From(GameObject target)
        {
            if (target == null)
                return default;

            return new TextSlot(target.GetComponent<TMP_Text>(), target.GetComponent<Text>());
        }

        // Executes set operation.
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

        // Executes equals operation.
        public bool Equals(TextSlot other)
        {
            return tmp == other.tmp && text == other.text;
        }

        // Executes equals operation.
        public override bool Equals(object obj)
        {
            return obj is TextSlot other && Equals(other);
        }

        // Executes get hash code operation.
        public override int GetHashCode()
        {
            unchecked
            {
                return ((tmp != null ? tmp.GetHashCode() : 0) * 397) ^ (text != null ? text.GetHashCode() : 0);
            }
        }
    }
}
