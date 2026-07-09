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

    [Header("Tab Highlights (tuỳ chọn)")]
    [SerializeField] private GameObject tabItemsHighlight;
    [SerializeField] private GameObject tabSkinsHighlight;

    [Header("Stats Labels (tuỳ chọn)")]
    [SerializeField] private TMP_Text totalItemsText;
    [SerializeField] private TMP_Text totalSkinsText;
    [SerializeField] private TMP_Text bagCapacityText;

    [Header("Player Avatar")]
    [SerializeField] private Image playerAvatarImage;
    [SerializeField] private Sprite knightIdleSprite;
    [SerializeField] private Sprite archerIdleSprite;
    [SerializeField] private Sprite mageIdleSprite;

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
    private float _lastLoadedAt = -999f;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        BindUiReferences();
        BindEvents();
        ShowTab(_showingSkins);
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
                if (eq.ItemType == newItem.ItemType)
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
                LoadInventory(force: true);
            },
            onError: error =>
            {
                Debug.LogError($"[InventoryManager] ❌ UnequipSkin FAIL: {error.Message}");
                ShowActionError($"Unequip skin thất bại: {error.Message}");
            }
        );
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

        if (itemFilterBar) itemFilterBar.SetActive(!showSkins);
        if (skinFilterBar) skinFilterBar.SetActive(showSkins);

        if (uiInventory) uiInventory.gameObject.SetActive(!showSkins);
        if (uiSkinInventory) uiSkinInventory.gameObject.SetActive(showSkins);

        RefreshCurrentTab();
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
        RefreshCurrentTab();
    }

    public void CycleSort()
    {
        _currentSortIndex = (_currentSortIndex + 1) % 3;
        UpdateSortButtonText();
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

            // Lọc skin
            var filteredSkins = new List<PlayerSkinSummaryResponse>();
            foreach (var skin in skins)
            {
                bool isOwned = skin.PlayerSkinId > 0;
                
                if (_currentSkinFilter == "Owned" && !isOwned) continue;
                if (_currentSkinFilter == "Unowned" && isOwned) continue;
                
                filteredSkins.Add(skin);
            }

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
                        return !(it.ItemType == "Armor" || it.ItemType == "Helmet" || it.ItemType == "Gloves" || it.ItemType == "Boots" || it.ItemType == "Ring" || it.ItemType == "Necklace");
                    }
                    if (_currentFilter == "Other")
                    {
                        return it.ItemType == "Weapon" || it.ItemType == "Armor" || it.ItemType == "Helmet" || it.ItemType == "Gloves" || it.ItemType == "Boots" || it.ItemType == "Ring" || it.ItemType == "Necklace" || it.ItemType == "Consumable" || it.ItemType == "Material" || it.ItemType == "QuestItem";
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

        tabItemsButton = tabItemsButton != null ? tabItemsButton : FindButton("TabItemsButton", "ItemsButton", "ItemTabButton");
        tabSkinsButton = tabSkinsButton != null ? tabSkinsButton : FindButton("TabSkinsButton", "SkinsButton", "SkinTabButton");
        tabItemsHighlight = tabItemsHighlight != null ? tabItemsHighlight : FindObject("TabItemsHighlight", "ItemsHighlight", "ItemTabHighlight");
        tabSkinsHighlight = tabSkinsHighlight != null ? tabSkinsHighlight : FindObject("TabSkinsHighlight", "SkinsHighlight", "SkinTabHighlight");

        if (loadingIndicator == null)
            loadingIndicator = FindObject("LoadingIndicator", "Loading", "Spinner");
        if (errorText == null)
            errorText = FindText("ErrorText", "MessageText", "StatusText");

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

        var btnFilterAll = FindButton("BtnFilterAll", "AllButton");
        var btnFilterWeapon = FindButton("BtnFilterWeapon", "WeaponButton");
        var btnFilterArmor = FindButton("BtnFilterArmor", "ArmorButton");
        var btnFilterConsumable = FindButton("BtnFilterConsumable", "PotionButton", "ConsumableButton");
        var btnFilterMaterial = FindButton("BtnFilterMaterial", "MaterialButton");
        var btnFilterQuest = FindButton("BtnFilterQuest", "QuestButton");
        var btnFilterOther = FindButton("BtnFilterOther", "OtherButton");
        var btnSort = FindButton("BtnSort", "SortButton", "OptionA");
        _sortDropdown = FindDropdown("BtnSort", "SortButton", "OptionA");

        var btnSkinFilterAll = FindButton("BtnSkinFilterAll");
        var btnSkinFilterOwned = FindButton("BtnSkinFilterOwned");
        var btnSkinFilterUnowned = FindButton("BtnSkinFilterUnowned");

        if (btnFilterAll) btnFilterAll.onClick.AddListener(() => SetFilter("All"));
        if (btnFilterWeapon) btnFilterWeapon.onClick.AddListener(() => SetFilter("Weapon"));
        if (btnFilterArmor) btnFilterArmor.onClick.AddListener(() => SetFilter("Armor"));
        if (btnFilterConsumable) btnFilterConsumable.onClick.AddListener(() => SetFilter("Consumable"));
        if (btnFilterMaterial) btnFilterMaterial.onClick.AddListener(() => SetFilter("Material"));
        if (btnFilterQuest) btnFilterQuest.onClick.AddListener(() => SetFilter("QuestItem"));
        if (btnFilterOther) btnFilterOther.onClick.AddListener(() => SetFilter("Other"));
        
        if (btnSkinFilterAll) btnSkinFilterAll.onClick.AddListener(() => SetFilter("All"));
        if (btnSkinFilterOwned) btnSkinFilterOwned.onClick.AddListener(() => SetFilter("Owned"));
        if (btnSkinFilterUnowned) btnSkinFilterUnowned.onClick.AddListener(() => SetFilter("Unowned"));

        if (btnSort) btnSort.onClick.AddListener(CycleSort);
        if (_sortDropdown) _sortDropdown.onValueChanged.AddListener(SetSortIndex);

        _eventsBound = uiInventory != null || itemDetailPopup != null || skinDetailPopup != null || tabItemsButton != null || tabSkinsButton != null;
    }

    private Sprite ResolveIcon(int itemId, string iconUrl, string itemName = null, string itemType = null)
    {
        // 1. Local database: lookup by name → type (no longer fragile itemId)
        if (ItemIconDatabase.Instance != null)
        {
            var localIcon = ItemIconDatabase.Instance.GetIcon(itemName, itemType);
            if (localIcon != null) return localIcon;
        }

        // 2. Remote URL cache
        var cachedRemote = RemoteSpriteCache.GetCached(iconUrl);
        if (cachedRemote != null)
            return cachedRemote;

        // 3. Kick off remote load (result arrives async → refreshes tab)
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

    private Button FindButton(params string[] names)
    {
        var obj = FindObject(names);
        return obj == null ? null : obj.GetComponent<Button>();
    }

    private TMP_Text FindText(params string[] names)
    {
        var obj = FindObject(names);
        return obj == null ? null : obj.GetComponent<TMP_Text>();
    }

    private GameObject FindObject(params string[] names)
    {
        List<Transform> roots = new List<Transform>();
        roots.Add(transform);
        if (itemFilterBar != null) roots.Add(itemFilterBar.transform);
        if (skinFilterBar != null) roots.Add(skinFilterBar.transform);
        if (uiInventory != null) roots.Add(uiInventory.transform);
        if (uiSkinInventory != null) roots.Add(uiSkinInventory.transform);
        if (itemDetailPopup != null) roots.Add(itemDetailPopup.transform);
        if (skinDetailPopup != null) roots.Add(skinDetailPopup.transform);

        foreach (var root in roots)
        {
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
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private void UpdateStatsDisplay()
    {
        if (_summary == null) return;
        if (totalItemsText) totalItemsText.text = $"Items: {_summary.TotalItems}";
        if (totalSkinsText) totalSkinsText.text = $"Skins: {_summary.TotalSkins}";
        if (bagCapacityText) bagCapacityText.text = $"Bag: {(_summary.BagItems?.Length ?? 0)}/{_summary.BagCapacity}";
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
            var labelText = row.Find("Text (TMP)")?.GetComponent<TMP_Text>();
            var valueText = row.Find("ValueText")?.GetComponent<TMP_Text>();

            if (labelText != null) 
                labelText.text = label;
            else 
                Debug.LogWarning($"[InventoryManager] Text (TMP) not found in {rowName}");

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
        if (playerAvatarImage == null) return;
        playerAvatarImage.preserveAspect = true;

        string pClass = MysticJourney.Core.Services.GameStateService.Instance.PlayerClass;
        if (pClass == "Knight" && knightIdleSprite != null)
            playerAvatarImage.sprite = knightIdleSprite;
        else if (pClass == "Archer" && archerIdleSprite != null)
            playerAvatarImage.sprite = archerIdleSprite;
        else if (pClass == "Mage" && mageIdleSprite != null)
            playerAvatarImage.sprite = mageIdleSprite;
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
        return IsItemType(item, "Consumable");
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
}
