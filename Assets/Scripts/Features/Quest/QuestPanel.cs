using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Executes mono behaviour operation.
public class QuestPanel : MonoBehaviour
{
    [Header("Quest List (Left)")]
    [SerializeField] private Transform questListContent;
    [SerializeField] private GameObject questSlotPrefab;

    [Header("Quest Detail (Right)")]
    [SerializeField] private TMP_Text questTitleTMP;
    [SerializeField] private TMP_Text objectiveTMP;
    [SerializeField] private TMP_Text descriptionTMP;
    [SerializeField] private GameObject detailCompleteIcon;

    [Header("Quest Type Icon (Right Detail)")]
    [SerializeField] private Image questTypeImage;
    [SerializeField] private Sprite killTypeSprite;
    [SerializeField] private Sprite collectTypeSprite;
    [SerializeField] private Sprite talkTypeSprite;
    [SerializeField] private Sprite exploreTypeSprite;

    [Header("Reclaim / Rewards (Right)")]
    [SerializeField] private GameObject rewardsContainer;
    [SerializeField] private Transform rewardItemsContainer;
    [SerializeField] private GameObject rewardSlotPrefab;
    [SerializeField] private GameObject skillRewardSlotPrefab;

    [Header("Track Button")]
    [SerializeField] private Button trackQuestButton;
    [SerializeField] private Sprite trackActiveSprite;
    [SerializeField] private Sprite trackInactiveSprite;

    [Header("Main Buttons")]
    [SerializeField] private Button closeButton;

    // Executes quest list content operation.
    public Transform QuestListContent => questListContent;
    // Executes quest slot prefab operation.
    public GameObject QuestSlotPrefab => questSlotPrefab;

    // Executes quest title tmp operation.
    public TMP_Text QuestTitleTMP => questTitleTMP;
    // Executes objective tmp operation.
    public TMP_Text ObjectiveTMP => objectiveTMP;
    // Executes description tmp operation.
    public TMP_Text DescriptionTMP => descriptionTMP;

    // Executes detail complete icon operation.
    public GameObject DetailCompleteIcon => detailCompleteIcon;

    // Executes quest type image operation.
    public Image QuestTypeImage => questTypeImage;
    // Executes kill type sprite operation.
    public Sprite KillTypeSprite => killTypeSprite;
    // Executes collect type sprite operation.
    public Sprite CollectTypeSprite => collectTypeSprite;
    // Executes talk type sprite operation.
    public Sprite TalkTypeSprite => talkTypeSprite;
    // Executes explore type sprite operation.
    public Sprite ExploreTypeSprite => exploreTypeSprite;

    // Executes rewards container operation.
    public GameObject RewardsContainer => rewardsContainer;
    // Executes reward items container operation.
    public Transform RewardItemsContainer => rewardItemsContainer;
    // Executes reward slot prefab operation.
    public GameObject RewardSlotPrefab => rewardSlotPrefab;
    // Executes skill reward slot prefab operation.
    public GameObject SkillRewardSlotPrefab => skillRewardSlotPrefab;

    // Executes track quest button operation.
    public Button TrackQuestButton => trackQuestButton;
    // Executes close button operation.
    public Button CloseButton => closeButton;

    // Executes track active sprite operation.
    public Sprite TrackActiveSprite => trackActiveSprite;
    // Executes track inactive sprite operation.
    public Sprite TrackInactiveSprite => trackInactiveSprite;

    // Initializes internal component caches and dependencies for QuestPanel upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        ValidateReferences();
    }

    // Executes on validate operation.
    private void OnValidate()
    {
        ValidateReferences();
    }

    // Executes validate references operation.
    private void ValidateReferences()
    {
        if (questListContent == null)
            Debug.LogError("[QuestPanel] questListContent missing.", this);
        if (questSlotPrefab == null)
            Debug.LogError("[QuestPanel] questSlotPrefab missing.", this);
        if (rewardItemsContainer == null)
            Debug.LogError("[QuestPanel] rewardItemsContainer missing.", this);
        if (rewardSlotPrefab == null)
            Debug.LogError("[QuestPanel] rewardSlotPrefab missing.", this);
        if (skillRewardSlotPrefab == null)
            Debug.LogError("[QuestPanel] skillRewardSlotPrefab missing.", this);
    }
}
