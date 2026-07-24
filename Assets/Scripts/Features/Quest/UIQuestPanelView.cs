using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIQuestPanelView : MonoBehaviour
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
            Debug.LogError("[UIQuestPanelView] questListContent missing.", this);
        if (questSlotPrefab == null)
            Debug.LogError("[UIQuestPanelView] questSlotPrefab missing.", this);
        if (rewardItemsContainer == null)
            Debug.LogError("[UIQuestPanelView] rewardItemsContainer missing.", this);
        if (rewardSlotPrefab == null)
            Debug.LogError("[UIQuestPanelView] rewardSlotPrefab missing.", this);
    }
}
