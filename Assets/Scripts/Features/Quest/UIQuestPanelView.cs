using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIQuestPanelView : MonoBehaviour
{
    [Header("List Prefabs")]
    [SerializeField] private Transform questListContent;
    [SerializeField] private UIQuestListItem questSlotTemplate;
    [SerializeField] private Transform rewardListContent;
    [SerializeField] private UIQuestRewardSlot rewardSlotTemplate;

    [Header("Detail Text Slots")]
    [SerializeField] private TMP_Text questTitleTMP;
    [SerializeField] private Text questTitleText;
    [SerializeField] private TMP_Text questTypeTMP;
    [SerializeField] private Text questTypeText;
    [SerializeField] private TMP_Text objectiveTMP;
    [SerializeField] private Text objectiveText;
    [SerializeField] private TMP_Text progressTMP;
    [SerializeField] private Text progressText;
    [SerializeField] private TMP_Text descriptionTMP;
    [SerializeField] private Text descriptionText;
    [SerializeField] private TMP_Text questGiverTMP;
    [SerializeField] private Text questGiverText;
    [SerializeField] private TMP_Text rewardsTMP;
    [SerializeField] private Text rewardsText;

    [Header("Action Buttons")]
    [SerializeField] private Button acceptQuestButton;
    [SerializeField] private Button completeQuestButton;
    [SerializeField] private Button declineQuestButton;
    [SerializeField] private Button claimQuestButton;
    [SerializeField] private Button claimedButton;
    [SerializeField] private Button trackToggleButton;
    [SerializeField] private Button primaryActionButton;
    [SerializeField] private Button closeButton;

    public Transform QuestListContent => questListContent;
    public UIQuestListItem QuestSlotTemplate => questSlotTemplate;
    public Transform RewardListContent => rewardListContent;
    public UIQuestRewardSlot RewardSlotTemplate => rewardSlotTemplate;

    public TMP_Text QuestTitleTMP => questTitleTMP;
    public Text QuestTitleText => questTitleText;
    public TMP_Text QuestTypeTMP => questTypeTMP;
    public Text QuestTypeText => questTypeText;
    public TMP_Text ObjectiveTMP => objectiveTMP;
    public Text ObjectiveText => objectiveText;
    public TMP_Text ProgressTMP => progressTMP;
    public Text ProgressText => progressText;
    public TMP_Text DescriptionTMP => descriptionTMP;
    public Text DescriptionText => descriptionText;
    public TMP_Text QuestGiverTMP => questGiverTMP;
    public Text QuestGiverText => questGiverText;
    public TMP_Text RewardsTMP => rewardsTMP;
    public Text RewardsText => rewardsText;

    public Button AcceptQuestButton => acceptQuestButton;
    public Button CompleteQuestButton => completeQuestButton;
    public Button DeclineQuestButton => declineQuestButton;
    public Button ClaimQuestButton => claimQuestButton;
    public Button ClaimedButton => claimedButton;
    public Button TrackToggleButton => trackToggleButton;
    public Button PrimaryActionButton => primaryActionButton;
    public Button CloseButton => closeButton;

    private void Awake()
    {
        ValidateReferences();
    }

    private void OnValidate()
    {
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (questListContent == null)
            Debug.LogError("[UIQuestPanelView] questListContent missing.", this);
        if (questSlotTemplate == null)
            Debug.LogError("[UIQuestPanelView] questSlotTemplate prefab missing.", this);
        if (rewardListContent == null)
            Debug.LogError("[UIQuestPanelView] rewardListContent missing.", this);
        if (rewardSlotTemplate == null)
            Debug.LogError("[UIQuestPanelView] rewardSlotTemplate prefab missing.", this);
    }
}
