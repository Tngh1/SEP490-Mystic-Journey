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
    [SerializeField] private GameObject paperPopup;

    private Transform questListContent;
    private GameObject questSlotPrefab;
    private Transform rewardItemsContainer;
    private GameObject rewardSlotPrefab;
    private GameObject skillRewardSlotPrefab;
    private GameObject skillRewardSlotInstance;
    private SkillUIManager skillPanelManager;
    private GameObject rewardsContainer;

    private readonly List<PlayerQuestResponse> quests = new List<PlayerQuestResponse>();
    private readonly List<UIQuestListItem> questSlots = new List<UIQuestListItem>();
    private readonly List<UIBaseItemSlot> rewardSlots = new List<UIBaseItemSlot>();

    private readonly Dictionary<int, QuestResponse> questDefinitionCache = new Dictionary<int, QuestResponse>();
    private readonly HashSet<int> pendingQuestDefinitionRequests = new HashSet<int>();

    private GameObject popupLayer;
    private QuestPanel questPanelView;
    private UIPaperPopupView paperPopupView;

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
    private bool popupLayerActivatedByPaperPopup;
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

    private Coroutine waitForQuestDataRoutine;

    private IEnumerator Start()
    {
        yield return null;
        BindUi();
        RefreshWorldAndQuests();

        WorldRuntimeEvents.QuestsChanged -= RefreshWorldAndQuests;
        WorldRuntimeEvents.QuestsChanged += RefreshWorldAndQuests;
        WorldRuntimeEvents.MapChanged -= OnMapChanged;
        WorldRuntimeEvents.MapChanged += OnMapChanged;
        if (QuestUIManager.Instance != null)
        {
            QuestUIManager.Instance.OnQuestProgressChanged -= OnQuestProgressChangedHandler;
            QuestUIManager.Instance.OnQuestProgressChanged += OnQuestProgressChangedHandler;
            QuestUIManager.Instance.OnQuestsLoaded -= OnQuestsLoadedHandler;
            QuestUIManager.Instance.OnQuestsLoaded += OnQuestsLoadedHandler;
        }

        // Nếu sau BindUi + Refresh mà vẫn không có quest (QuestUIManager chưa load xong API),
        // bắt đầu retry để đảm bảo tracker cập nhật ngay khi dữ liệu sẵn sàng.
        if (quests.Count == 0)
        {
            if (waitForQuestDataRoutine != null) StopCoroutine(waitForQuestDataRoutine);
            waitForQuestDataRoutine = StartCoroutine(WaitForQuestData());
        }
    }

    private void OnDestroy()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshWorldAndQuests;
        WorldRuntimeEvents.MapChanged -= OnMapChanged;

        if (QuestUIManager.Instance != null)
        {
            QuestUIManager.Instance.OnQuestProgressChanged -= OnQuestProgressChangedHandler;
            QuestUIManager.Instance.OnQuestsLoaded -= OnQuestsLoadedHandler;
        }

        if (Instance == this)
            Instance = null;
    }

    private void OnQuestProgressChangedHandler(int questId)
    {
        RefreshWorldAndQuests();
    }

    /// <summary>
    /// Gọi khi QuestUIManager load xong quest từ server (HandleLoadedQuestResponses).
    /// Đảm bảo tracker cập nhật ngay mà không cần chờ QuestsChanged event.
    /// </summary>
    private void OnQuestsLoadedHandler()
    {
        // Hủy retry nếu đang chạy — quest đã sẵn sàng.
        if (waitForQuestDataRoutine != null)
        {
            StopCoroutine(waitForQuestDataRoutine);
            waitForQuestDataRoutine = null;
        }

        // Đăng ký lại QuestUIManager events phòng trường hợp QuestUIManager bị tạo lại.
        if (QuestUIManager.Instance != null)
        {
            QuestUIManager.Instance.OnQuestProgressChanged -= OnQuestProgressChangedHandler;
            QuestUIManager.Instance.OnQuestProgressChanged += OnQuestProgressChangedHandler;
            QuestUIManager.Instance.OnQuestsLoaded -= OnQuestsLoadedHandler;
            QuestUIManager.Instance.OnQuestsLoaded += OnQuestsLoadedHandler;
        }

        RefreshWorldAndQuests();
    }

    /// <summary>
    /// Retry coroutine: nếu QuestUIManager chưa sẵn sàng hoặc chưa load xong quest từ API,
    /// thử lại mỗi 0.5s tối đa 10 lần (5 giây). Cover trường hợp QuestUIManager được tạo
    /// SAU MainQuestPanelRuntime và OnQuestsLoaded không được đăng ký kịp.
    /// </summary>
    private IEnumerator WaitForQuestData()
    {
        var wait = new WaitForSeconds(0.5f);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            yield return wait;

            // QuestUIManager có thể xuất hiện muộn (UIManager.EnsureQuestManager) → đăng ký lại.
            if (QuestUIManager.Instance != null)
            {
                QuestUIManager.Instance.OnQuestProgressChanged -= OnQuestProgressChangedHandler;
                QuestUIManager.Instance.OnQuestProgressChanged += OnQuestProgressChangedHandler;
                QuestUIManager.Instance.OnQuestsLoaded -= OnQuestsLoadedHandler;
                QuestUIManager.Instance.OnQuestsLoaded += OnQuestsLoadedHandler;
            }

            RefreshWorldAndQuests();

            if (quests.Count > 0)
                break;
        }

        waitForQuestDataRoutine = null;
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
        // Không gọi LoadMyQuests() ở đây: lúc MapChanged bắn ra, LastMapName trên server vẫn là
        // map cũ nên response sẽ thiếu quest của map mới. MapSceneController nạp lại quest ngay
        // sau khi UpdatePosition thành công, rồi QuestsChanged sẽ kéo panel về đúng dữ liệu.
        RefreshWorldAndQuests();
    }

    public void RefreshWorldAndQuests()
    {
        BindUi();

        var manager = GetQuestManager();
        if (manager == null)
        {
            Debug.LogWarning("[MainQuestPanelRuntime] QuestUIManager was not found in Main scene.");
            quests.Clear();
            selectedQuest = null;
            RenderAll();
            return;
        }

        // Render immediately from QuestUIManager local cache (no API call) so UI is always in sync
        // with the latest known server state (e.g. after TurnInQuestItem / AcceptQuest etc.)
        var cached = manager.GetMainQuests();
        if (cached.Count > 0)
        {
            quests.Clear();
            quests.AddRange(cached);
            selectedQuest = PickSelectedQuest(null);
            RenderAll();
            return; // Dừng tại đây khi đã có dữ liệu local, không phát sinh HTTP Request dư thừa
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
        paperPopup = paperPopup != null ? paperPopup : FindSceneObject("PaperPopup");
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

        questPanelView = questPanelView != null ? questPanelView : questPanel.GetComponent<QuestPanel>();
        if (questPanelView != null)
        {
            questListContent = questListContent != null ? questListContent : questPanelView.QuestListContent;
            questSlotPrefab = questSlotPrefab != null ? questSlotPrefab : questPanelView.QuestSlotPrefab;
            rewardItemsContainer = rewardItemsContainer != null ? rewardItemsContainer : questPanelView.RewardItemsContainer;
            rewardSlotPrefab = rewardSlotPrefab != null ? rewardSlotPrefab : questPanelView.RewardSlotPrefab;
            skillRewardSlotPrefab = skillRewardSlotPrefab != null ? skillRewardSlotPrefab : questPanelView.SkillRewardSlotPrefab;
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
        // (targetAmount > 1). Không nằm trong QuestPanel nên bind theo tên.
        if (detailProgressObj == null)
        {
            detailProgressObj = FindDescendant(questPanel.transform, "ProgressText");
            if (detailProgressObj != null)
                detailProgress = TextSlot.From(detailProgressObj);
        }

        EnsureRewardContentLayout();
        BindSkillRewardAssets();
        EnsureQuestListContentLayout();

        if (paperPopup != null)
        {
            paperPopupView = paperPopupView != null ? paperPopupView : paperPopup.GetComponent<UIPaperPopupView>();
            popupText = popupText.IsValid ? popupText : FindTextSlot(paperPopup.transform, "PopupText", "MessageText", "TitleText", "Text (TMP)");
            if (!didBind)
                paperPopup.SetActive(false);
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
        AddHoverEffects();

        didBind = true;
    }

    // UIHoverScaleEffect (Assets/Scripts/UI/UIHoverScaleEffect.cs) là hover dùng chung.
    // UIManager.EnsureButtonHoverEffects đã quét sẵn toàn scene lúc Awake, nên vòng này chỉ
    // còn cần cho các dòng quest Instantiate lúc runtime. Phải quét 3 root riêng: tracker và
    // PaperPopup không phải con của QuestPanel nên một lần quét từ panel sẽ bỏ sót.
    // Gọi sau BindTrackButton để bắt cả Button mà BindButton vừa AddComponent.
    private void AddHoverEffects()
    {
        AddHoverIn(questPanel);
        AddHoverIn(questTracker);
        AddHoverIn(paperPopup);
    }

    // Quét Selectable chứ không phải Button: filter trong TopBar là Toggle (xem BindFilterButton).
    private static void AddHoverIn(GameObject root)
    {
        if (root == null)
            return;

        // true: nút trong popup đang tắt vẫn phải được gắn, nếu không popup mở ra là mất hover.
        foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
        {
            if (selectable == null)
                continue;
            if (!(selectable is Button || selectable is Toggle))
                continue;
            // DimBackground là lớp phủ mờ toàn màn hình (bấm ra ngoài để đóng); phóng to nó
            // sẽ kéo giãn cả mảng tối mỗi khi chuột đi qua vùng trống.
            if (selectable.name == "DimBackground")
                continue;
            if (selectable.GetComponent<UIHoverScaleEffect>() == null)
                selectable.gameObject.AddComponent<UIHoverScaleEffect>();
        }
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
        bool wasEnabled = MysticJourney.Features.Quest.QuestWaypointManager.IsTrackingEnabled;
        MysticJourney.Features.Quest.QuestWaypointManager.IsTrackingEnabled = !wasEnabled;
        UpdateTrackButton();

        // Khi bật track, trigger mũi tên ngay lập tức thay vì chờ 2s routine
        if (MysticJourney.Features.Quest.QuestWaypointManager.IsTrackingEnabled &&
            MysticJourney.Features.Quest.QuestWaypointManager.Instance != null)
        {
            MysticJourney.Features.Quest.QuestWaypointManager.Instance.RefreshWaypoint();
        }
    }

    private void UpdateTrackButton()
    {
        if (trackButton != null)
        {
            // Chỉ hiện nút Track khi quest đang InProgress
            // (NotStarted chưa accept, Completed/Claimed không cần dẫn đường)
            bool trackable = selectedQuest != null
                && QuestUtils.IsStatus(selectedQuest, "InProgress");
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

        if (QuestUtils.IsStatus(active, "Completed"))
        {
            // Completed nhưng chưa Claimed: phần thưởng vẫn đang chờ. Nói rõ bước kế tiếp
            // thay vì chỉ dán nhãn "Completed" — nếu không player không biết phải làm gì.
            SetText(trackerStatus, "<color=#55FF55>Come back to claim your reward.</color>");
        }
        else if (QuestUtils.IsStatus(active, "NotStarted"))
        {
            // Quest chưa nhận: KHÔNG hiện objective (Defeat/Collect...) vì người chơi chưa
            // được giao mục tiêu đó — bước thật sự là đi gặp NPC. Waypoint cũng đang chỉ vào NPC.
            SetText(trackerStatus, $"<color=#FFD34D>{AcceptPromptLine(active)}</color>");
        }
        else
        {
            var targetName = Safe(active.ObjectiveTarget, "target");
            var objectiveType = Safe(active.ObjectiveType, "Objective");
            if (active.TargetAmount > 1)
            {
                var current = Mathf.Clamp(active.Progress, 0, active.TargetAmount);
                SetText(trackerStatus, $"{objectiveType}: {targetName} ({current}/{active.TargetAmount})");
            }
            else
            {
                SetText(trackerStatus, $"{objectiveType}: {targetName}");
            }
        }
    }

    private void RenderQuestList()
    {
        if (questListContent == null || questSlotPrefab == null)
        {
            if (!didWarnMissingListTemplate)
            {
                Debug.LogError("[MainQuestPanelRuntime] Quest list requires questListContent and a questSlotTemplate prefab assigned on QuestPanel.");
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
        UpdateTrackButton();
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

        // Chỉ đóng dấu hoàn thành khi đã nhận thưởng (Claimed), giống UIQuestListItem.
        if (detailCompleteIcon != null)
            detailCompleteIcon.SetActive(QuestUtils.IsStatus(selectedQuest, "Claimed"));

        ApplyDetailTypeIcon(selectedQuest.ObjectiveType);
        RenderRewardItems(selectedQuest);
    }

    // Progress hiển thị ở ProgressText riêng, chỉ với quest có đếm (targetAmount > 1).
    // Quest như Talk/Explore một lần (target <= 1) thì ẩn hẳn ô số.
    private void RenderProgress(PlayerQuestResponse quest)
    {
        if (detailProgressObj == null)
            return;

        // Quest chưa nhận thì chưa có tiến trình để đếm — ẩn ô số thay vì hiện 0/8.
        bool hasCount = quest != null && quest.TargetAmount > 1 && !QuestUtils.IsStatus(quest, "NotStarted");
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
        }

        var itemRewardIndex = 0;
        RewardViewData? skillReward = null;
        for (var i = 0; i < rewards.Count; i++)
        {
            if (rewards[i].IsSkill)
            {
                skillReward = rewards[i];
                continue;
            }

            var slot = GetOrCreateRewardSlot(itemRewardIndex);
            if (slot == null)
                continue;

            slot.gameObject.SetActive(true);
            slot.transform.SetSiblingIndex(itemRewardIndex);
            slot.SetupCustom(rewards[i].Name, rewards[i].Amount, rewards[i].Sprite);
            itemRewardIndex++;
        }

        for (var i = itemRewardIndex; i < rewardSlots.Count; i++)
            rewardSlots[i].gameObject.SetActive(false);

        RenderSkillReward(skillReward, itemRewardIndex);
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
            rewards.Add(new RewardViewData(
                skillName,
                "Skill",
                GetRewardSkillSprite(quest, skillName),
                isSkill: true,
                skillId: quest.RewardSkillId));
        }

        return rewards;
    }

    // Overload cũ: message tự do (AnnounceText), kind suy từ nội dung. Dùng cho các chỗ
    // không gắn với 1 quest cụ thể (vd MapTeleportPortal "Explored: ...").
    private struct PopupData
    {
        public string announce;
        public UIPaperPopupView.PaperPopupKind kind;
        public bool inferKind;
    }

    private readonly Queue<PopupData> popupQueue = new Queue<PopupData>();
    private bool isProcessingPopupQueue;
    private string lastPopupKey = string.Empty;
    private float lastPopupTime;

    public void ShowPaperPopup(string message)
    {
        ShowPopup(message, UIPaperPopupView.PaperPopupKind.None, inferKind: true);
    }

    public void ShowPaperPopup(string questTitle, UIPaperPopupView.PaperPopupKind kind)
    {
        ShowPopup(questTitle, kind, inferKind: false);
    }

    private void ShowPopup(string announce, UIPaperPopupView.PaperPopupKind kind, bool inferKind)
    {
        if (string.IsNullOrWhiteSpace(announce))
            return;

        // Anti-duplicate: nếu popup cùng nội dung + kind vừa hiện trong vòng 2.5 giây -> bỏ qua tránh trùng
        string key = $"{announce}_{kind}_{inferKind}";
        if (key == lastPopupKey && Time.time - lastPopupTime < 2.5f)
            return;

        lastPopupKey = key;
        lastPopupTime = Time.time;

        popupQueue.Enqueue(new PopupData { announce = announce, kind = kind, inferKind = inferKind });

        // QuestVideoManager tắt cả QuestTracker (GameObject chứa script này) trong lúc chiếu video,
        // nên không thể StartCoroutine ở đây. Cứ để popup nằm trong queue — OnEnable sẽ chạy tiếp
        // khi QuestTracker được bật lại sau video.
        if (!isProcessingPopupQueue && gameObject.activeInHierarchy)
        {
            if (popupRoutine != null) StopCoroutine(popupRoutine);
            popupRoutine = StartCoroutine(ProcessPopupQueue());
        }
    }

    private void OnEnable()
    {
        if (!isProcessingPopupQueue && popupQueue.Count > 0)
            popupRoutine = StartCoroutine(ProcessPopupQueue());
    }

    private void OnDisable()
    {
        // Unity giết coroutine khi GameObject bị tắt, nhưng cờ vẫn đang bật -> OnEnable sẽ
        // không chạy lại và MỌI popup sau đó im lặng. Reset để lần bật lại xử lý tiếp queue.
        isProcessingPopupQueue = false;
        popupRoutine = null;
    }

    private IEnumerator ProcessPopupQueue()
    {
        isProcessingPopupQueue = true;

        while (popupQueue.Count > 0)
        {
            // Nếu video đang chiếu -> tạm dừng chờ video chiếu xong mới hiện popup hoàn thành!
            while (MysticJourney.Features.Quest.QuestVideoManager.IsVideoPlaying)
            {
                yield return new WaitForSeconds(0.2f);
            }

            var data = popupQueue.Dequeue();
            BindUi();


            if (paperPopup != null)
            {
                if (popupLayer != null && !popupLayer.activeSelf)
                {
                    popupLayer.SetActive(true);
                    popupLayerActivatedByPaperPopup = true;
                }

                if (paperPopupView != null)
                {
                    if (data.inferKind)
                        paperPopupView.Show(data.announce);
                    else
                        paperPopupView.Show(data.announce, data.kind);
                }
                else
                {
                    SetText(popupText, data.announce);
                    paperPopup.SetActive(true);
                    paperPopup.transform.SetAsLastSibling();
                }

                yield return new WaitForSeconds(2.2f);
            }
        }

        // Đã hiện hết queue -> đóng popup
        if (paperPopupView != null)
            paperPopupView.Hide();
        else if (paperPopup != null)
            paperPopup.SetActive(false);

        if (popupLayer != null && popupLayerActivatedByPaperPopup)
        {
            // PopupLayer là container DÙNG CHUNG cho 14 popup (MapPopup, NPCPanel, ChestPanel...).
            // Tắt cả layer vì popup quest đã xong sẽ kéo theo mọi popup khác đang mở — người chơi
            // mở popup dịch chuyển map, ăn một popup quest, rồi popup map biến mất không lý do.
            // Chỉ tắt khi KHÔNG còn popup nào khác đang mở.
            if (!HasOtherActivePopup())
                popupLayer.SetActive(false);

            popupLayerActivatedByPaperPopup = false;
        }

        isProcessingPopupQueue = false;
        popupRoutine = null;
    }

    /// <summary>
    /// True nếu trong PopupLayer còn popup nào khác (không phải PaperPopup) đang bật.
    /// Xét activeSelf của con trực tiếp: mọi popup ở đây đều là sibling và tự bật/tắt chính nó,
    /// nên đó đúng là "đang mở" — không dùng activeInHierarchy vì lúc gọi hàm này layer có thể
    /// vẫn đang bật và ta cần biết trạng thái riêng của từng popup.
    /// </summary>
    private bool HasOtherActivePopup()
    {
        if (popupLayer == null) return false;

        var layerTransform = popupLayer.transform;
        for (int i = 0; i < layerTransform.childCount; i++)
        {
            var child = layerTransform.GetChild(i);
            if (child == null) continue;
            if (paperPopup != null && child.gameObject == paperPopup) continue;

            if (child.gameObject.activeSelf)
            {
                Debug.Log(
                    $"[MainQuestPanelRuntime] Keeping PopupLayer on: '{child.name}' is still open.");
                return true;
            }
        }

        return false;
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

        // Slot sinh sau BindUi nên AddHoverEffects không với tới; gắn ngay lúc tạo.
        // UIQuestListItem.Bind sẽ tự AddComponent<Button> làm raycast target cho hover.
        if (slotObj.GetComponent<UIHoverScaleEffect>() == null)
            slotObj.AddComponent<UIHoverScaleEffect>();

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

    private void BindSkillRewardAssets()
    {
        if (skillPanelManager == null)
            skillPanelManager = FindFirstObjectByType<SkillUIManager>(FindObjectsInactive.Include);

        if (skillRewardSlotPrefab == null && skillPanelManager != null)
            skillRewardSlotPrefab = skillPanelManager.skillItemPrefab;
    }

    private void RenderSkillReward(RewardViewData? reward, int siblingIndex)
    {
        if (!reward.HasValue)
        {
            if (skillRewardSlotInstance != null)
                skillRewardSlotInstance.SetActive(false);
            return;
        }

        BindSkillRewardAssets();
        if (skillRewardSlotPrefab == null || rewardItemsContainer == null)
        {
            Debug.LogError("[MainQuestPanelRuntime] SkillItemPrefab is missing; cannot render quest skill reward.");
            return;
        }

        if (skillRewardSlotInstance == null)
        {
            skillRewardSlotInstance = Instantiate(skillRewardSlotPrefab, rewardItemsContainer);
            skillRewardSlotInstance.name = "RewardSkill";

            var rect = skillRewardSlotInstance.GetComponent<RectTransform>();
            if (rect != null)
                rect.sizeDelta = new Vector2(80f, 80f);

            var layout = skillRewardSlotInstance.GetComponent<LayoutElement>()
                ?? skillRewardSlotInstance.AddComponent<LayoutElement>();
            layout.minWidth = 80f;
            layout.minHeight = 80f;
            layout.preferredWidth = 80f;
            layout.preferredHeight = 80f;
        }

        var data = reward.Value;
        var skillData = FindRewardSkillData(data.SkillId, data.Name);
        var skillItem = skillRewardSlotInstance.GetComponent<SkillItem>();
        if (skillItem == null)
        {
            Debug.LogError("[MainQuestPanelRuntime] SkillItemPrefab is missing its SkillItem component.", skillRewardSlotInstance);
            skillRewardSlotInstance.SetActive(false);
            return;
        }

        skillRewardSlotInstance.SetActive(true);
        skillRewardSlotInstance.transform.SetSiblingIndex(siblingIndex);
        skillItem.enabled = true;
        skillItem.SetupRewardPreview(skillData, data.Sprite);
        skillItem.enabled = false;

        var button = skillRewardSlotInstance.GetComponent<Button>();
        if (button != null)
            button.interactable = false;
    }

    private SkillData FindRewardSkillData(int? skillId, string skillName)
    {
        BindSkillRewardAssets();
        var skills = skillPanelManager != null ? skillPanelManager.allSkillsInGame : null;
        if (skills == null)
            return null;

        if (skillId.HasValue)
        {
            var byId = Array.Find(skills, skill => skill != null && skill.skillId == skillId.Value);
            if (byId != null)
                return byId;
        }

        return Array.Find(skills, skill =>
            skill != null && string.Equals(skill.name, skillName, StringComparison.OrdinalIgnoreCase));
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
            // KHÔNG dùng ToggleGroup: 3 toggle trong scene đều lưu m_IsOn = 0, còn
            // UpdateFilterHighlights đồng bộ bằng SetIsOnWithoutNotify (không báo cho group)
            // → group với allowSwitchOff = false rơi vào trạng thái "không có toggle nào bật"
            // và ăn luôn click tiếp theo. Trạng thái radio tự quản qua filterToggles là đủ.
            toggle.group = null;

            filterToggles[filterValue] = toggle;
            if (toggle.onValueChanged == null)
                toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                    SetFilter(filterValue);
                else
                    UpdateFilterHighlights(); // click lại tab đang bật → giữ nguyên filter, bật lại toggle
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

    // Quest NotStarted: bước hiện tại là đi gặp NPC, chưa phải mục tiêu Collect/Defeat.
    // Tracker và panel chi tiết dùng chung câu này để không nói hai điều khác nhau về cùng một quest.
    private static string AcceptPromptLine(PlayerQuestResponse quest)
    {
        // Quest kế tiếp có thể ở map khác (claim 20 ở AutumnPumpkin → 21 ở FrozenMountain).
        // Nói "Talk to Roselyn Aurora Queen" khi bà ở map khác là chỉ sai đường: việc cần làm
        // trước là ra Thuyền/cổng để sang map đó — đúng thứ mũi tên đang chỉ vào.
        if (QuestUtils.IsQuestOnDifferentMap(quest))
            return $"Travel to {Safe(quest?.MapName, "the next area")}";

        return $"Talk to {Safe(quest?.QuestGiverName, "Quest Giver")}";
    }

    private static string ObjectiveTextLine(PlayerQuestResponse quest)
    {
        if (quest == null)
            return string.Empty;

        if (QuestUtils.IsStatus(quest, "NotStarted"))
            return AcceptPromptLine(quest);

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
        public RewardViewData(string name, string amount, Sprite sprite, bool isSkill = false, int? skillId = null)
        {
            Name = name;
            Amount = amount;
            Sprite = sprite;
            IsSkill = isSkill;
            SkillId = skillId;
        }

        public string Name { get; }
        public string Amount { get; }
        public Sprite Sprite { get; }
        public bool IsSkill { get; }
        public int? SkillId { get; }
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
