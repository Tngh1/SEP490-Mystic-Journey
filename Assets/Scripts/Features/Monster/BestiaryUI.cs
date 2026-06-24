using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BestiaryUI : MonoBehaviour
{
    [Header("Left Panel - List")]
    [SerializeField] private Transform contentContainer;
    [SerializeField] private GameObject monsterSlotPrefab;
    [SerializeField] private MonsterDatabaseSO monsterVisualDatabase;

    [Header("Right Panel - Details")]
    [SerializeField] private Image detailIcon;
    [SerializeField] private TextMeshProUGUI detailNameText;
    [SerializeField] private TextMeshProUGUI detailTypeText;
    [SerializeField] private TextMeshProUGUI detailStatsText;
    [SerializeField] private TextMeshProUGUI detailDescText;

    private void OnEnable()
    {
        ClearList();
        LoadCatalogData();
    }

    private void LoadCatalogData()
    {
        // Gọi thẳng API, bỏ qua MonsterManager để tránh lỗi Null
        MonsterApi.Instance.GetCatalogForPlayer(1, 50, response =>
        {
            if (response != null && response.Items != null)
            {
                PopulateList(response.Items);
            }
        }, error => Debug.LogError("Lỗi tải Bestiary: " + error.Message));
    }

    private void PopulateList(PlayerMonsterCatalogItem[] items)
    {
        ClearList();

        foreach (var item in items)
        {
            // Sinh ra 1 ô quái mới
            GameObject slotObj = Instantiate(monsterSlotPrefab, contentContainer);
            MonsterSlotUI slotUI = slotObj.GetComponent<MonsterSlotUI>();

            // Lấy hình ảnh từ Database
            Sprite iconSprite = null;
            if (monsterVisualDatabase != null)
            {
                MonsterClientData visualData = monsterVisualDatabase.GetMonsterData(item.MonsterId);
                if (visualData != null) iconSprite = visualData.MonsterIcon;
            }

            // Gắn dữ liệu
            slotUI.Init(item, ShowDetail, iconSprite);
        }
    }

    private void ClearList()
    {
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void ShowDetail(PlayerMonsterCatalogItem data)
    {
        Sprite iconSprite = null;
        if (monsterVisualDatabase != null)
        {
            MonsterClientData visualData = monsterVisualDatabase.GetMonsterData(data.MonsterId);
            if (visualData != null) iconSprite = visualData.MonsterIcon;
        }

        if (data.IsDiscovered)
        {
            detailIcon.color = Color.white;
            if (iconSprite != null) detailIcon.sprite = iconSprite;

            detailNameText.text = data.Name;

            string typeColor = "#FFFFFF";
            if (data.Type == "Boss") typeColor = "#FF0000";
            else if (data.Type == "Elite") typeColor = "#FFD700";

            detailTypeText.text = $"Type: <color={typeColor}>{data.Type}</color>";

            detailDescText.text = data.Description;
            detailStatsText.text = $"Level: {data.Level}\n" +
                                   $"HP: {data.MaxHp}\n" +
                                   $"Atk: {data.Atk} | Def: {data.Def}\n" +
                                   $"Phần thưởng:\n" +
                                   $"- {data.ExperienceReward} XP\n" +
                                   $"- {data.GoldReward} Gold\n\n" +
                                   $"Đã tiêu diệt: {data.TimesDefeated}";
        }
        else
        {
            detailIcon.color = Color.black;
            if (iconSprite != null) detailIcon.sprite = iconSprite;

            detailNameText.text = "???";
            detailTypeText.text = "Type: Unknown";
            detailDescText.text = "Bạn chưa từng bắt gặp sinh vật này. Hãy khám phá thế giới để tìm hiểu thêm.";
            detailStatsText.text = "Level: ???\nHP: ???\nAtk: ??? | Def: ???\nPhần thưởng: ???";
        }
    }
}