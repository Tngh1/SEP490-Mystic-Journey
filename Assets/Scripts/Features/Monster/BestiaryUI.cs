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
    [Tooltip("Old UI: combine all stats into 1 slot. Leave empty if using 3 slots Health/DMG/DEF below")]
    [SerializeField] private TextMeshProUGUI detailStatsText;
    [SerializeField] private TextMeshProUGUI detailDescText;

    [Header("Right Panel - Stat Cells")]
    [SerializeField] private TextMeshProUGUI healthValueText;
    [SerializeField] private TextMeshProUGUI dmgValueText;
    [SerializeField] private TextMeshProUGUI defValueText;
    [Tooltip("Badge only enabled for Boss type monsters")]
    [SerializeField] private GameObject bossIcon;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;

    private MonsterSlotUI _selectedSlot;

    private void Awake()
    {
        SetupHoverEffects();
    }

    /// <summary>
    /// Gắn hiệu ứng phóng to khi rê chuột, dùng đúng component UIHoverScaleEffect mà HUD đang dùng.
    /// Slot quái được gắn trong MonsterSlotUI.Init vì chúng sinh ra lúc chạy.
    /// </summary>
    private void SetupHoverEffects()
    {
        if (closeButton == null) return;
        if (closeButton.GetComponent<UIHoverScaleEffect>() == null)
            closeButton.gameObject.AddComponent<UIHoverScaleEffect>();
    }

    private void OnEnable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePanel);
        }

        ClearList();
        ClearDetail();
        LoadCatalogData();
    }

    /// <summary>
    /// Ô detail trong scene có sẵn text mẫu ("Monster Name", "Boss", "1000"...) nên panel vừa mở
    /// đã thấy dữ liệu giả dù chưa chọn con nào. Xoá trắng cho tới khi người chơi bấm 1 slot.
    /// </summary>
    private void ClearDetail()
    {
        if (detailIcon != null)
        {
            detailIcon.sprite = null;
            detailIcon.enabled = false;
        }

        if (detailNameText != null) detailNameText.text = string.Empty;
        if (detailTypeText != null) detailTypeText.text = string.Empty;
        if (detailDescText != null) detailDescText.text = string.Empty;
        if (detailStatsText != null) detailStatsText.text = string.Empty;
        if (bossIcon != null) bossIcon.SetActive(false);

        SetStats(string.Empty, string.Empty, string.Empty);
    }

    private void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveAllListeners();
    }

    private void ClosePanel()
    {
        // Qua UIManager để nó dọn currentPanel và giữ quest tracker hiển thị đúng
        if (UIManager.Instance != null) UIManager.Instance.ClosePanel(gameObject);
        else gameObject.SetActive(false);
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
        }, error => Debug.LogError("Failed to load Bestiary: " + error.Message));
    }

    private void PopulateList(PlayerMonsterCatalogItem[] items)
    {
        ClearList();

        foreach (var item in items)
        {
            // Sinh ra 1 ô quái mới
            GameObject slotObj = Instantiate(monsterSlotPrefab, contentContainer);
            MonsterSlotUI slotUI = slotObj.GetComponent<MonsterSlotUI>();
            if (slotUI == null)
            {
                Debug.LogWarning("[Bestiary] monsterSlotPrefab is missing MonsterSlotUI component.");
                continue;
            }

            // Gắn dữ liệu. Bắt slot vào closure để tô sáng đúng ô được bấm.
            PlayerMonsterCatalogItem captured = item;
            MonsterSlotUI capturedSlot = slotUI;
            slotUI.Init(captured, data => OnSlotClicked(capturedSlot, data), GetIconFor(item.MonsterId));
        }
    }

    private Sprite GetIconFor(int monsterId)
    {
        if (monsterVisualDatabase == null) return null;
        MonsterClientData visualData = monsterVisualDatabase.GetMonsterData(monsterId);
        return visualData != null ? visualData.MonsterIcon : null;
    }

    private void OnSlotClicked(MonsterSlotUI slot, PlayerMonsterCatalogItem data)
    {
        if (_selectedSlot != null && _selectedSlot != slot) _selectedSlot.SetSelected(false);
        _selectedSlot = slot;
        if (slot != null) slot.SetSelected(true);

        ShowDetail(data);
    }

    private void ClearList()
    {
        _selectedSlot = null;
        if (contentContainer == null) return;
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void ShowDetail(PlayerMonsterCatalogItem data)
    {
        Sprite iconSprite = GetIconFor(data.MonsterId);

        if (data.IsDiscovered)
        {
            if (detailIcon != null)
            {
                detailIcon.color = Color.white;
                if (iconSprite != null) detailIcon.sprite = iconSprite;
                detailIcon.enabled = detailIcon.sprite != null;
            }

            if (detailNameText != null) detailNameText.text = data.Name;

            string typeColor = "#FFFFFF";
            if (data.Type == "Boss") typeColor = "#FF0000";
            else if (data.Type == "Elite") typeColor = "#FFD700";

            if (detailTypeText != null) detailTypeText.text = $"<color={typeColor}>{data.Type}</color>";
            if (bossIcon != null) bossIcon.SetActive(data.Type == "Boss");

            if (detailDescText != null) detailDescText.text = data.Description;

            SetStats(data.MaxHp.ToString(), data.Atk.ToString(), data.Def.ToString());

            // Chỉ dùng khi giao diện còn ô stats gộp
            if (detailStatsText != null)
            {
                detailStatsText.text = $"Level: {data.Level}\n" +
                                       $"HP: {data.MaxHp}\n" +
                                       $"Atk: {data.Atk} | Def: {data.Def}\n" +
                                       $"Rewards:\n" +
                                       $"- {data.ExperienceReward} XP\n" +
                                       $"- {data.GoldReward} Gold\n\n" +
                                       $"Defeated: {data.TimesDefeated}";
            }
        }
        else
        {
            if (detailIcon != null)
            {
                detailIcon.color = Color.black;
                if (iconSprite != null) detailIcon.sprite = iconSprite;
                detailIcon.enabled = detailIcon.sprite != null;
            }

            if (detailNameText != null) detailNameText.text = "???";
            if (detailTypeText != null) detailTypeText.text = "Unknown";
            if (bossIcon != null) bossIcon.SetActive(false);
            if (detailDescText != null)
                detailDescText.text = "You have not encountered this creature yet. Explore the world to learn more.";

            SetStats("???", "???", "???");

            if (detailStatsText != null)
                detailStatsText.text = "Level: ???\nHP: ???\nAtk: ??? | Def: ???\nRewards: ???";
        }
    }

    private void SetStats(string hp, string atk, string def)
    {
        if (healthValueText != null) healthValueText.text = hp;
        if (dmgValueText != null) dmgValueText.text = atk;
        if (defValueText != null) defValueText.text = def;
    }
}