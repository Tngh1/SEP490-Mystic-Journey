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

public class MainQuestPanelRuntime : MonoBehaviour
{
    public static MainQuestPanelRuntime Instance { get; private set; }

    [Header("Scene UI")]
    [SerializeField] private GameObject questTracker;
    [SerializeField] private GameObject questPanel;
    [SerializeField] private GameObject questPopup;

    private Transform questListContent;
    private GameObject questSlotPrefab;
    private Transform rewardItemsContainer;
    private GameObject rewardSlotPrefab;
    private GameObject rewardsContainer;

    private readonly List<PlayerQuestResponse> quests = new List<PlayerQuestResponse>();
    private readonly List<UIQuestListItem> questSlots = new List<UIQuestListItem>();
    private readonly List<UIBaseItemSlot> rewardSlots = new List<UIBaseItemSlot>();

    private readonly Dictionary<int, QuestResponse> questDefinitionCache = new Dictionary<int, QuestResponse>();
    private readonly HashSet<int> pendingQuestDefinitionRequests = new HashSet<int>();

    private GameObject popupLayer;
    private UIQuestPanelView questPanelView;
    private UIQuestPopupView questPopupView;

    private Button trackButton;
    private Image trackButtonImage;
    private TextSlot trackButtonText;
    private Sprite trackActiveSprite;
    private Sprite trackInactiveSprite;

    private TextSlot trackerNumber;
    private TextSlot trackerTitle;
    private TextSlot trackerStatus;
    private TextSlot detailTitle;
    private TextSlot objectiveText;
    private TextSlot descriptionText;
    private TextSlot popupText;
    private GameObject detailProgressObj;
    private TextSlot detailProgress;
    private GameObject detailCompleteIcon;
    private Image detailTypeImage;
    private Sprite detailKillSprite;
    private Sprite detailCollectSprite;
    private Sprite detailTalkSprite;
    private Sprite detailExploreSprite;

    private PlayerQuestResponse selectedQuest;
    private Coroutine popupRoutine;
    private string filter = "All";
    private readonly Dictionary<string, GameObject> filterHighlights = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, Toggle> filterToggles = new Dictionary<string, Toggle>();
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

        if (GetComponent<MysticJourney.Features.Quest.QuestWaypointManager>() == null)
            gameObject.AddComponent<MysticJourney.Features.Quest.QuestWaypointManager>();
    }

    private IEnumerator Start()
    {
        yield return null;
        BindUi();
        RefreshWorldAndQuests();

        WorldRuntimeEvents.QuestsChanged -= RefreshWorldAndQuests;
        WorldRuntimeEvents.QuestsChanged += RefreshWorldAndQuests;
        WorldRuntimeEvents.MapChanged -= OnMapChanged;
        WorldRuntimeEvents.MapChanged += OnMapChanged;
    }

    private void OnDestroy()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshWorldAndQuests;
        WorldRuntimeEvents.MapChanged -= OnMapChanged;

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

    private void OnMapChanged(string mapName)
    {
        RefreshWorldAndQuests();
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
            trackerNumber = trackerNumber.IsValid ? trackerNumber : FindTextSlot(questTracker.transform, "QuestNumber", "TrackerNumber");
            trackerTitle = trackerTitle.IsValid ? trackerTitle : FindTextSlot(questTracker.transform, "QuestName", "TrackerTitle", "QuestTitle", "TitleText");
            trackerStatus = trackerStatus.IsValid ? trackerStatus : FindTextSlot(questTracker.transform, "QuestStatus", "TrackerStatus", "ObjectiveText", "ProgressText");
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
            questSlotPrefab = questSlotPrefab != null ? questSlotPrefab : questPanelView.QuestSlotPrefab;
            rewardItemsContainer = rewardItemsContainer != null ? rewardItemsContainer : questPanelView.RewardItemsContainer;
            rewardSlotPrefab = rewardSlotPrefab != null ? rewardSlotPrefab : questPanelView.RewardSlotPrefab;
            rewardsContainer = rewardsContainer != null ? rewardsContainer : questPanelView.RewardsContainer;

            detailTitle = detailTitle.IsValid ? detailTitle : new TextSlot(questPanelView.QuestTitleTMP, null);
            objectiveText = objectiveText.IsValid ? objectiveText : new TextSlot(questPanelView.ObjectiveTMP, null);
            descriptionText = descriptionText.IsValid ? descriptionText : new TextSlot(questPanelView.DescriptionTMP, null);

            trackActiveSprite = trackActiveSprite != null ? trackActiveSprite : questPanelView.TrackActiveSprite;
            trackInactiveSprite = trackInactiveSprite != null ? trackInactiveSprite : questPanelView.TrackInactiveSprite;
            detailCompleteIcon = detailCompleteIcon != null ? detailCompleteIcon : questPanelView.DetailCompleteIcon;

            detailTypeImage = detailTypeImage != null ? detailTypeImage : questPanelView.QuestTypeImage;
            detailKillSprite = detailKillSprite != null ? detailKillSprite : questPanelView.KillTypeSprite;
            detailCollectSprite = detailCollectSprite != null ? detailCollectSprite : questPanelView.CollectTypeSprite;
            detailTalkSprite = detailTalkSprite != null ? detailTalkSprite : questPanelView.TalkTypeSprite;
            detailExploreSprite = detailExploreSprite != null ? detailExploreSprite : questPanelView.ExploreTypeSprite;
        }

        if (questPanelView == null)
        {
            questListContent = questListContent != null ? questListContent : FindDescendant(questPanel.transform, "QuestListContent")?.transform;
            rewardItemsContainer = rewardItemsContainer != null ? rewardItemsContainer : FindDescendant(questPanel.transform, "RewardItemsContainer")?.transform;
            if (rewardItemsContainer == null)
                rewardItemsContainer = FindDescendant(questPanel.transform, "ReclaimList")?.transform;

            detailTitle = detailTitle.IsValid ? detailTitle : FindTextSlot(questPanel.transform, "QuestTitleText", "QuestTitle", "TitleText");
            objectiveText = objectiveText.IsValid ? objectiveText : FindTextSlot(questPanel.transform, "ObjectiveText", "Objective");
            descriptionText = descriptionText.IsValid ? descriptionText : FindTextSlot(questPanel.transform, "DescriptionText", "Description");
        }

        // ProgressText là object riêng tách khỏi ObjectiveText, chỉ hiện với quest có đếm
        // (targetAmount > 1). Không nằm trong UIQuestPanelView nên bind theo tên.
        if (detailProgressObj == null)
        {
            detailProgressObj = FindDescendant(questPanel.transform, "ProgressText");
            if (detailProgressObj != null)
                detailProgress = TextSlot.From(detailProgressObj);
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

        BindFilterButton("AllButton", "All", "All");
        BindFilterButton("InProgressButton", "InProgress", "In Progress");
        BindFilterButton("CompletedButton", "Completed", "Completed");
        UpdateFilterHighlights();

        if (questPanelView != null && questPanelView.CloseButton != null)
            BindButton(questPanelView.CloseButton.gameObject, CloseQuestPanel);
        else
            BindPanelButton("CloseButton", CloseQuestPanel);

        BindTrackButton();

        didBind = true;
    }

    private void BindTrackButton()
    {
        GameObject btnObj = null;
        if (questPanelView != null && questPanelView.TrackQuestButton != null)
            btnObj = questPanelView.TrackQuestButton.gameObject;
        else
            btnObj = FindDescendant(questPanel.transform, "TrackQuestButton");

        if (btnObj == null)
            return;

        trackButton = BindButton(btnObj, OnTrackButtonClicked);
        trackButtonImage = btnObj.GetComponent<Image>();
        trackButtonText = FindButtonLabel(btnObj);
        UpdateTrackButton();
    }

    private void OnTrackButtonClicked()
    {
        MysticJourney.Features.Quest.QuestWaypointManager.IsTrackingEnabled = !MysticJourney.Features.Quest.QuestWaypointManager.IsTrackingEnabled;
        UpdateTrackButton();
    }

    private void UpdateTrackButton()
    {
        if (trackButton != null)
        {
            bool trackable = selectedQuest != null
                && !QuestUtils.IsStatus(selectedQuest, "Completed")
                && !QuestUtils.IsStatus(selectedQuest, "Claimed");
            if (trackButton.gameObject.activeSelf != trackable)
                trackButton.gameObject.SetActive(trackable);
            if (!trackable)
                return;
        }

        bool enabled = MysticJourney.Features.Quest.QuestWaypointManager.IsTrackingEnabled;

        if (trackButtonImage != null)
        {
            var sprite = enabled ? trackActiveSprite : trackInactiveSprite;
            if (sprite != null)
                trackButtonImage.sprite = sprite;
        }

        if (trackButtonText.IsValid)
            trackButtonText.Set(enabled ? "Tracking" : "Track");
    }

    private void SetFilter(string nextFilter)
    {
        filter = string.IsNullOrWhiteSpace(nextFilter) ? "All" : nextFilter;
        UpdateFilterHighlights();
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
        UpdateTrackButton();
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

        SetText(trackerNumber, $"Quest {active.QuestId}:");
        SetText(trackerTitle, active.QuestTitle ?? string.Empty);

        var statusLabel = QuestUtils.StatusLabel(active);
        var objectiveLine = ObjectiveTextLine(active);
        SetText(trackerStatus, string.IsNullOrWhiteSpace(objectiveLine)
            ? statusLabel
            : $"{statusLabel}\n{objectiveLine}");
    }

    private void RenderQuestList()
    {
        if (questListContent == null || questSlotPrefab == null)
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
            slot.Setup(quest, selectedQuest != null && selectedQuest.QuestId == quest.QuestId, OnQuestSelected);
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
            SetText(objectiveText, string.Empty);
            SetText(descriptionText, "Select a quest to view detail.");
            if (detailCompleteIcon != null) detailCompleteIcon.SetActive(false);
            if (detailProgressObj != null) detailProgressObj.SetActive(false);
            ApplyDetailTypeIcon(null);
            RenderRewardItems(null);
            return;
        }

        SetText(detailTitle, selectedQuest.QuestTitle);
        SetText(objectiveText, ObjectiveTextLine(selectedQuest));
        SetText(descriptionText, Safe(selectedQuest.QuestDescription, "No description."));
        RenderProgress(selectedQuest);

        if (detailCompleteIcon != null)
            detailCompleteIcon.SetActive(QuestUtils.IsStatus(selectedQuest, "Completed") || QuestUtils.IsStatus(selectedQuest, "Claimed"));

        ApplyDetailTypeIcon(selectedQuest.ObjectiveType);
        RenderRewardItems(selectedQuest);
    }

    // Progress hiển thị ở ProgressText riêng, chỉ với quest có đếm (targetAmount > 1).
    // Quest như Talk/Explore một lần (target <= 1) thì ẩn hẳn ô số.
    private void RenderProgress(PlayerQuestResponse quest)
    {
        if (detailProgressObj == null)
            return;

        bool hasCount = quest != null && quest.TargetAmount > 1;
        if (detailProgressObj.activeSelf != hasCount)
            detailProgressObj.SetActive(hasCount);

        if (hasCount && detailProgress.IsValid)
        {
            var current = Mathf.Clamp(quest.Progress, 0, quest.TargetAmount);
            detailProgress.Set($"{current}/{quest.TargetAmount}");
        }
    }

    private void ApplyDetailTypeIcon(string objectiveType)
    {
        if (detailTypeImage == null) return;

        var slot = UIQuestListItem.MapObjectiveType(objectiveType);
        Sprite sprite = slot switch
        {
            UIQuestListItem.QuestTypeSlot.Kill => detailKillSprite,
            UIQuestListItem.QuestTypeSlot.Collect => detailCollectSprite,
            UIQuestListItem.QuestTypeSlot.Talk => detailTalkSprite,
            _ => detailExploreSprite,
        };

        if (sprite != null)
            detailTypeImage.sprite = sprite;

        bool hasSprite = detailTypeImage.sprite != null;
        detailTypeImage.enabled = hasSprite;
        detailTypeImage.gameObject.SetActive(hasSprite);
    }

    private void RenderRewardItems(PlayerQuestResponse quest)
    {
        if (rewardItemsContainer == null)
            return;

        var rewards = BuildRewards(quest);
        bool claimed = QuestUtils.IsStatus(quest, "Claimed");

        if (rewardsContainer != null)
            rewardsContainer.SetActive(rewards.Count > 0);

        if (rewardSlotPrefab == null)
        {
            for (var i = 0; i < rewardSlots.Count; i++)
                rewardSlots[i].gameObject.SetActive(false);
            return;
        }

        for (var i = 0; i < rewards.Count; i++)
        {
            var slot = GetOrCreateRewardSlot(i);
            if (slot == null)
                continue;

            slot.gameObject.SetActive(true);
            slot.transform.SetSiblingIndex(i);
            slot.SetupCustom(rewards[i].Name, rewards[i].Amount, rewards[i].Sprite);
        }

        for (var i = rewards.Count; i < rewardSlots.Count; i++)
            rewardSlots[i].gameObject.SetActive(false);
    }

    private List<RewardViewData> BuildRewards(PlayerQuestResponse quest)
    {
        var rewards = new List<RewardViewData>();
        if (quest == null)
            return rewards;

        var definition = GetCachedQuestDefinition(quest.QuestId);

        if (quest.RewardExperience > 0)
        {
            rewards.Add(new RewardViewData(
                "EXP",
                $"+{quest.RewardExperience}",
                ResolveIconSprite(
                    FirstNonEmpty(quest.RewardExperienceIconUrl, quest.RewardExpIconUrl, definition?.RewardExperienceIconUrl, definition?.RewardExpIconUrl),
                    remoteFirst: false,
                    "reward:exp", "reward:experience", "EXP", "XP", "Experience")));
        }

        if (quest.RewardGold > 0)
        {
            rewards.Add(new RewardViewData(
                "Gold",
                $"+{quest.RewardGold:0}",
                ResolveIconSprite(
                    FirstNonEmpty(quest.RewardGoldIconUrl, definition?.RewardGoldIconUrl),
                    remoteFirst: false,
                    "reward:gold", "Gold", "Currency", "currency:gold")));
        }

        if (quest.RewardGems > 0)
        {
            rewards.Add(new RewardViewData(
                "Gems",
                $"+{quest.RewardGems:0}",
                ResolveIconSprite(
                    FirstNonEmpty(quest.RewardGemsIconUrl, quest.RewardGemIconUrl, definition?.RewardGemsIconUrl, definition?.RewardGemIconUrl),
                    remoteFirst: false,
                    "reward:gems", "reward:gem", "Gems", "Gem", "Diamond", "currency:gems")));
        }

        if (!string.IsNullOrWhiteSpace(quest.RewardItemName) || quest.RewardItemId.HasValue)
        {
            var itemName = !string.IsNullOrWhiteSpace(quest.RewardItemName) ? quest.RewardItemName : $"Item #{quest.RewardItemId.Value}";
            rewards.Add(new RewardViewData(
                itemName,
                "x1",
                ResolveIconSprite(
                    FirstNonEmpty(quest.RewardItemIconUrl, definition?.RewardItemIconUrl),
                    remoteFirst: true,
                    $"item:{quest.RewardItemId}", quest.RewardItemId?.ToString(), itemName, "QuestItem", "RewardItem")));
        }

        if (!string.IsNullOrWhiteSpace(quest.RewardSkillName) || quest.RewardSkillId.HasValue)
        {
            var skillName = RewardSkillLabel(quest);
            rewards.Add(new RewardViewData(skillName, "Skill", GetRewardSkillSprite(quest, skillName)));
        }

        return rewards;
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
        if (string.Equals(filter, "InProgress", StringComparison.OrdinalIgnoreCase))
            return QuestUtils.IsStatus(quest, "InProgress");
        if (string.Equals(filter, "Completed", StringComparison.OrdinalIgnoreCase))
            return QuestUtils.IsStatus(quest, "Completed") || QuestUtils.IsStatus(quest, "Claimed");

        return true;
    }

    private UIQuestListItem GetOrCreateQuestSlot(int index)
    {
        if (index < questSlots.Count)
            return questSlots[index];

        if (questSlotPrefab == null || questListContent == null)
            return null;

        var slotObj = Instantiate(questSlotPrefab, questListContent);
        slotObj.transform.localScale = Vector3.one;
        slotObj.name = $"QuestSlot_{index + 1}";

        var slot = slotObj.GetComponent<UIQuestListItem>();
        if (slot == null)
        {
            Debug.LogError($"[MainQuestPanelRuntime] Quest slot prefab '{questSlotPrefab.name}' is missing a UIQuestListItem component.", slotObj);
            return null;
        }

        questSlots.Add(slot);
        return slot;
    }

    private UIBaseItemSlot GetOrCreateRewardSlot(int index)
    {
        if (index < rewardSlots.Count)
            return rewardSlots[index];

        if (rewardSlotPrefab == null || rewardItemsContainer == null)
            return null;

        var slotObj = Instantiate(rewardSlotPrefab, rewardItemsContainer);
        slotObj.transform.localScale = Vector3.one;
        slotObj.name = $"Reward_{index + 1}";

        var slot = slotObj.GetComponent<UIBaseItemSlot>();
        if (slot == null)
        {
            Debug.LogError($"[MainQuestPanelRuntime] Reward slot prefab '{rewardSlotPrefab.name}' is missing a UIBaseItemSlot component.", slotObj);
            return null;
        }

        rewardSlots.Add(slot);
        return slot;
    }

    private void EnsureRewardContentLayout()
    {
        if (rewardItemsContainer == null)
            return;

        if (rewardItemsContainer.GetComponent<HorizontalLayoutGroup>() != null ||
            rewardItemsContainer.GetComponent<GridLayoutGroup>() != null ||
            rewardItemsContainer.GetComponent<VerticalLayoutGroup>() != null)
            return;

        var layout = rewardItemsContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
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

    private Sprite GetRewardSkillSprite(PlayerQuestResponse quest, string skillName)
    {
        if (quest == null)
            return null;

        var definition = GetCachedQuestDefinition(quest.QuestId);
        return ResolveIconSprite(
            FirstNonEmpty(quest.RewardSkillIconUrl, definition?.RewardSkillIconUrl),
            remoteFirst: true,
            quest.RewardSkillId.HasValue ? $"skill:{quest.RewardSkillId.Value}" : null,
            quest.RewardSkillId?.ToString(),
            skillName,
            "Skill",
            "RewardSkill");
    }

    private QuestResponse GetCachedQuestDefinition(int questId)
    {
        if (questId <= 0)
            return null;

        if (questDefinitionCache.TryGetValue(questId, out var definition))
            return definition;

        EnsureQuestDefinitionLoaded(questId);
        return null;
    }

    private void EnsureQuestDefinitionLoaded(int questId)
    {
        if (questId <= 0 || questDefinitionCache.ContainsKey(questId) || pendingQuestDefinitionRequests.Contains(questId))
            return;

        pendingQuestDefinitionRequests.Add(questId);
        QuestApi.Instance.GetById(
            questId,
            definition =>
            {
                pendingQuestDefinitionRequests.Remove(questId);
                if (definition != null)
                    questDefinitionCache[questId] = definition;

                if (isActiveAndEnabled)
                    RenderAll();
            },
            error =>
            {
                pendingQuestDefinitionRequests.Remove(questId);
                Debug.LogWarning($"[MainQuestPanelRuntime] Load quest definition failed questId={questId}: {error.Message}");
            });
    }

    private Sprite ResolveIconSprite(string remoteUrl, bool remoteFirst, params string[] localKeys)
    {
        if (remoteFirst)
        {
            var remote = GetRemoteSprite(remoteUrl);
            if (remote != null)
                return remote;
        }

        var local = GetLocalSprite(localKeys);
        if (local != null)
            return local;

        if (!remoteFirst)
        {
            var remote = GetRemoteSprite(remoteUrl);
            if (remote != null)
                return remote;
        }

        return null;
    }

    private Sprite GetLocalSprite(params string[] ids)
    {
        if (ids == null || ItemIconDatabase.Instance == null)
            return null;

        for (var i = 0; i < ids.Length; i++)
        {
            var key = ids[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (ItemIconDatabase.Instance.TryGetIcon(key.Trim(), out var dbSprite))
                return dbSprite;
        }

        return null;
    }

    private Sprite GetRemoteSprite(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var cached = RemoteSpriteCache.GetCached(url);
        if (cached != null)
            return cached;

        RemoteSpriteCache.Load(this, url, sprite =>
        {
            if (sprite != null && isActiveAndEnabled)
                RenderAll();
        });

        return null;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return null;

        for (var i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
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
            if (manager != null && manager.gameObject.scene.IsValid() && !string.IsNullOrEmpty(manager.gameObject.scene.name))
                return manager;
        }

        return null;
    }

    private static ToggleGroup EnsureToggleGroup(GameObject host)
    {
        var group = host.GetComponent<ToggleGroup>();
        if (group == null)
            group = host.AddComponent<ToggleGroup>();
        group.allowSwitchOff = false;
        return group;
    }

    private void BindFilterButton(string objectName, string filterValue, string label)
    {
        if (questPanel == null || string.IsNullOrWhiteSpace(objectName))
            return;

        var target = FindDescendant(questPanel.transform, objectName);
        if (target == null)
            return;

        SetText(FindButtonLabel(target), label);

        // Filter buttons trong TopBar là Toggle (có selectedSprite = FilterActive), không phải
        // Button. Toggle nuốt pointer click nên onClick không bao giờ chạy — phải listen
        // onValueChanged và chỉ đổi filter khi toggle bật.
        var toggle = target.GetComponent<Toggle>();
        if (toggle != null)
        {
            // ToggleGroup dùng chung trên parent (TopBar) → hành vi radio: luôn đúng 1 filter
            // bật, và click filter đang bật không tắt được (allowSwitchOff = false).
            var group = target.transform.parent != null
                ? EnsureToggleGroup(target.transform.parent.gameObject)
                : null;
            if (group != null)
                toggle.group = group;

            filterToggles[filterValue] = toggle;
            if (toggle.onValueChanged == null)
                toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                    SetFilter(filterValue);
            });
        }
        else
        {
            BindButton(target, () => SetFilter(filterValue));
        }

        var highlight = FindDescendant(target.transform, "ActiveBackground");
        if (highlight != null)
            filterHighlights[filterValue] = highlight;
    }

    private void UpdateFilterHighlights()
    {
        foreach (var pair in filterHighlights)
        {
            if (pair.Value == null)
                continue;

            bool active = string.Equals(pair.Key, filter, StringComparison.OrdinalIgnoreCase);
            if (pair.Value.activeSelf != active)
                pair.Value.SetActive(active);
        }

        // Đồng bộ toggle khi filter đổi bằng code (vd OpenQuestPanelForReward set "Completed").
        // SetIsOnWithoutNotify để không trigger lại listener → tránh vòng lặp / filter đúp.
        foreach (var pair in filterToggles)
        {
            if (pair.Value == null)
                continue;

            bool active = string.Equals(pair.Key, filter, StringComparison.OrdinalIgnoreCase);
            if (pair.Value.isOn != active)
                pair.Value.SetIsOnWithoutNotify(active);
        }
    }

    private void BindPanelButton(string objectName, UnityEngine.Events.UnityAction action, string label = null)
    {
        if (questPanel == null || string.IsNullOrWhiteSpace(objectName))
            return;

        var target = FindDescendant(questPanel.transform, objectName);
        BindButton(target, action);
        if (!string.IsNullOrWhiteSpace(label))
            SetText(FindButtonLabel(target), label);
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
            if (obj != null && obj.name == objectName && obj.scene.IsValid() && !string.IsNullOrEmpty(obj.scene.name))
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

    private static TextSlot FindTextSlot(Transform root, string name1, string name2 = null, string name3 = null, string name4 = null)
    {
        if (root == null)
            return default;

        var names = new[] { name1, name2, name3, name4 }.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
        for (var i = 0; i < names.Length; i++)
        {
            var child = FindDescendant(root, names[i]);
            var slot = TextSlot.From(child);
            if (slot.IsValid)
                return slot;
        }

        return default;
    }

    private static TextSlot FindButtonLabel(GameObject buttonObject)
    {
        if (buttonObject == null)
            return default;

        return FindTextSlot(buttonObject.transform, "Text (TMP)", "Text", "Label", "TitleText");
    }

    private static void SetText(TextSlot slot, string value)
    {
        slot.Set(value);
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
                if (!tmp.gameObject.activeSelf)
                    tmp.gameObject.SetActive(true);
                tmp.enabled = true;
                tmp.text = value ?? string.Empty;
                return;
            }

            if (text != null)
            {
                if (!text.gameObject.activeSelf)
                    text.gameObject.SetActive(true);
                text.enabled = true;
                text.text = value ?? string.Empty;
            }
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
