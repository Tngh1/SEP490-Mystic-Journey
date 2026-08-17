using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Executes core business logic for mono behaviour.
public class BestiaryUIManager : MonoBehaviour
{
    [Header("Left Panel - List")]
    [SerializeField] private Transform contentContainer;
    [SerializeField] private GameObject monsterSlotPrefab;
    [SerializeField] private MonsterDatabaseSO monsterVisualDatabase;
    [SerializeField] private TextMeshProUGUI discoveredCountText;

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

    // Initializes internal component caches and dependencies for BestiaryUIManager upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        EnsureDiscoveredCountText();
        SetupHoverEffects();
    }

    // Executes core business logic for ensure discovered count text.
    private void EnsureDiscoveredCountText()
    {
        if (discoveredCountText != null) return;

        Transform discoveredNumber = transform.Find("Deco/Deco2/DiscoveredNumber");
        if (discoveredNumber != null)
            discoveredCountText = discoveredNumber.GetComponent<TextMeshProUGUI>();
    }

    // Executes core business logic for setup hover effects.
    private void SetupHoverEffects()
    {
        if (closeButton == null) return;
        if (closeButton.GetComponent<UIHoverScaleEffect>() == null)
            closeButton.gameObject.AddComponent<UIHoverScaleEffect>();
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePanel);
        }

        ClearList();
        ClearDetail();
        if (discoveredCountText != null) discoveredCountText.text = "--";
        LoadCatalogData();
    }

    // Executes core business logic for clear detail.
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

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveAllListeners();
    }

    // Executes core business logic for close panel.
    private void ClosePanel()
    {
        if (UIManager.Instance != null) UIManager.Instance.ClosePanel(gameObject);
        else gameObject.SetActive(false);
    }

    // Executes core business logic for load catalog data.
    private void LoadCatalogData()
    {
        MonsterApi.Instance.GetCatalogForPlayer(1, 1000, response =>
        {
            if (response != null && response.Items != null)
            {
                int discoveredCount = 0;
                foreach (var item in response.Items)
                {
                    if (item.IsDiscovered) discoveredCount++;
                }
                if (discoveredCountText != null) discoveredCountText.text = discoveredCount.ToString();
                PopulateList(response.Items);
            }
        }, error => Debug.LogError("Failed to load Bestiary: " + error.Message));
    }

    // Executes core business logic for populate list.
    private void PopulateList(PlayerMonsterCatalogItem[] items)
    {
        ClearList();

        foreach (var item in items)
        {
            GameObject slotObj = Instantiate(monsterSlotPrefab, contentContainer);
            MonsterSlotUI slotUI = slotObj.GetComponent<MonsterSlotUI>();
            if (slotUI == null)
            {
                Debug.LogWarning("[Bestiary] monsterSlotPrefab is missing MonsterSlotUI component.");
                continue;
            }

            PlayerMonsterCatalogItem captured = item;
            MonsterSlotUI capturedSlot = slotUI;
            slotUI.Init(captured, data => OnSlotClicked(capturedSlot, data), GetIconFor(item.MonsterId));
        }
    }

    // Executes core business logic for get icon for.
    private Sprite GetIconFor(int monsterId)
    {
        if (monsterVisualDatabase == null) return null;
        MonsterClientData visualData = monsterVisualDatabase.GetMonsterData(monsterId);
        return visualData != null ? visualData.MonsterIcon : null;
    }

    // Executes core business logic for on slot clicked.
    private void OnSlotClicked(MonsterSlotUI slot, PlayerMonsterCatalogItem data)
    {
        if (_selectedSlot != null && _selectedSlot != slot) _selectedSlot.SetSelected(false);
        _selectedSlot = slot;
        if (slot != null) slot.SetSelected(true);

        ShowDetail(data);
    }

    // Executes core business logic for clear list.
    private void ClearList()
    {
        _selectedSlot = null;
        if (contentContainer == null) return;
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }
    }

    // Executes core business logic for show detail.
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

    // Executes core business logic for set stats.
    private void SetStats(string hp, string atk, string def)
    {
        if (healthValueText != null) healthValueText.text = hp;
        if (dmgValueText != null) dmgValueText.text = atk;
        if (defValueText != null) defValueText.text = def;
    }
}
