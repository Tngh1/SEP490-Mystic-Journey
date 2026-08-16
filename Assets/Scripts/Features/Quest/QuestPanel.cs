using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    public Transform QuestListContent => questListContent;
    public GameObject QuestSlotPrefab => questSlotPrefab;

    public TMP_Text QuestTitleTMP => questTitleTMP;
    public TMP_Text ObjectiveTMP => objectiveTMP;
    public TMP_Text DescriptionTMP => descriptionTMP;

    public GameObject DetailCompleteIcon => detailCompleteIcon;

    public Image QuestTypeImage => questTypeImage;
    public Sprite KillTypeSprite => killTypeSprite;
    public Sprite CollectTypeSprite => collectTypeSprite;
    public Sprite TalkTypeSprite => talkTypeSprite;
    public Sprite ExploreTypeSprite => exploreTypeSprite;

    public GameObject RewardsContainer => rewardsContainer;
    public Transform RewardItemsContainer => rewardItemsContainer;
    public GameObject RewardSlotPrefab => rewardSlotPrefab;
    public GameObject SkillRewardSlotPrefab => skillRewardSlotPrefab;

    public Button TrackQuestButton => trackQuestButton;
    public Button CloseButton => closeButton;

    public Sprite TrackActiveSprite => trackActiveSprite;
    public Sprite TrackInactiveSprite => trackInactiveSprite;

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
