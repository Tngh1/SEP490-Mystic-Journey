using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using QuestUtils = MysticJourney.Core.Utilities.QuestUtils;
using GameConstants = MysticJourney.Core.Utilities.GameConstants;

public class MainQuestPanelRuntime : MonoBehaviour
{
    public static MainQuestPanelRuntime Instance { get; private set; }

    [Header("Scene UI")]
    [SerializeField] private GameObject questTracker;
    [SerializeField] private GameObject questPanel;
    [SerializeField] private GameObject questPopup;
    [SerializeField] private Transform questListContent;
    [SerializeField] private UIQuestListItem questSlotPrefab;
    [SerializeField] private Transform rewardListContent;
    [SerializeField] private UIQuestRewardSlot rewardSlotPrefab;
    [SerializeField] private QuestImageLibrary imageLibrary;

    private readonly List<PlayerQuestResponse> quests = new List<PlayerQuestResponse>();
    private readonly List<UIQuestListItem> questSlots = new List<UIQuestListItem>();
    private readonly List<UIQuestRewardSlot> rewardSlots = new List<UIQuestRewardSlot>();

    private GameObject popupLayer;
    private UIQuestPanelView questPanelView;
    private UIQuestPopupView questPopupView;

    // Action Buttons
    private GameObject acceptQuestButtonObject;
    private GameObject completeQuestButtonObject;
    private GameObject declineQuestButtonObject;
    private GameObject claimQuestButtonObject;
    private GameObject claimedButtonObject;

    private Button acceptQuestButton;
    private Button completeQuestButton;
    private Button declineQuestButton;
    private Button claimQuestButton;
    private Button claimedButton;

    private GameObject primaryActionButtonObject;
    private Button primaryActionButton;
    private TextSlot primaryActionButtonText;
    private bool usePrimaryActionButton;

    private TextSlot trackerNumber;
    private TextSlot trackerTitle;
    private TextSlot trackerStatus;
    private TextSlot detailTitle;
    private TextSlot detailType;
    private TextSlot objectiveText;
    private TextSlot detailProgress;
    private TextSlot descriptionText;
    private TextSlot questGiverText;
    private TextSlot rewardsText;
    private TextSlot popupText;

    private PlayerQuestResponse selectedQuest;
    private Coroutine popupRoutine;
    private string filter = "All";
    private int pendingSelectedQuestId;
    private bool popupLayerActivatedByQuest;
    private bool didWarnMissingListTemplate;
    private bool didBind;

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
        RefreshWorldAndQuests();
        WorldRuntimeEvents.QuestsChanged += RefreshWorldAndQuests;
    }

    private void OnDestroy()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshWorldAndQuests;
        if (Instance == this)
            Instance = null;
    }

    public void OpenQuestPanel()
    {
        BindUi();

        if (questPanel == null)
            return;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowPanel(questPanel);
        else
            questPanel.SetActive(true);

        RefreshWorldAndQuests();
    }

    public void OpenQuestPanelForQuest(int questId)
    {
        pendingSelectedQuestId = questId;
        OpenQuestPanel();
    }

    public void OpenQuestPanelForReward(int questId)
    {
        pendingSelectedQuestId = questId;
        filter = "Completed";
        OpenQuestPanel();
    }

    public void CloseQuestPanel()
    {
        if (questPanel == null)
            BindUi();

        if (questPanel == null)
            return;

        if (UIManager.Instance != null)
            UIManager.Instance.ClosePanel(questPanel);
        else
            questPanel.SetActive(false);
    }

    public void RefreshWorldAndQuests()
    {
        BindUi();

        var manager = GetQuestManager();
        if (manager == null)
        {
            Debug.LogWarning("[MainQuestPanelRuntime] QuestManager was not found in Main scene.");
            quests.Clear();
            selectedQuest = null;
            RenderAll();
            return;
        }

        // Render immediately from QuestManager local cache (no API call) so UI is always in sync
        // with the latest known server state (e.g. after TurnInQuestItem / AcceptQuest etc.)
        var cached = manager.GetMainQuests();
        if (cached.Count > 0)
        {
            quests.Clear();
            quests.AddRange(cached);
            selectedQuest = PickSelectedQuest(null);
            RenderAll();
        }

        // Then also hit the API to get any newer state
        manager.LoadMainQuests(
            (loadedQuests, activeQuest) =>
            {
                quests.Clear();
                quests.AddRange(loadedQuests ?? new List<PlayerQuestResponse>());
                selectedQuest = PickSelectedQuest(activeQuest);
                RenderAll();
            },
            error =>
            {
                Debug.LogWarning($"[MainQuestPanelRuntime] Load quests failed: {error}");
                // Don't clear – keep whatever was shown from cache
                RenderAll();
            }
        );
    }

    private void BindUi()
    {
        questTracker = questTracker != null ? questTracker : (gameObject.name == "QuestTracker" ? gameObject : FindSceneObject("QuestTracker"));
        questPanel = questPanel != null ? questPanel : FindSceneObject("QuestPanel");
        questPopup = questPopup != null ? questPopup : FindSceneObject("QuestPopup");
        popupLayer = popupLayer != null ? popupLayer : FindSceneObject("PopupLayer");

        if (questTracker != null)
        {
            questTracker.SetActive(true);
            BindButton(questTracker, OpenQuestPanel);
            // Bind each named child Text separately — tên chính xác theo Hierarchy
            trackerNumber = trackerNumber.IsValid ? trackerNumber : FindTextSlot(questTracker.transform, "QuestNumber", "TrackerNumber");
            trackerTitle  = trackerTitle.IsValid  ? trackerTitle  : FindTextSlot(questTracker.transform, "QuestName",   "TrackerTitle", "QuestTitle", "TitleText");
            trackerStatus = trackerStatus.IsValid ? trackerStatus : FindTextSlot(questTracker.transform, "QuestStatus",  "TrackerStatus", "ObjectiveText", "ProgressText");
        }

        if (questPanel == null)
        {
            if (!didBind)
                Debug.LogWarning("[MainQuestPanelRuntime] QuestPanel was not found in Main scene.");
            didBind = true;
            return;
        }

        questPanelView = questPanelView != null ? questPanelView : questPanel.GetComponent<UIQuestPanelView>();
        if (questPanelView != null)
        {
            questListContent = questListContent != null ? questListContent : questPanelView.QuestListContent;
            questSlotPrefab = questSlotPrefab != null ? questSlotPrefab : questPanelView.QuestSlotTemplate;
            rewardListContent = rewardListContent != null ? rewardListContent : questPanelView.RewardListContent;
            rewardSlotPrefab = rewardSlotPrefab != null ? rewardSlotPrefab : questPanelView.RewardSlotTemplate;

            detailTitle = detailTitle.IsValid ? detailTitle : new TextSlot(questPanelView.QuestTitleTMP, questPanelView.QuestTitleText);
            detailType = detailType.IsValid ? detailType : new TextSlot(questPanelView.QuestTypeTMP, questPanelView.QuestTypeText);
            objectiveText = objectiveText.IsValid ? objectiveText : new TextSlot(questPanelView.ObjectiveTMP, questPanelView.ObjectiveText);
            detailProgress = detailProgress.IsValid ? detailProgress : new TextSlot(questPanelView.ProgressTMP, questPanelView.ProgressText);
            descriptionText = descriptionText.IsValid ? descriptionText : new TextSlot(questPanelView.DescriptionTMP, questPanelView.DescriptionText);
            questGiverText = questGiverText.IsValid ? questGiverText : new TextSlot(questPanelView.QuestGiverTMP, questPanelView.QuestGiverText);
            rewardsText = rewardsText.IsValid ? rewardsText : new TextSlot(questPanelView.RewardsTMP, questPanelView.RewardsText);
        }

        // Only use aggressive fallbacks if UIQuestPanelView is completely missing
        if (questPanelView == null)
        {
            questListContent = questListContent != null ? questListContent : FindDescendant(questPanel.transform, "QuestListContent")?.transform;
            rewardListContent = rewardListContent != null ? rewardListContent : FindDescendant(questPanel.transform, "RewardListContent")?.transform;

            detailTitle = detailTitle.IsValid ? detailTitle : FindTextSlot(questPanel.transform, "QuestTitle", "DetailTitle", "TitleText");
            detailType = detailType.IsValid ? detailType : FindTextSlot(questPanel.transform, "QuestType", "Type", "TypeText");
            objectiveText = objectiveText.IsValid ? objectiveText : FindTextSlot(questPanel.transform, "Objective", "QuestObjective", "ObjectiveText");
            detailProgress = detailProgress.IsValid ? detailProgress : FindTextSlot(questPanel.transform, "Progress", "QuestProgress", "ProgressText");
            descriptionText = descriptionText.IsValid ? descriptionText : FindTextSlot(questPanel.transform, "Description", "QuestDescription", "DescriptionText");
            questGiverText = questGiverText.IsValid ? questGiverText : FindTextSlot(questPanel.transform, "QuestGiver", "Giver", "GiverText");
            rewardsText = rewardsText.IsValid ? rewardsText : FindTextSlot(questPanel.transform, "Rewards", "RewardsTitle", "RewardsText");
        }

        EnsureRewardContentLayout();
        EnsureQuestListContentLayout();
        if (questPopup != null)
        {
            questPopupView = questPopupView != null ? questPopupView : questPopup.GetComponent<UIQuestPopupView>();
            popupText = popupText.IsValid ? popupText : FindTextSlot(questPopup.transform, "PopupText", "MessageText", "TitleText", "Text (TMP)");
            if (!didBind)
                questPopup.SetActive(false);
        }

        BindPanelButton("AllButton", () => SetFilter("All"));
        BindPanelButton("InProgressButton", () => SetFilter("InProgress"));
        BindPanelButton("CompletedButton", () => SetFilter("Completed"));
        BindPanelButton("AllRegionsButton", () => SetFilter("All"));
        BindPanelButton("RefreshButton", RefreshWorldAndQuests);
        
        if (questPanelView != null && questPanelView.CloseButton != null)
            BindButton(questPanelView.CloseButton.gameObject, CloseQuestPanel);
        else
            BindPanelButton("CloseButton", CloseQuestPanel);

        BindQuestActionButtons();

        didBind = true;
    }

    private void BindQuestActionButtons()
    {
        // Yuuko update: Đã bổ sung DeclineQuestButton cho khớp với ActionButtons của bạn
        if (questPanelView != null)
        {
            acceptQuestButtonObject = questPanelView.AcceptQuestButton != null ? questPanelView.AcceptQuestButton.gameObject : FindDescendant(questPanel.transform, "AcceptQuestButton");
            completeQuestButtonObject = questPanelView.CompleteQuestButton != null ? questPanelView.CompleteQuestButton.gameObject : FindDescendant(questPanel.transform, "CompleteQuestButton");
            declineQuestButtonObject = questPanelView.DeclineQuestButton != null ? questPanelView.DeclineQuestButton.gameObject : FindDescendant(questPanel.transform, "DeclineQuestButton");
            claimQuestButtonObject = questPanelView.ClaimQuestButton != null ? questPanelView.ClaimQuestButton.gameObject : FindDescendant(questPanel.transform, "ClaimQuestButton");
            claimedButtonObject = questPanelView.ClaimedButton != null ? questPanelView.ClaimedButton.gameObject : FindDescendant(questPanel.transform, "ClaimedButton");
        }
        else
        {
            acceptQuestButtonObject = FindDescendant(questPanel.transform, "AcceptQuestButton");
            completeQuestButtonObject = FindDescendant(questPanel.transform, "CompleteQuestButton");
            declineQuestButtonObject = FindDescendant(questPanel.transform, "DeclineQuestButton");
            claimQuestButtonObject = FindDescendant(questPanel.transform, "ClaimQuestButton");
            claimedButtonObject = FindDescendant(questPanel.transform, "ClaimedButton");
        }

        var foundActionObjects = new List<GameObject> { acceptQuestButtonObject, completeQuestButtonObject, claimQuestButtonObject }
            .Where(obj => obj != null)
            .Distinct()
            .ToList();

        usePrimaryActionButton = foundActionObjects.Count <= 1 && declineQuestButtonObject == null;
        primaryActionButtonObject = usePrimaryActionButton
            ? (questPanelView != null && questPanelView.PrimaryActionButton != null ? questPanelView.PrimaryActionButton.gameObject : null)
              ?? foundActionObjects.FirstOrDefault()
              ?? FindDescendant(questPanel.transform, "QuestActionButton")
              ?? FindDescendant(questPanel.transform, "PrimaryActionButton")
              ?? FindDescendant(questPanel.transform, "AcceptButton")
            : null;

        if (usePrimaryActionButton && primaryActionButtonObject != null)
        {
            primaryActionButton = BindButton(primaryActionButtonObject, OnPrimaryActionClicked);
            primaryActionButtonText = primaryActionButtonText.IsValid
                ? primaryActionButtonText
                : FindTextSlot(primaryActionButtonObject.transform, "Text (TMP)", "Text", "Label", "TitleText");
            return;
        }

        usePrimaryActionButton = false;
        primaryActionButton = null;
        primaryActionButtonObject = null;

        acceptQuestButton = BindButton(acceptQuestButtonObject, () => AcceptQuest(selectedQuest));
        completeQuestButton = BindButton(completeQuestButtonObject, () => CompleteQuest(selectedQuest));
        claimQuestButton = BindButton(claimQuestButtonObject, () => ClaimReward(selectedQuest));

        // Gán chức năng đóng bảng quest cho nút Decline (Bạn có thể đổi sang API Decline nếu có sau này)
        declineQuestButton = BindButton(declineQuestButtonObject, CloseQuestPanel);

        claimedButton = claimedButtonObject == null ? null : claimedButtonObject.GetComponent<Button>();
        if (claimedButton != null)
            claimedButton.interactable = false;
    }



    private void SetFilter(string nextFilter)
    {
        filter = string.IsNullOrWhiteSpace(nextFilter) ? "All" : nextFilter;
        RenderQuestList();
    }

    private PlayerQuestResponse PickSelectedQuest(PlayerQuestResponse activeFromWorld)
    {
        if (pendingSelectedQuestId > 0)
        {
            var pending = quests.FirstOrDefault(q => q.QuestId == pendingSelectedQuestId);
            if (pending != null)
            {
                pendingSelectedQuestId = 0;
                return pending;
            }
        }

        var sameSelected = QuestUtils.FindSameQuest(quests, selectedQuest);
        if (sameSelected != null)
            return sameSelected;

        var sameActive = QuestUtils.FindSameQuest(quests, activeFromWorld);
        return sameActive ?? QuestUtils.PickPreferredQuest(quests);
    }

    private void RenderAll()
    {
        RenderTracker();
        RenderQuestList();
        RenderQuestDetail();
    }

    private void RenderTracker()
    {
        var active = QuestUtils.PickPreferredQuest(quests);
        if (active == null)
        {
            SetText(trackerNumber, string.Empty);
            SetText(trackerTitle, "Quest Tracker");
            SetText(trackerStatus, ApiClient.Instance.HasToken() ? "No quest available." : "Login to load quests.");
            return;
        }

        // QuestNumber: "Quest {id}:" hoặc chỉ số thứ tự trong danh sách
        var questIndex = quests.IndexOf(active);
        var numberLabel = questIndex >= 0 ? $"Quest {questIndex + 1}:" : $"Quest {active.QuestId}:";
        SetText(trackerNumber, numberLabel);

        // QuestName: chỉ tên quest, không kèm status hay số đếm
        SetText(trackerTitle, active.QuestTitle ?? string.Empty);

        // QuestStatus: trạng thái + tóm tắt objective (không có số đếm để tránh lộn với text khác)
        var statusLabel = QuestUtils.StatusLabel(active);
        var objectiveLine = ObjectiveTextLine(active);
        SetText(trackerStatus, string.IsNullOrWhiteSpace(objectiveLine)
            ? statusLabel
            : $"{statusLabel}\n{objectiveLine}");
    }

    private void RenderQuestList()
    {
        if (questListContent == null || GetQuestSlotSource() == null)
        {
            if (!didWarnMissingListTemplate)
            {
                Debug.LogError("[MainQuestPanelRuntime] Quest list requires questListContent and a questSlotTemplate prefab assigned on UIQuestPanelView.");
                didWarnMissingListTemplate = true;
            }
            return;
        }

        var visible = quests.Where(MatchesFilter).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            var slot = GetOrCreateQuestSlot(i);
            if (slot == null)
                continue;

            var quest = visible[i];
            slot.gameObject.SetActive(true);
            slot.transform.SetSiblingIndex(i);
            slot.Setup(quest, selectedQuest != null && selectedQuest.QuestId == quest.QuestId, OnQuestSelected, GetQuestSprite(quest));
        }

        for (var i = visible.Count; i < questSlots.Count; i++)
            questSlots[i].gameObject.SetActive(false);
    }

    private void OnQuestSelected(PlayerQuestResponse quest)
    {
        selectedQuest = quest;
        RenderQuestList();
        RenderQuestDetail();
    }

    private void RenderQuestDetail()
    {
        if (selectedQuest == null)
        {
            SetText(detailTitle, "Select a quest");
            SetText(detailType, string.Empty);
            SetText(objectiveText, string.Empty);
            SetText(descriptionText, "Select a quest to view detail.");
            SetText(questGiverText, string.Empty);
            SetText(rewardsText, string.Empty);
            RenderRewards(null);
            UpdateActionButtons(null);
            return;
        }

        SetText(detailTitle, selectedQuest.QuestTitle);
        SetText(detailType, $"{Safe(selectedQuest.QuestType, "Main Quest")} - Lv.{selectedQuest.RequiredLevel}");
        SetText(objectiveText, ObjectiveTextLine(selectedQuest));
        SetText(detailProgress, ProgressOnlyLine(selectedQuest));
        SetText(descriptionText, Safe(selectedQuest.QuestDescription, "No description."));
        SetText(questGiverText, $"Quest Giver\n{Safe(selectedQuest.QuestGiverName, "Elder Rowan")}");
        SetText(rewardsText, RewardLine(selectedQuest));
        RenderRewards(selectedQuest);
        UpdateActionButtons(selectedQuest);
    }

    private void RenderRewards(PlayerQuestResponse quest)
    {
        if (rewardListContent == null || GetRewardSlotSource() == null)
            return;

        var rewards = BuildRewards(quest);
        for (var i = 0; i < rewards.Count; i++)
        {
            var slot = GetOrCreateRewardSlot(i);
            if (slot == null)
                continue;

            slot.gameObject.SetActive(true);
            slot.transform.SetSiblingIndex(i);
            slot.Setup(rewards[i].Name, rewards[i].Amount, rewards[i].Sprite);
            slot.SetClaimed(QuestUtils.IsStatus(quest, "Claimed"));
        }

        for (var i = rewards.Count; i < rewardSlots.Count; i++)
        {
            rewardSlots[i].SetClaimed(false);
            rewardSlots[i].gameObject.SetActive(false);
        }
    }

    private List<RewardViewData> BuildRewards(PlayerQuestResponse quest)
    {
        var rewards = new List<RewardViewData>();
        if (quest == null)
            return rewards;

        if (quest.RewardExperience > 0)
            rewards.Add(new RewardViewData("EXP", $"+{quest.RewardExperience}", GetLibrarySprite("reward:exp", "EXP")));
        if (quest.RewardGold > 0)
            rewards.Add(new RewardViewData("Gold", $"+{quest.RewardGold:0}", GetLibrarySprite("reward:gold", "Gold")));
        if (quest.RewardGems > 0)
            rewards.Add(new RewardViewData("Gems", $"+{quest.RewardGems:0}", GetLibrarySprite("reward:gems", "Gems")));
        if (!string.IsNullOrWhiteSpace(quest.RewardItemName) || quest.RewardItemId.HasValue)
        {
            var itemName = !string.IsNullOrWhiteSpace(quest.RewardItemName) ? quest.RewardItemName : $"Item #{quest.RewardItemId.Value}";
            rewards.Add(new RewardViewData(itemName, "x1", GetLibrarySprite($"item:{quest.RewardItemId}", itemName)));
        }
        if (!string.IsNullOrWhiteSpace(quest.RewardSkillName) || quest.RewardSkillId.HasValue)
        {
            var skillName = RewardSkillLabel(quest);
            rewards.Add(new RewardViewData(skillName, "Skill", GetRewardSkillSprite(quest, skillName)));
        }

        return rewards;
    }

    private void UpdateActionButtons(PlayerQuestResponse quest)
    {
        if (usePrimaryActionButton && primaryActionButtonObject != null)
        {
            UpdatePrimaryActionButton(quest);
            return;
        }

        SetActive(acceptQuestButtonObject, QuestUtils.IsStatus(quest, "NotStarted"));
        SetActive(completeQuestButtonObject, QuestUtils.IsStatus(quest, "InProgress"));
        SetActive(claimQuestButtonObject, QuestUtils.IsStatus(quest, "Completed"));
        SetActive(claimedButtonObject, QuestUtils.IsStatus(quest, "Claimed"));

        // Cập nhật trạng thái Decline
        SetActive(declineQuestButtonObject, QuestUtils.IsStatus(quest, "NotStarted") || QuestUtils.IsStatus(quest, "InProgress"));

        if (completeQuestButton != null && quest != null)
            completeQuestButton.interactable = CanCompleteLocally(quest);

        if (acceptQuestButton != null)
            acceptQuestButton.interactable = quest != null;
        if (claimQuestButton != null)
            claimQuestButton.interactable = quest != null;
        if (declineQuestButton != null)
            declineQuestButton.interactable = quest != null;
        if (claimedButton != null)
            claimedButton.interactable = false;
    }

    private void UpdatePrimaryActionButton(PlayerQuestResponse quest)
    {
        if (primaryActionButtonObject == null)
            return;

        var isNotStarted = QuestUtils.IsStatus(quest, "NotStarted");
        var isInProgress = QuestUtils.IsStatus(quest, "InProgress");
        var isCompleted = QuestUtils.IsStatus(quest, "Completed");
        var isClaimed = QuestUtils.IsStatus(quest, "Claimed");
        var visible = quest != null && (isNotStarted || isInProgress || isCompleted || isClaimed);

        primaryActionButtonObject.SetActive(visible);
        if (!visible)
            return;

        var label = isNotStarted ? "Accept Quest" :
                    isInProgress ? "Complete Quest" :
                    isCompleted ? "Claim Reward" :
                    "Claimed";
        SetText(primaryActionButtonText, label);

        if (primaryActionButton != null)
            primaryActionButton.interactable = isNotStarted || isCompleted || (isInProgress && CanCompleteLocally(quest));
    }

    private void OnPrimaryActionClicked()
    {
        if (selectedQuest == null)
            return;

        if (QuestUtils.IsStatus(selectedQuest, "NotStarted"))
        {
            AcceptQuest(selectedQuest);
            return;
        }

        if (QuestUtils.IsStatus(selectedQuest, "InProgress"))
        {
            if (CanCompleteLocally(selectedQuest))
                CompleteQuest(selectedQuest);
            return;
        }

        if (QuestUtils.IsStatus(selectedQuest, "Completed"))
            ClaimReward(selectedQuest);
    }

    private void AcceptQuest(PlayerQuestResponse quest)
    {
        if (quest == null)
            return;

        var manager = GetQuestManager();
        if (manager == null)
        {
            ShowPopup("Quest system is not ready.");
            return;
        }

        manager.AcceptQuest(
            quest.QuestId,
            () =>
            {
                pendingSelectedQuestId = quest.QuestId;
                ShowPopup($"Quest Accepted!\n{quest.QuestTitle} has been added to your quest log.");
                WorldRuntimeEvents.RaiseQuestsChanged();
            },
            error => ShowPopup(error)
        );
    }

    private void CompleteQuest(PlayerQuestResponse quest)
    {
        if (quest == null)
            return;

        var manager = GetQuestManager();
        if (manager == null)
        {
            ShowPopup("Quest system is not ready.");
            return;
        }

        manager.CompleteQuest(
            quest.QuestId,
            () =>
            {
                pendingSelectedQuestId = quest.QuestId;
                ShowPopup("Quest completed. Claim your reward.");
                WorldRuntimeEvents.RaiseQuestsChanged();
            },
            error => ShowPopup(error)
        );
    }

    private void ClaimReward(PlayerQuestResponse quest)
    {
        if (quest == null)
            return;

        var manager = GetQuestManager();
        if (manager == null)
        {
            ShowPopup("Quest system is not ready.");
            return;
        }

        manager.ClaimReward(
            quest.QuestId,
            () =>
            {
                pendingSelectedQuestId = quest.QuestId;
                ShowPopup("Congratulations! Reward claimed.");
                RefreshPlayerLevel();
                WorldRuntimeEvents.RaiseQuestsChanged();
            },
            error => ShowPopup(error)
        );
    }

    private void RefreshPlayerLevel()
    {
        if (!ApiClient.Instance.HasToken())
            return;

        PlayerApi.Instance.GetMyProfile(
            profile =>
            {
                if (profile == null)
                    return;

                WorldState.PlayerLevel = Mathf.Max(1, profile.Level);
                PlayerPrefs.SetInt(ApiConfig.PlayerLevelKey, WorldState.PlayerLevel);
                PlayerPrefs.Save();
                WorldRuntimeEvents.RaiseLevelChanged();
            },
            error => Debug.LogWarning($"[MainQuestPanelRuntime] Refresh profile failed: {error.Message}")
        );
    }

    public void ShowQuestPopup(string message)
    {
        ShowPopup(message);
    }

    private void ShowPopup(string message)
    {
        BindUi();

        if (questPopup == null)
        {
            Debug.Log($"[QuestPopup] {message}");
            return;
        }

        if (questPopupView != null)
            questPopupView.SetMessage(message);
        else
            SetText(popupText, message);

        if (popupLayer != null && !popupLayer.activeSelf)
        {
            popupLayer.SetActive(true);
            popupLayerActivatedByQuest = true;
        }

        if (questPopupView != null)
            questPopupView.Show(message);
        else
        {
            questPopup.SetActive(true);
            questPopup.transform.SetAsLastSibling();
        }

        if (popupRoutine != null)
            StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(HidePopupAfterDelay());
    }

    private IEnumerator HidePopupAfterDelay()
    {
        yield return new WaitForSeconds(2.4f);

        if (questPopupView != null)
            questPopupView.Hide();
        else if (questPopup != null)
            questPopup.SetActive(false);

        if (popupLayer != null && popupLayerActivatedByQuest)
        {
            popupLayer.SetActive(false);
            popupLayerActivatedByQuest = false;
        }
    }

    private bool MatchesFilter(PlayerQuestResponse quest)
    {
        if (filter == "InProgress")
            return QuestUtils.IsStatus(quest, "InProgress");
        if (filter == "Completed")
            return QuestUtils.IsStatus(quest, "Completed") || QuestUtils.IsStatus(quest, "Claimed");
        return true;
    }

    private UIQuestListItem GetOrCreateQuestSlot(int index)
    {
        if (index < questSlots.Count)
            return questSlots[index];

        var source = GetQuestSlotSource();
        if (source == null || questListContent == null)
            return null;

        var slot = Instantiate(source, questListContent);
        slot.transform.localScale = Vector3.one;
        slot.gameObject.name = $"QuestSlot_{index + 1}";
        questSlots.Add(slot);
        return slot;
    }

    private UIQuestRewardSlot GetOrCreateRewardSlot(int index)
    {
        if (index < rewardSlots.Count)
            return rewardSlots[index];

        var source = GetRewardSlotSource();
        if (source == null || rewardListContent == null)
            return null;

        var slot = Instantiate(source, rewardListContent);
        slot.transform.localScale = Vector3.one;
        slot.gameObject.name = $"QuestReward_{index + 1}";
        rewardSlots.Add(slot);
        return slot;
    }

    private UIQuestListItem GetQuestSlotSource()
    {
        return questSlotPrefab;
    }

    private UIQuestRewardSlot GetRewardSlotSource()
    {
        return rewardSlotPrefab;
    }



    private void EnsureRewardContentLayout()
    {
        if (rewardListContent == null)
            return;

        if (rewardListContent.GetComponent<HorizontalLayoutGroup>() != null ||
            rewardListContent.GetComponent<GridLayoutGroup>() != null ||
            rewardListContent.GetComponent<VerticalLayoutGroup>() != null)
            return;

        var layout = rewardListContent.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private void EnsureQuestListContentLayout()
    {
        if (questListContent == null)
            return;

        if (questListContent.GetComponent<HorizontalLayoutGroup>() != null ||
            questListContent.GetComponent<GridLayoutGroup>() != null ||
            questListContent.GetComponent<VerticalLayoutGroup>() != null)
            return;

        var layout = questListContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = questListContent.gameObject.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = questListContent.gameObject.AddComponent<ContentSizeFitter>();
        
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }



    private Sprite GetQuestSprite(PlayerQuestResponse quest)
    {
        if (quest == null)
            return null;

        return GetLibrarySprite($"quest:{quest.QuestId}", quest.QuestId.ToString(), quest.QuestTitle);
    }

    private Sprite GetRewardSkillSprite(PlayerQuestResponse quest, string skillName)
    {
        if (quest == null)
            return null;

        return quest.RewardSkillId.HasValue
            ? GetLibrarySprite($"skill:{quest.RewardSkillId.Value}", quest.RewardSkillId.Value.ToString(), skillName)
            : GetLibrarySprite(skillName);
    }

    private Sprite GetLibrarySprite(params string[] ids)
    {
        if (imageLibrary == null || ids == null)
            return null;

        for (var i = 0; i < ids.Length; i++)
        {
            var sprite = imageLibrary.GetSprite(ids[i]);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private static QuestManager GetQuestManager()
    {
        if (QuestManager.Instance != null)
            return QuestManager.Instance;

        var managers = Resources.FindObjectsOfTypeAll<QuestManager>();
        for (var i = 0; i < managers.Length; i++)
        {
            var manager = managers[i];
            if (manager != null && manager.gameObject.scene.IsValid() && manager.gameObject.scene.name == "Main")
                return manager;
        }

        return null;
    }

    private void BindPanelButton(string objectName, UnityEngine.Events.UnityAction action)
    {
        if (questPanel == null || string.IsNullOrWhiteSpace(objectName))
            return;

        var target = FindDescendant(questPanel.transform, objectName);
        BindButton(target, action);
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

    private static UIQuestListItem EnsureQuestSlotComponent(GameObject target)
    {
        if (target == null)
            return null;

        var item = target.GetComponent<UIQuestListItem>();
        if (item == null)
            item = target.AddComponent<UIQuestListItem>();
        return item;
    }

    private static UIQuestRewardSlot EnsureRewardSlotComponent(GameObject target)
    {
        if (target == null)
            return null;

        var item = target.GetComponent<UIQuestRewardSlot>();
        if (item == null)
            item = target.AddComponent<UIQuestRewardSlot>();
        return item;
    }



    private static bool CanCompleteLocally(PlayerQuestResponse quest)
    {
        if (quest == null)
            return false;

        if (string.Equals(quest.ObjectiveType, "Talk", StringComparison.OrdinalIgnoreCase))
            return true;

        return quest.Progress >= Mathf.Max(1, quest.TargetAmount);
    }

    private static string StatusLabel(PlayerQuestResponse quest)
    {
        if (quest == null)
            return "Unknown";

        return quest.Status switch
        {
            "NotStarted" => "Available",
            "InProgress" => "In Progress",
            "Completed" => "Completed",
            "Claimed" => "Claimed",
            _ => Safe(quest.Status, "Unknown")
        };
    }

    private static string ObjectiveLine(PlayerQuestResponse quest)
    {
        if (quest == null)
            return string.Empty;

        var current = Mathf.Clamp(quest.Progress, 0, Mathf.Max(1, quest.TargetAmount));
        var target = Mathf.Max(1, quest.TargetAmount);
        var objective = Safe(quest.ObjectiveType, "Explore");
        var targetName = Safe(quest.ObjectiveTarget, "target");
        var location = Safe(quest.ObjectiveLocation, Safe(quest.RegionName, quest.MapName));
        return $"{objective}: {targetName} at {location}  {current}/{target}";
    }

    private static string ObjectiveTextLine(PlayerQuestResponse quest)
    {
        if (quest == null)
            return string.Empty;

        var objective = Safe(quest.ObjectiveType, "Explore");
        var targetName = Safe(quest.ObjectiveTarget, "target");
        var location = Safe(quest.ObjectiveLocation, Safe(quest.RegionName, quest.MapName));
        return $"{objective}: {targetName} at {location}";
    }

    private static string ProgressOnlyLine(PlayerQuestResponse quest)
    {
        if (quest == null)
            return string.Empty;

        var current = Mathf.Clamp(quest.Progress, 0, Mathf.Max(1, quest.TargetAmount));
        var target = Mathf.Max(1, quest.TargetAmount);
        return $"{current}/{target}";
    }
    private static string RewardLine(PlayerQuestResponse quest)
    {
        if (quest == null)
            return string.Empty;

        var parts = new List<string>();
        if (quest.RewardExperience > 0)
            parts.Add($"EXP +{quest.RewardExperience}");
        if (quest.RewardGold > 0)
            parts.Add($"Gold +{quest.RewardGold:0}");
        if (quest.RewardGems > 0)
            parts.Add($"Gems +{quest.RewardGems:0}");
        if (!string.IsNullOrWhiteSpace(quest.RewardItemName))
            parts.Add($"Item: {quest.RewardItemName}");
        if (!string.IsNullOrWhiteSpace(quest.RewardSkillName) || quest.RewardSkillId.HasValue)
            parts.Add($"Skill: {RewardSkillLabel(quest)}");

        return parts.Count == 0 ? "No reward." : string.Join(" | ", parts);
    }

    private static string RewardSkillLabel(PlayerQuestResponse quest)
    {
        if (quest == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(quest.RewardSkillName)
            ? quest.RewardSkillName
            : quest.RewardSkillId.HasValue ? $"Skill #{quest.RewardSkillId.Value}" : string.Empty;
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (var i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj != null && obj.name == objectName && obj.scene.IsValid() && obj.scene.name == "Main")
                return obj;
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

    private static TextSlot FindTextSlot(Transform root, params string[] names)
    {
        return FindTextSlot(root, names, default, default, default, default, default);
    }

    private static TextSlot FindTextSlot(Transform root, string name1, string name2 = null, string name3 = null, string name4 = null, TextSlot skip = default, TextSlot skip2 = default, TextSlot skip3 = default, TextSlot skip4 = default, TextSlot skip5 = default)
    {
        var names = new[] { name1, name2, name3, name4 }.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
        return FindTextSlot(root, names, skip, skip2, skip3, skip4, skip5);
    }

    private static TextSlot FindTextSlot(Transform root, string[] names, TextSlot skip, TextSlot skip2, TextSlot skip3, TextSlot skip4, TextSlot skip5)
    {
        if (root == null)
            return default;

        for (var i = 0; i < names.Length; i++)
        {
            var child = FindDescendant(root, names[i]);
            var slot = TextSlot.From(child);
            if (slot.IsValid && !slot.Equals(skip) && !slot.Equals(skip2) && !slot.Equals(skip3) && !slot.Equals(skip4) && !slot.Equals(skip5))
                return slot;
        }

        return default;
    }

    private static void SetText(TextSlot slot, string value)
    {
        slot.Set(value);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    private readonly struct RewardViewData
    {
        public RewardViewData(string name, string amount, Sprite sprite)
        {
            Name = name;
            Amount = amount;
            Sprite = sprite;
        }

        public string Name { get; }
        public string Amount { get; }
        public Sprite Sprite { get; }
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



