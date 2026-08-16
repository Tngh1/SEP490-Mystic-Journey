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

public class SkillUIManager : MonoBehaviour
{
    private void Awake()
    {
        ConfigureSkillListLayout();
    }

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
    // Kéo toàn bộ file SkillData từ thư mục ScriptableObjects vào mảng này
    public SkillData[] allSkillsInGame;

    [Header("Slots")]
    public SkillSlot[] skillSlots; // assign 3 slots in inspector

    [Header("Stone Counter UI")]
    public TMPro.TextMeshProUGUI stoneCountText;

#if UNITY_EDITOR
    [ContextMenu("Load All Skills In Project")]
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

    private void OnEnable()
    {
        ConfigureSkillListLayout();
        // --- BỎ CÁC Ô TRANG BỊ TRONG LIST VÀ CHỈ DÙNG HUD ---
        var allSlots = FindObjectsByType<SkillSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var hudSlots = new List<SkillSlot>();

        foreach (var s in allSlots)
        {
            if (s.transform.IsChildOf(this.transform))
            {
                // Ẩn các slot cũ (nếu còn) bên trong Panel
                s.gameObject.SetActive(false);
            }
            else if (s.name.Contains("Slot")) // Chỉ lấy các ô có chữ "Slot" (bỏ qua các nút bấm khác)
            {
                // Các slot nằm ngoài (tức là ở HUD)
                hudSlots.Add(s);
            }
        }

        // Sắp xếp các ô HUD theo toạ độ X (trái sang phải) để gán Index chuẩn
        hudSlots.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        
        // Giới hạn đúng 3 ô để không gửi slotIndex > 2 lên server gây lỗi "Invalid slot index"
        while (hudSlots.Count > 3)
        {
            hudSlots.RemoveAt(hudSlots.Count - 1);
        }

        skillSlots = hudSlots.ToArray();

        for (int i = 0; i < skillSlots.Length; i++)
        {
            if (skillSlots[i] != null) skillSlots[i].slotIndex = i;
        }
        
        // Tự động gọi API mỗi khi Panel này được SetActive(true)
        RefreshSkillList();
    }

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
                            stones += item.Quantity;
                        }
                    }
                }
                if (stoneCountText != null)
                {
                    stoneCountText.text = stones.ToString();
                }
            },
            onError: (err) =>
            {
                Debug.LogWarning("[SkillUIManager] Could not load stone count: " + err.Message);
            }
        );
    }

    private void EnsureStoneCountUI()
    {
        if (stoneCountText == null)
        {
            stoneCountText = transform.Find("Header/Stone/NumberStone")?.GetComponent<TMPro.TextMeshProUGUI>();
        }
    }

    public void RefreshSkillList()
    {
        ConfigureSkillListLayout();
        RefreshStoneCount();

        // The catalog is local game data. Render it first so an empty owned-skill
        // response (or a temporary API failure) never leaves the panel blank.
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

    private void PopulateUI(List<PlayerSkillResponse> playerSkills)
    {
        if (skillItemPrefab == null || contentArea == null) return;

        // 1. DỌN DẸP UI TRIỆT ĐỂ (Chống bug nhân đôi hình)
        for (int i = contentArea.childCount - 1; i >= 0; i--)
        {
            Transform child = contentArea.GetChild(i);
            child.SetParent(null); // Tách Object ra khỏi danh sách ngay lập tức để Layout group update
            Destroy(child.gameObject);
        }

        // 2. LẤY CLASS HIỆN TẠI CỦA NGƯỜI CHƠI
        string playerClass = GameStateService.Instance?.PlayerClass ?? "";

        var sortedSkillList = new List<(SkillData visual, PlayerSkillResponse server)>();
        HashSet<int> processedSkillIds = new HashSet<int>();
        HashSet<Sprite> processedIcons = new HashSet<Sprite>();

        foreach (var data in allSkillsInGame)
        {
            if (data == null || data.skillIcon == null) continue;
            if (processedSkillIds.Contains(data.skillId)) continue;
            
            processedSkillIds.Add(data.skillId);

            // 3. TÍNH NĂNG LỌC: Bỏ qua các kỹ năng không thuộc Class của mình (hoặc không phải All)
            bool isAllClass = string.IsNullOrWhiteSpace(data.classRequirement) || data.classRequirement.Equals("All", System.StringComparison.OrdinalIgnoreCase);
            bool isMyClass = string.IsNullOrWhiteSpace(playerClass) ||
                             data.classRequirement.Equals(playerClass, System.StringComparison.OrdinalIgnoreCase);

            if (!isAllClass && !isMyClass)
                continue; // ⬅️ Nếu không hợp hệ, lập tức bỏ qua, không hiển thị lên UI

            PlayerSkillResponse owned = null;
            if (playerSkills != null)
            {
                owned = playerSkills.Find(ps => ps.SkillId == data.skillId);
            }
            sortedSkillList.Add((data, owned));
        }

        // 4. THỰC HIỆN SẮP XẾP DANH SÁCH (Đã lọc)
        sortedSkillList.Sort((a, b) =>
        {
            bool aUnlocked = a.server != null;
            bool bUnlocked = b.server != null;

            if (aUnlocked && !bUnlocked) return -1;
            if (!aUnlocked && bUnlocked) return 1;
            return a.visual.skillId.CompareTo(b.visual.skillId);
        });

        // 5. Tiến hành vẽ giao diện 
        foreach (var item in sortedSkillList)
        {
            GameObject newSkillObj = Instantiate(skillItemPrefab, contentArea);
            SkillItem itemScript = newSkillObj.GetComponent<SkillItem>();
            if (itemScript != null) itemScript.Setup(item.visual, item.server);
        }

        FinalizeSkillListLayout();


        // =========================================================
        // (Phần code xử lý skillSlots của bạn ở bên dưới GIỮ NGUYÊN)
        if (skillSlots != null)
        {
            foreach (var s in skillSlots)
            {
                if (s != null && s.equippedIcon != null)
                {
                    s.equippedIcon.sprite = null;
                    s.equippedIcon.color = new Color(1, 1, 1, 0); // hide
                }
            }

            // RefreshLockState làm đúng việc này (ổ khóa theo level) và còn cập nhật
            // nhãn "Lv 5" / "Empty" — vòng lặp tự set lockImage ở đây sẽ để lại nhãn cũ.
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
                        var slot = skillSlots[ps.EquippedSlot.Value];
                        var visual = System.Array.Find(allSkillsInGame, d => d.skillId == ps.SkillId);
                        if (visual != null && slot != null && slot.equippedIcon != null)
                        {
                            slot.equippedIcon.sprite = visual.skillIcon;
                            slot.equippedIcon.color = Color.white;

                            // 👇 THÊM DÒNG NÀY: Phát loa để nạp dữ liệu cho HUD & Nhân vật lúc mới vào game
                            SkillSlot.BroadcastSkillEquipped(ps.EquippedSlot.Value, visual, ps);
                        }
                    }
                }
            }
        }
    }
}
