using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MysticJourney.Core.Services;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Executes core business logic for mono behaviour.
public class SkillUIManager : MonoBehaviour
{
    // Initializes internal component caches and dependencies for SkillUIManager upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        ConfigureSkillListLayout();
    }

    // Executes core business logic for configure skill list layout.
    private void ConfigureSkillListLayout()
    {
        if (contentArea == null)
        {
            contentArea = transform.Find("Background/bg/SkillList/Viewport/Content");
        }

        if (skillScrollRect == null && contentArea != null)
        {
            skillScrollRect = contentArea.GetComponentInParent<ScrollRect>(true);
        }

        if (skillScrollRect != null)
        {
            skillScrollRect.content = contentArea as RectTransform;
            skillScrollRect.horizontal = false;
            skillScrollRect.vertical = true;
            skillScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        var contentRect = contentArea as RectTransform;
        if (contentRect == null) return;

        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        var grid = contentArea.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
        }

        var fitter = contentArea.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = contentArea.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

// Executes core business logic for finalize skill list layout.
private void FinalizeSkillListLayout()
    {
        var contentRect = contentArea as RectTransform;
        if (contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        Canvas.ForceUpdateCanvases();
        if (skillScrollRect != null)
        {
            skillScrollRect.StopMovement();
            skillScrollRect.verticalNormalizedPosition = 1f;

            if (contentRect != null)
            {
                contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, 0f);
            }
        }
    }

    [Header("UI References")]
    public GameObject skillItemPrefab;
    public Transform contentArea;
    [SerializeField] private ScrollRect skillScrollRect;

    [Header("Master Data")]
    public SkillData[] allSkillsInGame;

    [Header("Slots")]
    public SkillSlot[] skillSlots;

    [Header("Stone Counter UI")]
    public TMPro.TextMeshProUGUI stoneCountText;

#if UNITY_EDITOR
    [ContextMenu("Load All Skills In Project")]
    // Executes core business logic for load all skills in project.
    public void LoadAllSkillsInProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:SkillData");
        var list = new List<SkillData>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if (skill != null && !list.Contains(skill))
            {
                list.Add(skill);
            }
        }

        list.Sort((a, b) => a.skillId.CompareTo(b.skillId));
        allSkillsInGame = list.ToArray();

        EditorUtility.SetDirty(this);
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
        Debug.Log($"<color=green>[SkillUIManager] Đã tự động nạp thành công {allSkillsInGame.Length} SkillData vào allSkillsInGame!</color>");
    }
#endif

    // Binds HUD skill slots, refreshes available skill loadouts, and queries upgrade stones.
    private void OnEnable()
    {
        ConfigureSkillListLayout(); // Setup grid layout parameters
        var allSlots = FindObjectsByType<SkillSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var hudSlots = new List<SkillSlot>();

        foreach (var s in allSlots)
        {
            if (s.transform.IsChildOf(this.transform))
            {
                s.gameObject.SetActive(false); // Hide template slots
            }
            else if (s.name.Contains("Slot"))
            {
                hudSlots.Add(s); // Collect active HUD action slots
            }
        }

        hudSlots.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x)); // Order from slot 1 to 3 (left to right)

        while (hudSlots.Count > 3)
        {
            hudSlots.RemoveAt(hudSlots.Count - 1); // Limit to 3 skill slots
        }

        skillSlots = hudSlots.ToArray();

        for (int i = 0; i < skillSlots.Length; i++)
        {
            if (skillSlots[i] != null) skillSlots[i].slotIndex = i; // Assign index 0, 1, 2
        }

        RefreshSkillList(); // Populate skill items in inventory grid
    }

    // Queries player inventory to count Skill Upgrade Stones (ItemId 22).
    public void RefreshStoneCount()
    {
        EnsureStoneCountUI();
        var inventoryApi = InventoryApi.Instance;
        if (inventoryApi == null)
        {
            Debug.LogWarning("[SkillUIManager] Inventory API is unavailable while the application is closing.");
            return;
        }

        inventoryApi.GetInventory(
            onSuccess: (summary) =>
            {
                int stones = 0;
                if (summary?.BagItems != null)
                {
                    foreach (var item in summary.BagItems)
                    {
                        if (item != null && !string.IsNullOrEmpty(item.ItemName) &&
                            (item.ItemId == 22 || item.ItemName.Equals("Skill Upgrade Stone", System.StringComparison.OrdinalIgnoreCase) || (item.ItemName.Contains("Skill Upgrade") && item.ItemName.Contains("Stone"))))
                        {
                            stones += item.Quantity; // Aggregate upgrade material count
                        }
                    }
                }
                if (stoneCountText != null)
                {
                    stoneCountText.text = stones.ToString(); // Display total upgrade stones in HUD header
                }
            },
            onError: (err) =>
            {
                Debug.LogWarning("[SkillUIManager] Could not load stone count: " + err.Message);
            }
        );
    }

    // Executes core business logic for ensure stone count ui.
    private void EnsureStoneCountUI()
    {
        if (stoneCountText == null)
        {
            stoneCountText = transform.Find("Header/Stone/NumberStone")?.GetComponent<TMPro.TextMeshProUGUI>();
        }
    }

    // Executes core business logic for refresh skill list.
    public void RefreshSkillList()
    {
        ConfigureSkillListLayout();
        RefreshStoneCount();

        PopulateUI(null);

        var skillApi = SkillApi.Instance;
        if (skillApi == null)
        {
            Debug.LogWarning("[SkillUIManager] Skill API is unavailable while the application is closing.");
            return;
        }

        skillApi.GetMySkills(
            onSuccess: (response) =>
            {
                PopulateUI(response != null ? response.Skills : null);
            },
            onError: (error) =>
            {
                Debug.LogError($"[UI] Lỗi tải kỹ năng sở hữu: {error.Message}");
            }
        );
    }

    // Executes core business logic for populate ui.
    private void PopulateUI(List<PlayerSkillResponse> playerSkills)
    {
        if (skillItemPrefab == null || contentArea == null) return;

        for (int i = contentArea.childCount - 1; i >= 0; i--)
        {
            Transform child = contentArea.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        // Supported player classes: Knight, Archer, or Mage; the class selects base stats, compatible skills, skins, and combat scaling.
        string playerClass = GameStateService.Instance?.PlayerClass ?? "";

        var sortedSkillList = new List<(SkillData visual, PlayerSkillResponse server)>();
        HashSet<int> processedSkillIds = new HashSet<int>();
        HashSet<Sprite> processedIcons = new HashSet<Sprite>();

        foreach (var data in allSkillsInGame)
        {
            if (data == null || data.skillIcon == null) continue;
            if (processedSkillIds.Contains(data.skillId)) continue;

            processedSkillIds.Add(data.skillId);

            bool isAllClass = string.IsNullOrWhiteSpace(data.classRequirement) || data.classRequirement.Equals("All", System.StringComparison.OrdinalIgnoreCase);
            bool isMyClass = string.IsNullOrWhiteSpace(playerClass) ||
                             data.classRequirement.Equals(playerClass, System.StringComparison.OrdinalIgnoreCase);

            if (!isAllClass && !isMyClass)
                continue;

            PlayerSkillResponse owned = null;
            if (playerSkills != null)
            {
                owned = playerSkills.Find(ps => ps.SkillId == data.skillId);
            }
            sortedSkillList.Add((data, owned));
        }

        sortedSkillList.Sort((a, b) =>
        {
            bool aUnlocked = a.server != null;
            bool bUnlocked = b.server != null;

            if (aUnlocked && !bUnlocked) return -1;
            if (!aUnlocked && bUnlocked) return 1;
            return a.visual.skillId.CompareTo(b.visual.skillId);
        });

        foreach (var item in sortedSkillList)
        {
            GameObject newSkillObj = Instantiate(skillItemPrefab, contentArea);
            SkillItem itemScript = newSkillObj.GetComponent<SkillItem>();
            if (itemScript != null) itemScript.Setup(item.visual, item.server);
        }

        FinalizeSkillListLayout();


        if (skillSlots != null)
        {
            foreach (var s in skillSlots)
            {
                if (s != null && s.equippedIcon != null)
                {
                    s.equippedIcon.sprite = null;
                    s.equippedIcon.color = new Color(1, 1, 1, 0);
                }
            }

            foreach (var s in skillSlots)
            {
                if (s != null) s.RefreshLockState();
            }

            if (playerSkills != null)
            {
                foreach (var ps in playerSkills)
                {
                    if (ps.EquippedSlot.HasValue && ps.EquippedSlot.Value >= 0 && ps.EquippedSlot.Value < skillSlots.Length)
                    {
                        // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
                        var slot = skillSlots[ps.EquippedSlot.Value];
                        var visual = System.Array.Find(allSkillsInGame, d => d.skillId == ps.SkillId);
                        if (visual != null && slot != null && slot.equippedIcon != null)
                        {
                            slot.equippedIcon.sprite = visual.skillIcon;
                            slot.equippedIcon.color = Color.white;

                            SkillSlot.BroadcastSkillEquipped(ps.EquippedSlot.Value, visual, ps);
                        }
                    }
                }
            }
        }
    }
}
