using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

// =============================================================================
// InventoryManager – UC 20 (Manage Inventory) – Controller chính
//
// Trách nhiệm:
//   • Load inventory từ API (UC 20.1 View Inventory)
//   • Hiển thị danh sách item lên UIInventory:
//       Tab "Items" – InventoryItems (Weapon, Armor, Consumable…)
//       Tab "Skins" – PlayerSkins (từ bảng PlayerSkins, có PlayerSkinId riêng)
//   • Mở UIItemDetailPopup khi click slot (UC 20.2 View Item Detail)
//   • Xử lý nút hành động từ popup:
//       - Equip Item        (UC 20.4)  – dùng InventoryItemId
//       - Unequip Item      (UC 20.5)  – dùng InventoryItemId
//       - Consume Item      (UC 20.3)  – dùng InventoryItemId
//       - Equip Skin        (UC 20.6)  – dùng PlayerSkinId (KHÁC InventoryItemId)
//       - Unequip Skin      (UC 20.7)  – dùng PlayerSkinId (KHÁC InventoryItemId)
//   • Refresh lại UI sau mỗi thao tác
//
// Cách dùng:
//   1. Gắn script này lên GameObject Inventory Panel trong scene Main.
//   2. Gán các reference qua Inspector.
//   3. Gọi LoadInventory() khi mở panel.
// =============================================================================
public class InventoryManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector References
    // -------------------------------------------------------------------------
    [Header("UI Panels")]
    [SerializeField] private UIInventory uiInventory;
    [SerializeField] private UISkinInventory uiSkinInventory;
    [SerializeField] private UIItemDetailPopup itemDetailPopup;
    [SerializeField] private UISkinDetailPopup skinDetailPopup;

    [Header("Filter Bars")]
    [SerializeField] private GameObject itemFilterBar;
    [SerializeField] private GameObject skinFilterBar;

    [Header("Tab Buttons (tuỳ chọn)")]
    [SerializeField] private Button tabItemsButton;
    [SerializeField] private Button tabSkinsButton;
    [SerializeField] private Toggle tabItemsToggle;
    [SerializeField] private Toggle tabSkinsToggle;

    [Header("Tab Highlights (tuỳ chọn)")]
    [SerializeField] private GameObject tabItemsHighlight;
    [SerializeField] private GameObject tabSkinsHighlight;

    [Header("Active Filter/Tab Sprites")]
    // Chỉ cần gán sprite trạng thái ĐANG CHỌN. Sprite thường được tự lưu lại từ scene lúc bind
    // (xem RegisterFilterVisual) nên không phải gán tay.
    [SerializeField] private Sprite filterActiveSprite;
    [SerializeField] private Sprite tabActiveSprite;

    [Header("Stats Labels (tuỳ chọn)")]
    [SerializeField] private TMP_Text totalItemsText;
    [SerializeField] private TMP_Text totalSkinsText;
    [SerializeField] private TMP_Text bagCapacityText;

    [Header("Player Avatar")]
    [SerializeField] private Image playerAvatarImage;
    [SerializeField] private Sprite knightIdleSprite;
    [SerializeField] private Sprite archerIdleSprite;
    [SerializeField] private Sprite mageIdleSprite;

    [Header("Battle Power (tuỳ chọn)")]
    [SerializeField] private TMP_Text battlePowerText;

    [Header("State")]
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private float cacheSeconds = 5f;

    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------
    private InventorySummaryResponse _summary;
    private string _currentFilter = "All"; // All, Weapon, Armor, Consumable, Material, QuestItem, Other
    private string _currentSkinFilter = "All"; // All, Owned, Unowned
    private int _currentSortIndex = 0; // 0=Latest, 1=Rarity High, 2=Rarity Low
    private TMP_Text _sortButtonText;
    private bool _showingSkins = false;
    private bool _requestInFlight;
    private bool _eventsBound;

    private SkinDatabaseSO _skinDatabase;
    private float _lastLoadedAt = -999f;

    // Filter/tab trong scene là Toggle với m_IsOn = 0, graphic = null và KHÔNG có ToggleGroup,
    // nên không có sẵn trạng thái "đang chọn" nào để hiện. Tự quản radio state ở đây rồi đổi
    // sprite của background Image (chính là targetGraphic của Toggle) cho nút đang active.
    // Key phải có tiền tố nhóm: item và skin là 2 bộ filter riêng và cùng có giá trị "All".
    private readonly Dictionary<string, Image> _filterGraphics = new Dictionary<string, Image>();
    // Sprite gốc trong scene, dùng làm trạng thái thường khi Inspector không gán ô Inactive.
    private readonly Dictionary<string, Sprite> _filterNormalSprites = new Dictionary<string, Sprite>();
    private Sprite _tabItemsNormalSprite;
    private Sprite _tabSkinsNormalSprite;
    private bool _tabNormalSpritesCached;

    private static string FilterKey(bool isSkinFilter, string filterValue)
        => (isSkinFilter ? "skin:" : "item:") + filterValue;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        BindUiReferences();
        BindEvents();
        AddHoverEffects();
        ShowTab(_showingSkins);
    }

    // Script này nằm trên GameObject "InventoryManager" RỖNG (0 con), nên
    // GetComponentsInChildren<Button> từ đây không với tới nút nào. Phải quét theo
    // CollectSearchRoots (đã gồm panel root + filter bar + 2 popup) mới đủ.
    // Toggle cũng cần: tab và filter trong panel là Toggle, không phải Button.
    private void AddHoverEffects()
    {
        foreach (var root in CollectSearchRoots())
        {
            if (root == null) continue;

            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable == null) continue;
                if (!(selectable is Button || selectable is Toggle)) continue;
                // Item trong Template của Dropdown sinh/huỷ theo lần mở — bỏ qua.
                if (selectable.GetComponentInParent<TMP_Dropdown>(true) != null) continue;
                // DimBackground là lớp phủ mờ toàn màn hình (bấm ra ngoài để đóng), phóng to nó
                // sẽ kéo giãn cả mảng tối mỗi khi chuột đi qua vùng trống.
                if (selectable.name == "DimBackground") continue;
                if (selectable.GetComponent<UIHoverScaleEffect>() == null)
                    selectable.gameObject.AddComponent<UIHoverScaleEffect>();
            }
        }
    }

    private void OnEnable()
    {
        // Load inventory mỗi khi mở panel
        LoadInventory();
    }

    // =========================================================================
    // UC 20.1 – Load Inventory
    // =========================================================================
    public static void RefreshAny(bool refreshStats = false)
    {
#if UNITY_2023_1_OR_NEWER
        var manager = UnityEngine.Object.FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
#else
        var manager = UnityEngine.Object.FindObjectOfType<InventoryManager>(true);
#endif
        manager?.LoadInventory(force: true, refreshStats: refreshStats);
    }

    public void LoadInventory(bool force = false, bool refreshStats = true)
    {
        BindUiReferences();
        BindEvents();
        UpdatePlayerAvatar();

        if (force)
        {
            _requestInFlight = false;
        }

        if (_requestInFlight)
            return;

        if (!force && _summary != null && Time.unscaledTime - _lastLoadedAt < cacheSeconds)
        {
            RefreshCurrentTab();
            if (refreshStats)
                LoadPlayerStats();
            return;
        }

        _requestInFlight = true;
        SetLoading(true);
        SetError(null);
        itemDetailPopup?.Hide();

        if (refreshStats)
            LoadPlayerStats();

        InventoryApi.Instance.GetInventory(
            onSuccess: response =>
            {
                _requestInFlight = false;
                SetLoading(false);
                if (response == null)
                {
                    SetError("Không tải được dữ liệu inventory.");
                    return;
                }

                _summary = response;
                _lastLoadedAt = Time.unscaledTime;
                UpdateStatsDisplay();
                UpdatePlayerAvatar();
                RefreshCurrentTab();
            },
            onError: error =>
            {
                _requestInFlight = false;
                SetLoading(false);
                SetError($"Lỗi tải inventory: {error.Message}");
                Debug.LogError($"[InventoryManager] LoadInventory FAIL: {error.Message}");
            }
        );
    }

    // =========================================================================
    // UC 20.2 – Handle Slot Click → Mở Item Detail Popup
    // =========================================================================
    private void HandleSlotClicked(UIBaseItemSlot slot)
    {
        if (_requestInFlight) return; // Prevent double clicks while API is processing
        if (slot?.RawData == null) return;

        // Tab Items: rawData là InventoryItemResponse
        if (!_showingSkins && slot.RawData is InventoryItemResponse item)
        {
            Sprite icon = ResolveIcon(item.ItemId, item.IconUrl, item.ItemName, item.ItemType);
            itemDetailPopup?.Show(item, icon);
            return;
        }

        // Tab Skins: rawData là PlayerSkinSummaryResponse
        if (_showingSkins && slot.RawData is PlayerSkinSummaryResponse skin)
        {
            Sprite icon = ResolveIcon(skin.SkinId, skin.IconUrl);
            skinDetailPopup?.ShowSkinDetails(skin, icon);
        }
    }

    // =========================================================================
    // UC 20.4 – Equip Item (dùng InventoryItemId)
    // =========================================================================
    private void HandleEquipItem(InventoryItemResponse item)
    {
        if (!CanEquipItem(item))
        {
            ShowActionError("Quest items cannot be equipped.");
            return;
        }

        Debug.Log($"[InventoryManager] EquipItem inventoryItemId={item.InventoryItemId}");
        itemDetailPopup?.Hide();

        InventoryApi.Instance.EquipItem(
            inventoryItemId: item.InventoryItemId,
            onSuccess: response =>
            {
                Debug.Log($"[InventoryManager] ✅ EquipItem OK");
                LoadInventory(force: true);
            },
            onError: error =>
            {
                Debug.LogError($"[InventoryManager] ❌ EquipItem FAIL: {error.Message}");
                ShowActionError($"Equip thất bại: {error.Message}");
            }
        );
    }

    // =========================================================================
    // UC 20.4.1 – Equip Initiated (Show Comparison)
    // =========================================================================
    private void HandleEquipInitiated(InventoryItemResponse newItem)
    {
        if (!CanEquipItem(newItem))
        {
            ShowActionError("Quest items are only used for quests.");
            return;
        }

        InventoryItemResponse oldItem = null;
        if (_summary?.EquippedItems != null)
        {
            foreach (var eq in _summary.EquippedItems)
            {
                if (eq != null && eq.IsEquipped && IsSameEquipSlot(eq, newItem))
                {
                    oldItem = eq;
                    break;
                }
            }
        }

        if (oldItem == null)
        {
            // Nothing equipped in this slot, equip directly without comparison
            HandleEquipItem(newItem);
        }
        else
        {
            Sprite oldIcon = oldItem != null ? ResolveIcon(oldItem.ItemId, oldItem.IconUrl, oldItem.ItemName, oldItem.ItemType) : null;
            itemDetailPopup?.ShowEquipComparison(oldItem, oldIcon);
        }
    }

    private static bool IsSameEquipSlot(InventoryItemResponse a, InventoryItemResponse b)
    {
        if (a == null || b == null) return false;

        string slotA = GetEquipSlotCategory(a);
        string slotB = GetEquipSlotCategory(b);

        if (!string.IsNullOrEmpty(slotA) && !string.IsNullOrEmpty(slotB))
            return string.Equals(slotA, slotB, System.StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private static string GetEquipSlotCategory(InventoryItemResponse item)
    {
        if (item == null) return string.Empty;

        // 1. Ưu tiên ItemSlot hoặc EquippedSlot (như "Helmet", "Armor", "Gloves", "Boots", "Weapon"...)
        foreach (var (slotObject, slotKeys) in EquipSlotMap)
        {
            foreach (var key in slotKeys)
            {
                if (Matches(item.EquippedSlot, key) || Matches(item.ItemSlot, key))
                    return slotObject;
            }
        }

        // 2. Fallback sang ItemType nếu ItemSlot/EquippedSlot không có
        foreach (var (slotObject, slotKeys) in EquipSlotMap)
        {
            foreach (var key in slotKeys)
            {
                if (Matches(item.ItemType, key))
                    return slotObject;
            }
        }

        return item.ItemType ?? string.Empty;
    }

    // =========================================================================
    // UC 20.5 – Unequip Item (dùng InventoryItemId)
    // =========================================================================
    private void HandleUnequipItem(InventoryItemResponse item)
    {
        if (!CanEquipItem(item))
        {
            ShowActionError("This item cannot be unequipped.");
            return;
        }

        Debug.Log($"[InventoryManager] UnequipItem inventoryItemId={item.InventoryItemId}");
        itemDetailPopup?.Hide();

        InventoryApi.Instance.UnequipItem(
            inventoryItemId: item.InventoryItemId,
            onSuccess: response =>
            {
                Debug.Log($"[InventoryManager] ✅ UnequipItem OK");
                LoadInventory(force: true);
            },
            onError: error =>
            {
                Debug.LogError($"[InventoryManager] ❌ UnequipItem FAIL: {error.Message}");
                ShowActionError($"Unequip thất bại: {error.Message}");
            }
        );
    }

    // =========================================================================
    // UC 20.3 – Consume Item (dùng InventoryItemId)
    // =========================================================================
    private void HandleConsumeItem(InventoryItemResponse item, int quantity)
    {
        if (item != null && item.ItemName != null && item.ItemName.Contains("Lucky Ticket", System.StringComparison.OrdinalIgnoreCase))
        {
            itemDetailPopup?.Hide();
            UIManager.Instance?.CloseAll();
            if (UIManager.Instance != null && UIManager.Instance.gachaPanel != null)
            {
                UIManager.Instance.OpenPanel(UIManager.Instance.gachaPanel);
            }
            return;
        }

        if (!IsConsumable(item) || quantity <= 0)
        {
            ShowActionError("Only consumable items can be used.");
            return;
        }

        Debug.Log($"[InventoryManager] ConsumeItem inventoryItemId={item.InventoryItemId} qty={quantity}");
        itemDetailPopup?.Hide();

        InventoryApi.Instance.ConsumeItem(
            inventoryItemId: item.InventoryItemId,
            quantity: quantity,
            onSuccess: _ =>
            {
                Debug.Log($"[InventoryManager] ✅ ConsumeItem OK");
                LoadPlayerStats(); // Refresh HP immediately after using a potion
                LoadInventory(force: true, refreshStats: false);
            },
            onError: error =>
            {
                Debug.LogError($"[InventoryManager] ❌ ConsumeItem FAIL: {error.Message}");
                ShowActionError($"Dùng item thất bại: {error.Message}");
            }
        );
    }

    // =========================================================================
    // UC 20.6 – Equip Skin (dùng PlayerSkinId – KHÔNG phải InventoryItemId)
    // PlayerSkinId lấy từ _summary.PlayerSkins[i].PlayerSkinId
    // =========================================================================
    private void HandleEquipSkin(PlayerSkinSummaryResponse skin)
    {
        Debug.Log($"[InventoryManager] EquipSkin playerSkinId={skin.PlayerSkinId} skinName={skin.SkinName}");
        skinDetailPopup?.Hide();

        InventoryApi.Instance.EquipSkin(
            playerSkinId: skin.PlayerSkinId,
            onSuccess: response =>
            {
                Debug.Log($"[InventoryManager] ✅ EquipSkin OK | SkinName={response?.SkinName}");
                UpdateLocalNetworkPlayerSkin(skin.SkinId);
                LoadInventory(force: true);
            },
            onError: error =>
            {
                Debug.LogError($"[InventoryManager] ❌ EquipSkin FAIL: {error.Message}");
                ShowActionError($"Equip skin thất bại: {error.Message}");
            }
        );
    }

    // =========================================================================
    // UC 20.7 – Unequip Skin (dùng PlayerSkinId – KHÔNG phải InventoryItemId)
    // =========================================================================
    private void HandleUnequipSkin(PlayerSkinSummaryResponse skin)
    {
        Debug.Log($"[InventoryManager] UnequipSkin playerSkinId={skin.PlayerSkinId} skinName={skin.SkinName}");
        skinDetailPopup?.Hide();

        InventoryApi.Instance.UnequipSkin(
            playerSkinId: skin.PlayerSkinId,
            onSuccess: _ =>
            {
                Debug.Log($"[InventoryManager] ✅ UnequipSkin OK");
                UpdateLocalNetworkPlayerSkin(0);
                LoadInventory(force: true);
            },
            onError: error =>
            {
                Debug.LogError($"[InventoryManager] ❌ UnequipSkin FAIL: {error.Message}");
                ShowActionError($"Unequip skin thất bại: {error.Message}");
            }
        );
    }

    private void UpdateLocalNetworkPlayerSkin(int skinId)
    {
        int normalizedSkinId = Mathf.Max(0, skinId);

        NetworkPlayer networkPlayer = NetworkPlayer.Local;
        if (networkPlayer == null && PlayerEntity.Instance != null)
            networkPlayer = PlayerEntity.Instance.GetComponent<NetworkPlayer>();

        if (networkPlayer != null && networkPlayer.Object != null)
        {
            networkPlayer.ApplyEquippedSkin(normalizedSkinId);
            return;
        }

        WorldState.EquippedSkinId = normalizedSkinId;
        WorldState.SaveToPlayerPrefs();

        var spawner = UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>();
        if (spawner != null)
            spawner.RespawnWithSkin();
    }

    // =========================================================================
    // Tab Management
    // =========================================================================
    private void ShowTab(bool showSkins)
    {
        _showingSkins = showSkins;
        itemDetailPopup?.Hide();

        if (tabItemsHighlight) tabItemsHighlight.SetActive(!showSkins);
        if (tabSkinsHighlight) tabSkinsHighlight.SetActive(showSkins);

        // tabItemsHighlight/tabSkinsHighlight không được gán trong scene, nên tab active phải
        // nhận biết bằng sprite giống filter.
        UpdateTabHighlights();

        if (itemFilterBar) itemFilterBar.SetActive(!showSkins);
        if (skinFilterBar) skinFilterBar.SetActive(showSkins);

        if (uiInventory) uiInventory.gameObject.SetActive(!showSkins);
        if (uiSkinInventory) uiSkinInventory.gameObject.SetActive(showSkins);

        UpdateFilterHighlights();
        RefreshCurrentTab();
    }

    // Đổi Image.sprite chứ KHÔNG dùng Toggle.spriteState: cả 9 filter và 2 tab đều để
    // Transition = ColorTint, mà SpriteState chỉ có tác dụng khi Transition = SpriteSwap. Ghi
    // thẳng sprite nên không phụ thuộc vào transition mode ai đặt trong Inspector.
    private static void ApplySprite(Image graphic, Sprite sprite)
    {
        if (graphic == null || sprite == null) return;
        if (graphic.sprite != sprite) graphic.sprite = sprite;
    }

    // Toggle bị set bằng code (hoặc bấm lại nút đang bật) phải đồng bộ lại isOn.
    // SetIsOnWithoutNotify để không gọi lại listener → tránh đệ quy / refresh đúp.
    private static void SyncToggle(Image graphic, bool active)
    {
        if (graphic == null) return;
        var toggle = graphic.GetComponent<Toggle>();
        if (toggle != null && toggle.isOn != active)
            toggle.SetIsOnWithoutNotify(active);
    }

    private void UpdateTabHighlights()
    {
        if (!_tabNormalSpritesCached)
        {
            _tabNormalSpritesCached = true;
            var itemsGraphic = tabItemsToggle != null ? tabItemsToggle.targetGraphic as Image : null;
            var skinsGraphic = tabSkinsToggle != null ? tabSkinsToggle.targetGraphic as Image : null;
            if (itemsGraphic != null) _tabItemsNormalSprite = itemsGraphic.sprite;
            if (skinsGraphic != null) _tabSkinsNormalSprite = skinsGraphic.sprite;
        }

        ApplyTabVisual(tabItemsToggle, !_showingSkins, _tabItemsNormalSprite);
        ApplyTabVisual(tabSkinsToggle, _showingSkins, _tabSkinsNormalSprite);
    }

    private void ApplyTabVisual(Toggle tab, bool active, Sprite normalSprite)
    {
        if (tab == null) return;

        var graphic = tab.targetGraphic as Image;
        ApplySprite(graphic, active ? tabActiveSprite : normalSprite);

        if (tab.isOn != active) tab.SetIsOnWithoutNotify(active);
    }

    public void SetFilter(string filterType)
    {
        if (_showingSkins)
        {
            _currentSkinFilter = filterType;
        }
        else
        {
            _currentFilter = filterType;
        }
        UpdateFilterHighlights();
        RefreshCurrentTab();
    }

    public void CycleSort()
    {
        _currentSortIndex = (_currentSortIndex + 1) % 3;
        UpdateSortButtonText();
        
        string sortModeName = _currentSortIndex switch
        {
            0 => "Mới nhất (Latest)",
            1 => "Độ hiếm: Cao -> Thấp",
            2 => "Độ hiếm: Thấp -> Cao",
            _ => "Mặc định"
        };
        
        Debug.Log($"[InventoryManager] Switched Sort Mode to: {sortModeName}");
        SetError($"Sắp xếp: {sortModeName}");
        
        RefreshCurrentTab();
    }

    private TMP_Dropdown _sortDropdown;

    public void SetSortIndex(int index)
    {
        _currentSortIndex = index;
        RefreshCurrentTab();
    }

    private void UpdateSortButtonText()
    {
        if (_sortDropdown != null) return; // Dropdown automatically handles its text

        if (_sortButtonText == null)
        {
            var btnSort = FindButton("BtnSort", "SortButton", "OptionA");
            if (btnSort != null) _sortButtonText = btnSort.GetComponentInChildren<TMP_Text>();
        }
        
        if (_sortButtonText == null) return;
        
        switch (_currentSortIndex)
        {
            case 0: _sortButtonText.text = "Latest"; break;
            case 1: _sortButtonText.text = "Rarity: High"; break;
            case 2: _sortButtonText.text = "Rarity: Low"; break;
        }
    }

    private int GetRarityValue(string rarity)
    {
        if (string.IsNullOrEmpty(rarity)) return 0;
        switch (rarity.ToLower())
        {
            case "common": return 1;
            case "uncommon": return 2;
            case "rare": return 3;
            case "epic": return 4;
            case "legendary": return 5;
            case "mythic": return 6;
            default: return 0;
        }
    }


    private void RefreshCurrentTab()
    {
        if (_summary == null) return;

        var displayList = new List<UIItemDisplayData>();

        if (_showingSkins)
        {
            if (uiSkinInventory == null) return;
            // ── Tab Skins: lấy từ _summary.PlayerSkins ─────────────────────
            // (có PlayerSkinId đúng để dùng khi equip/unequip)
            var skins = _summary.PlayerSkins;
            if (skins == null) { uiSkinInventory.Refresh(displayList); return; }

            string pClass = MysticJourney.Core.Services.GameStateService.Instance?.PlayerClass;

            // Lọc skin
            var filteredSkins = new List<PlayerSkinSummaryResponse>();
            foreach (var skin in skins)
            {
                bool isOwned = skin.PlayerSkinId > 0;
                
                if (_currentSkinFilter == "Owned" && !isOwned) continue;
                if (_currentSkinFilter == "Unowned" && isOwned) continue;
                
                if (IsSkinForAnotherClass(skin.SkinId, skin.SkinName, pClass)) continue;

                filteredSkins.Add(skin);
            }

            // Sort skins according to _currentSortIndex
            filteredSkins.Sort((a, b) => {
                if (_currentSortIndex == 0) // Default / ID
                {
                    return b.PlayerSkinId.CompareTo(a.PlayerSkinId);
                }
                else // Rarity
                {
                    int rA = GetRarityValue(a.SkinRarity);
                    int rB = GetRarityValue(b.SkinRarity);
                    if (rA != rB)
                    {
                        return _currentSortIndex == 1 ? rB.CompareTo(rA) : rA.CompareTo(rB);
                    }
                    return b.PlayerSkinId.CompareTo(a.PlayerSkinId);
                }
            });

            foreach (var skin in filteredSkins)
            {
                Sprite icon = ResolveIcon(skin.SkinId, skin.IconUrl);
                displayList.Add(new UIItemDisplayData
                {
                    itemId = skin.PlayerSkinId,  // dùng PlayerSkinId làm id hiển thị (0 = chưa sở hữu)
                    itemName = skin.SkinName,
                    icon = icon,
                    quantity = 1,
                    rarity = skin.SkinRarity,
                    isEquipped = skin.IsEquipped,
                    rawData = skin                  // PlayerSkinSummaryResponse để popup dùng
                });
            }
            uiSkinInventory.Refresh(displayList);
        }
        else
        {
            if (uiInventory == null) return;
            // ── Tab Items: lấy từ EquippedItems + BagItems ─────────────────
            // Lọc ra item thông thường (không phải skin)
            var allItems = new List<InventoryItemResponse>();

            if (_summary.EquippedItems != null)
                foreach (var it in _summary.EquippedItems)
                    if (ShouldShowInventoryItem(it)) allItems.Add(it);

            if (_summary.BagItems != null)
                foreach (var it in _summary.BagItems)
                    if (ShouldShowInventoryItem(it)) allItems.Add(it);

            // --- FILTER ---
            if (_currentFilter != "All")
            {
                allItems.RemoveAll(it => {
                    if (_currentFilter == "Armor")
                    {
                        return !(it.ItemType == "Armor" || it.ItemType == "Helmet" || it.ItemType == "Gloves" || it.ItemType == "Boots" || it.ItemType == "Ring" || it.ItemType == "Necklace" || it.ItemType == "Shield");
                    }
                    if (_currentFilter == "Other")
                    {
                        return it.ItemType == "Weapon" || it.ItemType == "Armor" || it.ItemType == "Helmet" || it.ItemType == "Gloves" || it.ItemType == "Boots" || it.ItemType == "Ring" || it.ItemType == "Necklace" || it.ItemType == "Shield" || it.ItemType == "Consumable" || it.ItemType == "Material" || it.ItemType == "QuestItem" || it.ItemType == "Quest";
                    }
                    if (_currentFilter == "QuestItem")
                    {
                        return !(it.ItemType == "QuestItem" || it.ItemType == "Quest");
                    }
                    return it.ItemType != _currentFilter;
                });
            }

            // --- SORT ---
            allItems.Sort((a, b) => {
                if (_currentSortIndex == 0) // Latest
                {
                    return b.InventoryItemId.CompareTo(a.InventoryItemId);
                }
                else // Rarity
                {
                    int rA = GetRarityValue(a.ItemRarity);
                    int rB = GetRarityValue(b.ItemRarity);
                    if (rA != rB)
                    {
                        return _currentSortIndex == 1 ? rB.CompareTo(rA) : rA.CompareTo(rB);
                    }
                    return b.InventoryItemId.CompareTo(a.InventoryItemId);
                }
            });

            foreach (var item in allItems)
            {
                Sprite icon = ResolveIcon(item.ItemId, item.IconUrl, item.ItemName, item.ItemType);
                displayList.Add(new UIItemDisplayData
                {
                    itemId = item.InventoryItemId,
                    itemName = item.ItemName,
                    icon = icon,
                    quantity = item.Quantity,
                    rarity = item.ItemRarity,
                    isEquipped = item.IsEquipped && CanEquipItem(item),
                    rawData = item  // InventoryItemResponse để popup dùng
                });
            }
            uiInventory.Refresh(displayList);
        }
    }

    private void BindUiReferences()
    {
        if (uiInventory == null)
            uiInventory = GetComponentInChildren<UIInventory>(true);
        if (uiSkinInventory == null)
            uiSkinInventory = GetComponentInChildren<UISkinInventory>(true);
        if (itemDetailPopup == null)
        {
            itemDetailPopup = GetComponentInChildren<UIItemDetailPopup>(true);
            if (itemDetailPopup == null)
            {
#if UNITY_2023_1_OR_NEWER
                itemDetailPopup = UnityEngine.Object.FindFirstObjectByType<UIItemDetailPopup>(FindObjectsInactive.Include);
#else
                itemDetailPopup = UnityEngine.Object.FindObjectOfType<UIItemDetailPopup>(true);
#endif
            }
        }
        if (skinDetailPopup == null)
        {
            skinDetailPopup = GetComponentInChildren<UISkinDetailPopup>(true);
            if (skinDetailPopup == null)
            {
#if UNITY_2023_1_OR_NEWER
                skinDetailPopup = UnityEngine.Object.FindFirstObjectByType<UISkinDetailPopup>(FindObjectsInactive.Include);
#else
                skinDetailPopup = UnityEngine.Object.FindObjectOfType<UISkinDetailPopup>(true);
#endif
            }
        }

        tabItemsButton = tabItemsButton != null ? tabItemsButton : FindButton("TabItemsButton", "ItemsButton", "ItemTabButton", "EquipmentTab");
        tabSkinsButton = tabSkinsButton != null ? tabSkinsButton : FindButton("TabSkinsButton", "SkinsButton", "SkinTabButton", "AppearanceTab");
        
        if (tabItemsToggle == null) tabItemsToggle = FindToggle("EquipmentTab", "TabItemsToggle", "ItemTabToggle");
        if (tabSkinsToggle == null) tabSkinsToggle = FindToggle("AppearanceTab", "TabSkinsToggle", "SkinTabToggle");
        tabItemsHighlight = tabItemsHighlight != null ? tabItemsHighlight : FindObject("TabItemsHighlight", "ItemsHighlight", "ItemTabHighlight");
        tabSkinsHighlight = tabSkinsHighlight != null ? tabSkinsHighlight : FindObject("TabSkinsHighlight", "SkinsHighlight", "SkinTabHighlight");

        if (loadingIndicator == null)
            loadingIndicator = FindObject("LoadingIndicator", "Loading", "Spinner");
        if (errorText == null)
            errorText = FindText("ErrorText", "MessageText", "StatusText");

        // Tên object trong scene có DẤU CÁCH ở cuối ("BattlePowerText ") — Unity cho phép và
        // FindObject so sánh chuỗi chính xác nên phải liệt kê cả 2 biến thể, nếu không sẽ không
        // bao giờ tìm thấy và Lực chiến mãi trống.
        if (battlePowerText == null)
            battlePowerText = FindText("BattlePowerText ", "BattlePowerText", "BattlePower", "PowerText");

        // FIX: Remove broken TMP_Dropdown components that cause "template not assigned" errors when clicked
        // (This happens if a Button like SkinTab or SortBtn accidentally has a TMP_Dropdown component added to it)
        var allDropdowns = GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (var dropdown in allDropdowns)
        {
            if (dropdown != null && dropdown.template == null)
            {
                Debug.LogWarning($"[InventoryManager] Destroying broken TMP_Dropdown on '{dropdown.gameObject.name}' to prevent UI click errors.");
                Destroy(dropdown);
            }
        }

        UpdateSortButtonText();
    }

    private void BindEvents()
    {
        if (_eventsBound)
            return;

        if (uiInventory != null)
            uiInventory.OnInventorySlotClicked += HandleSlotClicked;
        if (uiSkinInventory != null)
            uiSkinInventory.OnInventorySlotClicked += HandleSlotClicked;

        if (itemDetailPopup != null)
        {
            itemDetailPopup.OnEquipInitiated += HandleEquipInitiated;
            itemDetailPopup.OnEquipConfirmed += HandleEquipItem;
            itemDetailPopup.OnUnequipClicked += HandleUnequipItem;
            itemDetailPopup.OnConsumeConfirmed += HandleConsumeItem;
        }

        if (skinDetailPopup != null)
        {
            skinDetailPopup.OnEquipSkinClicked += HandleEquipSkin;
            skinDetailPopup.OnUnequipSkinClicked += HandleUnequipSkin;
        }

        if (tabItemsButton) tabItemsButton.onClick.AddListener(() => ShowTab(false));
        if (tabSkinsButton) tabSkinsButton.onClick.AddListener(() => ShowTab(true));

        if (tabItemsToggle) tabItemsToggle.onValueChanged.AddListener(isOn => { if (isOn) ShowTab(false); });
        if (tabSkinsToggle) tabSkinsToggle.onValueChanged.AddListener(isOn => { if (isOn) ShowTab(true); });

        // Cleanup any broken TMP_Dropdown (template == null) that blocks button click events
        var allDropdowns = GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (var dd in allDropdowns)
        {
            if (dd != null && dd.template == null)
            {
                DestroyImmediate(dd);
            }
        }

        BindFilterAction("All", false, "BtnFilterAll", "AllButton");
        BindFilterAction("Weapon", false, "BtnFilterWeapon", "WeaponButton");
        BindFilterAction("Armor", false, "BtnFilterArmor", "ArmorButton");
        BindFilterAction("Consumable", false, "BtnFilterConsumable", "PotionButton", "ConsumableButton");
        BindFilterAction("Material", false, "BtnFilterMaterial", "MaterialButton");
        BindFilterAction("QuestItem", false, "BtnFilterQuest", "QuestButton");
        BindFilterAction("Other", false, "BtnFilterOther", "OtherButton");

        BindFilterAction("All", true, "BtnSkinFilterAll");
        BindFilterAction("Owned", true, "BtnSkinFilterOwned");
        BindFilterAction("Unowned", true, "BtnSkinFilterUnowned");

        BindAction(CycleSort, "BtnSort", "SortButton", "OptionA", "BtnSkinSort");
        _sortDropdown = FindDropdown("BtnSort", "SortButton", "OptionA", "BtnSkinSort");
        if (_sortDropdown != null && _sortDropdown.template == null)
        {
            DestroyImmediate(_sortDropdown);
            _sortDropdown = null;
        }

        if (_sortDropdown) _sortDropdown.onValueChanged.AddListener(SetSortIndex);

        _eventsBound = uiInventory != null || itemDetailPopup != null || skinDetailPopup != null || tabItemsButton != null || tabSkinsButton != null;
    }

    private Sprite ResolveIcon(int itemId, string iconUrl, string itemName = null, string itemType = null)
    {
        var isSkinLookup = string.IsNullOrWhiteSpace(itemName) && string.IsNullOrWhiteSpace(itemType) && itemId > 0;

        if (isSkinLookup)
        {
            var prefabIcon = ResolveSkinPrefabIcon(itemId);
            if (prefabIcon != null)
                return prefabIcon;

            var remoteIcon = ResolveRemoteIcon(iconUrl);
            if (remoteIcon != null)
                return remoteIcon;
        }

        if (ItemIconDatabase.Instance != null)
        {
            var localIcon = ItemIconDatabase.Instance.GetIcon(itemName, itemType);
            if (localIcon != null)
                return localIcon;

            if (isSkinLookup)
            {
                if (ItemIconDatabase.Instance.TryGetIcon($"skin:{itemId}", out localIcon) && localIcon != null)
                    return localIcon;
                if (ItemIconDatabase.Instance.TryGetIcon(itemId.ToString(), out localIcon) && localIcon != null)
                    return localIcon;
                if (ItemIconDatabase.Instance.TryGetIcon("Skin", out localIcon) && localIcon != null)
                    return localIcon;
            }
        }

        return ResolveRemoteIcon(iconUrl);
    }

    private Button FindButton(params string[] names)
    {
        var obj = FindObject(names);
        return obj == null ? null : obj.GetComponent<Button>();
    }

    private Toggle FindToggle(params string[] names)
    {
        var obj = FindObject(names);
        return obj == null ? null : obj.GetComponent<Toggle>();
    }

    private void BindFilterAction(string filterValue, bool isSkinFilter, params string[] names)
    {
        var obj = FindObject(names);
        if (obj == null)
        {
            Debug.LogWarning("[InventoryManager] BindFilterAction: Could not find object for " + filterValue);
            return;
        }

        var btn = obj.GetComponent<Button>();
        if (btn != null)
        {
            RegisterFilterVisual(FilterKey(isSkinFilter, filterValue), btn);
            btn.onClick.AddListener(() => {
                Debug.Log("[InventoryManager] Button clicked: " + filterValue);
                SetFilter(filterValue);
            });
            return;
        }

        var toggle = obj.GetComponent<Toggle>();
        if (toggle != null)
        {
            RegisterFilterVisual(FilterKey(isSkinFilter, filterValue), toggle);
            toggle.onValueChanged.AddListener((isOn) => {
                if (isOn)
                {
                    Debug.Log("[InventoryManager] Toggle selected: " + filterValue);
                    SetFilter(filterValue);
                }
                else
                {
                    // Bấm lại filter đang bật: giữ nguyên filter và bật lại toggle, nếu không
                    // nút sẽ tắt highlight mà danh sách vẫn đang lọc theo nó.
                    UpdateFilterHighlights();
                }
            });
            return;
        }

        Debug.LogWarning("[InventoryManager] BindFilterAction: Object " + obj.name + " has neither Button nor Toggle for " + filterValue);
    }

    // targetGraphic của các nút này là background Image nằm trên chính object đó (đã kiểm trong
    // scene), nên đủ để đổi sprite. Fallback GetComponent<Image> cho nút nào bỏ trống targetGraphic.
    private void RegisterFilterVisual(string key, Selectable selectable)
    {
        var graphic = selectable.targetGraphic as Image;
        if (graphic == null) graphic = selectable.GetComponent<Image>();
        if (graphic == null)
        {
            Debug.LogWarning("[InventoryManager] RegisterFilterVisual: no Image on " + selectable.name);
            return;
        }

        _filterGraphics[key] = graphic;
        // BindEvents có thể chạy lại (xem _eventsBound), lúc đó nút đang chọn đã mang sprite
        // active — cache nó làm "sprite thường" thì nút sẽ kẹt highlight vĩnh viễn.
        if (graphic.sprite != filterActiveSprite) _filterNormalSprites[key] = graphic.sprite;
    }

    // Đổi sprite cho nút đang active và trả phần còn lại về sprite thường. Chỉ đụng tới nhóm đang
    // hiện (item/skin) để filter của tab kia không bị xoá highlight khi quay lại.
    private void UpdateFilterHighlights()
    {
        string activeKey = FilterKey(_showingSkins, _showingSkins ? _currentSkinFilter : _currentFilter);
        string groupPrefix = _showingSkins ? "skin:" : "item:";

        foreach (var pair in _filterGraphics)
        {
            var graphic = pair.Value;
            if (graphic == null) continue;
            if (!pair.Key.StartsWith(groupPrefix, System.StringComparison.Ordinal)) continue;

            bool active = pair.Key == activeKey;

            _filterNormalSprites.TryGetValue(pair.Key, out Sprite normal);
            ApplySprite(graphic, active ? filterActiveSprite : normal);
            SyncToggle(graphic, active);
        }
    }

    private void BindAction(System.Action action, params string[] names)
    {
        var obj = FindObject(names);
        if (obj == null) return;
        
        var btn = obj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(new UnityEngine.Events.UnityAction(action));
            return;
        }

        var toggle = obj.GetComponent<Toggle>();
        if (toggle != null)
        {
            toggle.onValueChanged.AddListener((isOn) => { if (isOn) action(); });
        }
    }

    private TMP_Text FindText(params string[] names)
    {
        // FindComponent, KHÔNG phải FindObject: FindObject quét theo thứ tự hierarchy (children
        // ngoài, names trong) nên object CHA khớp tên trước con. Ví dụ "BattlePower" (container,
        // không có TMP) khớp trước con "BattlePowerText " → GetComponent<TMP_Text>() trả null và
        // lực chiến không bao giờ được ghi. Bản này đòi object phải THỰC SỰ có TMP_Text.
        return FindComponent<TMP_Text>(names);
    }

    // Tìm theo (tên, loại component): duyệt names theo ĐÚNG thứ tự ưu tiên đã truyền vào, và chỉ
    // nhận object nào có sẵn component T. Tránh bẫy "cha cùng tên nhưng không mang component".
    private T FindComponent<T>(params string[] names) where T : Component
    {
        var roots = CollectSearchRoots();

        for (var j = 0; j < names.Length; j++)
        {
            foreach (var root in roots)
            {
                if (root == null) continue;
                var children = root.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < children.Length; i++)
                {
                    if (children[i] == null || children[i].name != names[j]) continue;
                    var comp = children[i].GetComponent<T>();
                    if (comp != null) return comp;
                }
            }
        }

        return null;
    }

    // Danh sách gốc để quét tìm object theo tên. Dùng chung cho FindObject và FindComponent.
    private List<Transform> CollectSearchRoots()
    {
        List<Transform> roots = new List<Transform>();
        roots.Add(transform);
        // Gốc InventoryPanel: script này nằm trên GameObject "Managers" NGOÀI panel, còn
        // uiInventory nằm ở InventoryPanel > RightSection > InventoryGridArea. Tìm từ transform
        // hoặc từ uiInventory chỉ quét XUỐNG nên không bao giờ với tới LeftSection (avatar, ô
        // trang bị, lực chiến) ở nhánh bên cạnh. Phải thêm gốc panel để quét được cả 2 nhánh.
        var panelRoot = ResolvePanelRoot();
        if (panelRoot != null) roots.Add(panelRoot);
        if (itemFilterBar != null) roots.Add(itemFilterBar.transform);
        if (skinFilterBar != null) roots.Add(skinFilterBar.transform);
        if (uiInventory != null) roots.Add(uiInventory.transform);
        if (uiSkinInventory != null) roots.Add(uiSkinInventory.transform);
        if (itemDetailPopup != null) roots.Add(itemDetailPopup.transform);
        if (skinDetailPopup != null) roots.Add(skinDetailPopup.transform);
        return roots;
    }

    private GameObject FindObject(params string[] names)
    {
        var roots = CollectSearchRoots();

        foreach (var root in roots)
        {
            if (root == null) continue;
            var children = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                for (var j = 0; j < names.Length; j++)
                {
                    if (children[i] != null && children[i].name == names[j])
                        return children[i].gameObject;
                }
            }
        }

        return null;
    }

    private TMP_Dropdown FindDropdown(params string[] names)
    {
        var obj = FindObject(names);
        return obj == null ? null : obj.GetComponent<TMP_Dropdown>();
    }

    // Gốc "InventoryPanel" trong scene. Leo lên từ uiInventory (nằm trong panel) để không phụ
    // thuộc vào việc script này được gắn ở đâu; fallback quét cả scene (kể cả object đang tắt —
    // InventoryPanel mặc định m_IsActive=0).
    private Transform _panelRootCache;
    private Transform ResolvePanelRoot()
    {
        if (_panelRootCache != null) return _panelRootCache;

        var probe = uiInventory != null ? uiInventory.transform
                  : uiSkinInventory != null ? uiSkinInventory.transform
                  : null;

        for (var t = probe; t != null; t = t.parent)
        {
            if (t.name == "InventoryPanel") { _panelRootCache = t; return t; }
        }

        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t != null && t.name == "InventoryPanel" && t.gameObject.scene.IsValid())
            {
                _panelRootCache = t;
                return t;
            }
        }

        return null;
    }
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private void UpdateStatsDisplay()
    {
        if (_summary == null) return;
        if (totalItemsText) totalItemsText.text = $"Items: {_summary.TotalItems}";
        if (totalSkinsText) totalSkinsText.text = $"Skins: {_summary.TotalSkins}";
        if (bagCapacityText) bagCapacityText.text = $"Bag: {(_summary.BagItems?.Length ?? 0)}/{_summary.BagCapacity}";

        // Icon trang bị đang mặc phụ thuộc _summary.EquippedItems nên refresh cùng lúc với summary.
        UpdateEquipmentSlots();
    }

    private void SetLoading(bool isLoading)
    {
        if (loadingIndicator) loadingIndicator.SetActive(isLoading);
    }

    private void SetError(string msg)
    {
        if (errorText)
        {
            errorText.text = msg ?? string.Empty;
            errorText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
        }
    }

    private void ShowActionError(string msg)
    {
        Debug.LogWarning($"[InventoryManager] Action error: {msg}");
        SetError(msg);
    }

    private void LoadPlayerStats()
    {
        CharacterApi.Instance.GetMyStats(
            onSuccess: response =>
            {
                if (response != null)
                {
                    UpdatePlayerStatsUI(response);
                    PlayerHUDController.Instance?.ApplyStats(response);
                    PlayerEntity.Instance?.ApplyHealth(response.CurrentHp, response.MaxHp);
                }
            },
            onError: error =>
            {
                Debug.LogWarning($"[InventoryManager] Failed to load character stats: {error.Message}");
            }
        );
    }

    private Transform FindStatsPanel()
    {
        var allChildren = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (var t in allChildren)
        {
            if (t.name.Contains("StatsPanel"))
                return t;
        }
        return null;
    }

    private void UpdateStatRow(Transform statsPanel, string rowName, string label, string value)
    {
        var row = statsPanel.Find(rowName);
        if (row != null)
        {
            // Nhãn là TUỲ CHỌN: các row trong scene chỉ có con "icon" + "ValueText" (tên chỉ số
            // nằm trong sprite icon), nên không tìm thấy "Text (TMP)" là bình thường — trước đây
            // chỗ này log warning mỗi lần refresh cho cả 8 row.
            var labelText = row.Find("Text (TMP)")?.GetComponent<TMP_Text>()
                         ?? row.Find("LabelText")?.GetComponent<TMP_Text>();
            var valueText = row.Find("ValueText")?.GetComponent<TMP_Text>();

            if (labelText != null)
                labelText.text = label;

            if (valueText != null)
            {
                valueText.enableWordWrapping = false;
                valueText.text = value;
            }
            else
                Debug.LogWarning($"[InventoryManager] ValueText not found or missing TMP_Text component in {rowName}");
        }
        else
        {
            Debug.LogWarning($"[InventoryManager] Row '{rowName}' not found in stats panel.");
        }
    }

    private void UpdatePlayerStatsUI(PlayerStatsResponse stats)
    {
        if (stats == null) return;

        // Lực chiến ghi TRƯỚC và nằm NGOÀI cửa ải StatsPanel: nó là object riêng
        // (LeftSection > BattlePower), không nằm trong StatsPanel. Nếu để ở cuối hàm thì mỗi khi
        // không tìm thấy StatsPanel, hàm return sớm và lực chiến cũng mất theo dù chẳng liên quan.
        UpdateBattlePower(stats);

        var statsPanel = FindStatsPanel();
        if (statsPanel == null)
        {
            Debug.LogWarning("[InventoryManager] StatsPanel not found.");
            return;
        }

        UpdateStatRow(statsPanel, "HPRow", "HP", stats.MaxHp.ToString());
        UpdateStatRow(statsPanel, "ATKRow", "ATK", stats.Atk.ToString());
        UpdateStatRow(statsPanel, "DEFRow", "DEF", stats.Def.ToString());
        UpdateStatRow(statsPanel, "SPDRow", "SPD", stats.MoveSpeed.ToString());
        UpdateStatRow(statsPanel, "ASPRow", "ASP", stats.AttackSpeed.ToString());
        UpdateStatRow(statsPanel, "CRITRow", "CRT", $"{stats.CritRate}%");
        UpdateStatRow(statsPanel, "CRITDAMAGERow", "CRTD", $"{stats.CritDamage}%");
        UpdateStatRow(statsPanel, "DMGBonusRow", "%DMG", $"{stats.DamageBonus}%");
    }

    private void UpdatePlayerAvatar()
    {
        if (playerAvatarImage == null)
            playerAvatarImage = FindImage("Character", "PlayerAvatar", "AvatarImage");
        if (playerAvatarImage == null) return;

        playerAvatarImage.preserveAspect = true;

        string pClass = MysticJourney.Core.Services.GameStateService.Instance?.PlayerClass;
        Sprite sprite = null;

        // Ưu tiên hiển thị sprite của Skin đang được trang bị (Equipped)
        if (_summary != null && _summary.PlayerSkins != null)
        {
            foreach (var skin in _summary.PlayerSkins)
            {
                if (skin != null && skin.IsEquipped && skin.SkinId > 0)
                {
                    bool isDefault = skin.SkinName != null && skin.SkinName.IndexOf("Default", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!isDefault)
                    {
                        sprite = ResolveIcon(skin.SkinId, skin.IconUrl);
                        if (sprite != null)
                            break;
                    }
                }
            }
        }

        // Fallback về sprite mặc định theo Class nếu không mặc skin tùy chỉnh
        if (sprite == null)
            sprite = ResolveClassSprite(pClass);

        if (sprite != null)
            playerAvatarImage.sprite = sprite;

        bool hasSprite = playerAvatarImage.sprite != null;
        playerAvatarImage.enabled = hasSprite;
        if (hasSprite && !playerAvatarImage.gameObject.activeSelf)
            playerAvatarImage.gameObject.SetActive(true);

        if (!hasSprite)
            Debug.LogWarning($"[InventoryManager] No avatar sprite for class '{pClass}'. Assign knight/archer/mage idle sprites in the Inspector.");
    }

    private Sprite ResolveClassSprite(string playerClass)
    {
        var c = (playerClass ?? string.Empty).Trim();
        if (c.Equals("Knight", System.StringComparison.OrdinalIgnoreCase)) return knightIdleSprite;
        if (c.Equals("Archer", System.StringComparison.OrdinalIgnoreCase)) return archerIdleSprite;
        if (c.Equals("Mage", System.StringComparison.OrdinalIgnoreCase)) return mageIdleSprite;
        // Class lạ/chưa set: dùng Knight làm mặc định thay vì để trống hẳn.
        return knightIdleSprite;
    }

    // ── Ô trang bị (CharacterPreviewArea > EquipSlots) ───────────────────────────
    // Mỗi ô có 1 Image nền (chính nó) + 1 con tên "Image" để vẽ icon món đang mặc.
    private static readonly (string slotObject, string[] slotKeys)[] EquipSlotMap =
    {
        ("WeaponSlot",    new[] { "Weapon", "MainHand" }),
        ("HelmetSlot",    new[] { "Helmet", "Head" }),
        ("ArmorSlot",     new[] { "Armor", "Body", "Chest" }),
        ("GlovesSlot",    new[] { "Gloves", "Hands" }),
        ("BootsSlot",     new[] { "Boots", "Feet" }),
        ("PantsSlot",     new[] { "Pants", "Legs" }),
        ("ShieldSlot",    new[] { "Shield", "OffHand" }),
        ("AccessorySlot", new[] { "Accessory", "Ring", "Necklace" }),
    };

    private void UpdateEquipmentSlots()
    {
        var equipped = _summary?.EquippedItems;

        foreach (var (slotObject, slotKeys) in EquipSlotMap)
        {
            var iconImage = FindEquipSlotIcon(slotObject);
            if (iconImage == null)
                continue;

            var item = FindEquippedForSlot(equipped, slotObject);
            if (item == null)
            {
                // Ô rỗng: ẩn icon để lộ nền ô, KHÔNG tắt cả ô (nền phải luôn thấy).
                iconImage.sprite = null;
                iconImage.enabled = false;
                continue;
            }

            var icon = ResolveIcon(item.ItemId, item.IconUrl, item.ItemName, item.ItemType);
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.preserveAspect = true;
        }
    }

    private Image FindEquipSlotIcon(string slotObjectName)
    {
        var slot = FindObject(slotObjectName);
        if (slot == null) return null;

        // Con tên "Image" là lớp vẽ icon; nếu prefab không có thì dùng luôn Image của ô.
        var child = slot.transform.Find("Image");
        var image = child != null ? child.GetComponent<Image>() : null;
        return image != null ? image : slot.GetComponent<Image>();
    }

    private static InventoryItemResponse FindEquippedForSlot(InventoryItemResponse[] equipped, string slotObject)
    {
        if (equipped == null) return null;

        foreach (var item in equipped)
        {
            if (item == null || !item.IsEquipped) continue;

            if (string.Equals(GetEquipSlotCategory(item), slotObject, System.StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }

    private static bool Matches(string value, string key)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Trim().Equals(key, System.StringComparison.OrdinalIgnoreCase);
    }

    // ── Lực chiến ────────────────────────────────────────────────────────────────
    // BE chưa có endpoint/field lực chiến (FriendDTO.Power chỉ là Level*100 tạm), nên tính ở
    // client từ đúng bộ stats mà panel đã tải. Đổi công thức thì sửa một chỗ này.
    private void UpdateBattlePower(PlayerStatsResponse stats)
    {
        if (battlePowerText == null)
            battlePowerText = FindText("BattlePowerText ", "BattlePowerText", "BattlePower", "PowerText");
        if (battlePowerText == null || stats == null) return;

        battlePowerText.text = CalculateBattlePower(stats).ToString("N0");
    }

    // ponytail: công thức tuyến tính đơn giản (ATK nặng nhất, HP nhẹ nhất) — đủ để so sánh
    // tương đối giữa các bộ trang bị. Nâng cấp: chuyển sang BE tính và trả về cùng PlayerStats
    // để client/web/mobile hiện cùng một số.
    private static int CalculateBattlePower(PlayerStatsResponse s)
    {
        float power = s.Atk * 4f
                    + s.Def * 3f
                    + s.MaxHp * 0.5f
                    + s.CritRate * 2f
                    + s.CritDamage * 1f
                    + s.DamageBonus * 2f;
        return Mathf.Max(0, Mathf.RoundToInt(power));
    }

    private Image FindImage(params string[] names)
    {
        // FindComponent: cùng lý do như FindText — object cha có thể trùng tên nhưng không mang
        // Image, khiến GetComponent<Image>() trả null dù con đúng vẫn tồn tại.
        return FindComponent<Image>(names);
    }

    private static bool ShouldShowInventoryItem(InventoryItemResponse item)
    {
        if (item == null || item.IsSkin)
            return false;

        if (item.Quantity > 0)
            return true;

        return item.IsEquipped && CanEquipItem(item);
    }

    private static bool CanEquipItem(InventoryItemResponse item)
    {
        return IsEquipment(item);
    }

    private static bool IsConsumable(InventoryItemResponse item)
    {
        return IsItemType(item, "Consumable") || (item != null && item.ItemName != null && item.ItemName.Contains("Lucky Ticket", System.StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEquipment(InventoryItemResponse item)
    {
        return IsItemType(item, "Weapon") ||
               IsItemType(item, "Armor") ||
               IsItemType(item, "Accessory") ||
               IsItemType(item, "Helmet") ||
               IsItemType(item, "Gloves") ||
               IsItemType(item, "Boots") ||
               IsItemType(item, "Ring") ||
               IsItemType(item, "Necklace");
    }

    private static bool IsItemType(InventoryItemResponse item, string itemType)
    {
        return item != null &&
               string.Equals(item.ItemType, itemType, System.StringComparison.OrdinalIgnoreCase);
    }


    private Sprite ResolveRemoteIcon(string iconUrl)
    {
        var cachedRemote = RemoteSpriteCache.GetCached(iconUrl);
        if (cachedRemote != null)
            return cachedRemote;

        if (!string.IsNullOrWhiteSpace(iconUrl))
        {
            RemoteSpriteCache.Load(this, iconUrl, sprite =>
            {
                if (sprite != null && isActiveAndEnabled && _summary != null)
                    RefreshCurrentTab();
            });
        }

        return null;
    }


    private Sprite ResolveSkinPrefabIcon(int skinId)
    {
        if (skinId <= 0)
            return null;

        if (_skinDatabase == null)
            _skinDatabase = SkinDatabaseSO.LoadDefault();

        if (_skinDatabase != null && _skinDatabase.TryGetPreviewSprite(skinId, out var previewSprite))
            return previewSprite;

        return null;
    }

    private bool IsSkinForAnotherClass(int skinId, string skinName, string playerClass)
    {
        if (string.IsNullOrWhiteSpace(playerClass))
            return false;

        string pClassClean = playerClass.Trim();

        if (_skinDatabase == null)
            _skinDatabase = SkinDatabaseSO.LoadDefault();

        if (_skinDatabase != null && _skinDatabase.TryGetSkinData(skinId, out var skinData))
        {
            string skinClassStr = skinData.characterClass.ToString();
            if (!string.Equals(skinClassStr, pClassClean, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(skinName))
        {
            string nameClean = skinName.Trim();
            bool isDefault = nameClean.IndexOf("Default", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isDefault)
            {
                if (nameClean.IndexOf("Knight", System.StringComparison.OrdinalIgnoreCase) >= 0 && !pClassClean.Equals("Knight", System.StringComparison.OrdinalIgnoreCase))
                    return true;
                if (nameClean.IndexOf("Archer", System.StringComparison.OrdinalIgnoreCase) >= 0 && !pClassClean.Equals("Archer", System.StringComparison.OrdinalIgnoreCase))
                    return true;
                if (nameClean.IndexOf("Mage", System.StringComparison.OrdinalIgnoreCase) >= 0 && !pClassClean.Equals("Mage", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
