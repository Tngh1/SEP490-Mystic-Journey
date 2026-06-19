using UnityEngine;
using UnityEngine.UI;

public class UIQuestPanelView : MonoBehaviour
{
    [SerializeField] private Transform questListContent;
    [SerializeField] private UIQuestListItem questSlotTemplate;
    [SerializeField] private Transform rewardListContent;
    [SerializeField] private UIQuestRewardSlot rewardSlotTemplate;

    public Transform QuestListContent
    {
        get
        {
            Bind();
            return questListContent;
        }
    }

    public UIQuestListItem QuestSlotTemplate
    {
        get
        {
            Bind();
            return questSlotTemplate;
        }
    }

    public Transform RewardListContent
    {
        get
        {
            Bind();
            return rewardListContent;
        }
    }

    public UIQuestRewardSlot RewardSlotTemplate
    {
        get
        {
            Bind();
            return rewardSlotTemplate;
        }
    }

    private void Awake()
    {
        Bind();
    }

    private void OnValidate()
    {
        Bind();
    }

    public GameObject Find(string objectName)
    {
        return FindDescendant(transform, objectName);
    }

    private void Bind()
    {
        if (questListContent == null)
        {
            var listPanel = FindDescendant(transform, "QuestListPanel")?.transform;
            var leftSection = FindDescendant(transform, "LeftSection")?.transform;
            var root = listPanel != null ? listPanel : leftSection;
            if (root != null)
            {
                var scroll = root.GetComponentInChildren<ScrollRect>(true);
                questListContent = scroll != null && scroll.content != null
                    ? scroll.content
                    : (FindDescendant(root, "Content")?.transform ?? root);
            }
        }

        if (questSlotTemplate == null)
        {
            var namedSlot = FindDescendant(transform, "New_QuestSlot") ?? FindDescendant(transform, "QuestSlot");
            if (namedSlot != null)
                questSlotTemplate = namedSlot.GetComponent<UIQuestListItem>() ?? namedSlot.AddComponent<UIQuestListItem>();
        }

        if (rewardListContent == null)
        {
            // Yuuko update: Ưu tiên tìm ReclaimList trước
            var reclaimList = FindDescendant(transform, "ReclaimList")?.transform ??
                              FindDescendant(transform, "RewardsContent")?.transform ??
                              FindDescendant(transform, "RewardList")?.transform ??
                              FindDescendant(transform, "Rewards")?.transform;

            if (reclaimList != null)
            {
                // Tự động tìm ScrollRect và lấy Content bên trong ReclaimList
                var scroll = reclaimList.GetComponentInChildren<ScrollRect>(true);
                rewardListContent = scroll != null && scroll.content != null
                    ? scroll.content
                    : (FindDescendant(reclaimList, "Content")?.transform ?? reclaimList);
            }
        }

        if (rewardSlotTemplate == null && rewardListContent != null)
            rewardSlotTemplate = rewardListContent.GetComponentInChildren<UIQuestRewardSlot>(true);
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
}