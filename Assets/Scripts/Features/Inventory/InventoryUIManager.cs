using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

// Executes core business logic for mono behaviour.
public class InventoryUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private InventoryPanel uiInventory;
    [SerializeField] private UISkinInventory uiSkinInventory;
    [SerializeField] private UIItemDetailPopup itemDetailPopup;
    [SerializeField] private UISkinDetailPopup skinDetailPopup;

    [Header("Filter Bars")]
    [SerializeField] private GameObject itemFilterBar;
    [SerializeField] private GameObject skinFilterBar;
    [SerializeField] private Toggle btnSkinFilterUnowned;

    [Header("Tab Buttons (tuỳ chọn)")]
    [SerializeField] private Button tabItemsButton;
    [SerializeField] private Button tabSkinsButton;
    [SerializeField] private Toggle tabItemsToggle;
    [SerializeField] private Toggle tabSkinsToggle;

    [Header("Tab Highlights (tuỳ chọn)")]
    [SerializeField] private GameObject tabItemsHighlight;
    [SerializeField] private GameObject tabSkinsHighlight;

    [Header("Active Filter/Tab Sprites")]
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

    private InventorySummaryResponse _summary;
    private string _currentFilter = "All";
    private string _currentSkinFilter = "All";
    private bool _showingSkins = false;
    private bool _requestInFlight;
    private bool _eventsBound;

    private SkinDatabaseSO _skinDatabase;
    private float _lastLoadedAt = -999f;

    private readonly Dictionary<string, Image> _filterGraphics = new Dictionary<string, Image>();
    private readonly Dictionary<string, Sprite> _filterNormalSprites = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Sprite> _emptyEquipSlotIcons = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);
    private Sprite _tabItemsNormalSprite;
    private Sprite _tabSkinsNormalSprite;
    private bool _tabNormalSpritesCached;

    // Executes core business logic for filter key.
    private static string FilterKey(bool isSkinFilter, string filterValue)
        => (isSkinFilter ? "skin:" : "item:") + filterValue;

    // Initializes references, attaches UI hover animations, and opens default item tab.
    private void Awake()
    {
        BindUiReferences(); // Cache child transforms and slot views
        CacheEmptyEquipSlotIcons(); // Preserve the designed slot-type icons before item data can replace them
        BindEvents(); // Subscribe slot click and filter toggle events
        AddHoverEffects(); // Attach scale hover script to selectables
        ShowTab(_showingSkins); // Display Item or Skin inventory tab
    }

    // Executes core business logic for add hover effects.
    private void AddHoverEffects()
    {
        foreach (var root in CollectSearchRoots())
        {
            if (root == null) continue;

            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable == null) continue;
                if (!(selectable is Button || selectable is Toggle)) continue;
                if (selectable.GetComponentInParent<TMP_Dropdown>(true) != null) continue;
                if (selectable.name == "DimBackground") continue;
                if (selectable.GetComponent<UIHoverScaleEffect>() == null)
                    selectable.gameObject.AddComponent<UIHoverScaleEffect>();
            }
        }
    }

    // Refreshes item slots, skin unlocks, and combat stats upon modal opening.
    private void OnEnable()
    {
        LoadInventory(); // Query player inventory snapshot
    }

    // Static helper to trigger inventory reload from any game system.
    public static void RefreshAny(bool refreshStats = false)
    {
        var manager = UnityEngine.Object.FindFirstObjectByType<InventoryUIManager>(FindObjectsInactive.Include);
        manager?.LoadInventory(force: true, refreshStats: refreshStats); // Force bypass cache and reload
    }

    // Loads bag items, equipped gear, skins, and character attributes from REST API.
    public void LoadInventory(bool force = false, bool refreshStats = true)
    {
        BindUiReferences();
        BindEvents();
        UpdatePlayerAvatar(); // Set knight/archer/mage portrait

        if (force)
        {
            _requestInFlight = false;
        }

        if (_requestInFlight)
            return; // Ignore concurrent requests

        if (!force && _summary != null && Time.unscaledTime - _lastLoadedAt < cacheSeconds)
        {
            RefreshCurrentTab(); // Reuse cached inventory if within TTL
            if (refreshStats)
                LoadPlayerStats();
            return;
        }

        _requestInFlight = true;
        SetLoading(true);
        SetError(null);
        itemDetailPopup?.Hide();

        if (refreshStats)
            LoadPlayerStats(); // Fetch effective character attributes

        InventoryApi.Instance.GetInventory(
            onSuccess: response =>
            {
                _requestInFlight = false;
                SetLoading(false);
                if (response == null)
                {
                    SetError("Failed to load inventory data.");
                    return;
                }

                _summary = response; // Cache bag and skin summary
                _lastLoadedAt = Time.unscaledTime;
                UpdateStatsDisplay(); // Update bag capacity label
                UpdatePlayerAvatar();
                RefreshCurrentTab(); // Render active grid
            },
            onError: error =>
            {
                _requestInFlight = false;
                SetLoading(false);
                SetError($"Failed to load inventory: {error.Message}");
                Debug.LogError($"[InventoryUIManager] LoadInventory FAIL: {error.Message}");
            }
        );
    }

    // Handles slot selection, opening item details or gear comparison popup.
    private void HandleSlotClicked(UIBaseItemSlot slot)
    {
        if (_requestInFlight) return;
        if (slot?.RawData == null) return;

        if (!_showingSkins && slot.RawData is InventoryItemResponse item)
        {
            Sprite icon = ResolveIcon(item.ItemId, item.IconUrl, item.ItemName, item.ItemType); // Look up sprite icon

            int slotQty = (slot.DisplayData != null && slot.DisplayData.quantity > 0) ? slot.DisplayData.quantity : 99;

            if (IsEquipment(item) && !item.IsEquipped)
            {
                InventoryItemResponse oldItem = FindEquippedItemForSameSlot(item);
                if (oldItem != null)
                {
                    Sprite oldIcon = ResolveIcon(oldItem.ItemId, oldItem.IconUrl, oldItem.ItemName, oldItem.ItemType);
                    itemDetailPopup?.ShowEquipComparison(item, icon, slotQty, oldItem, oldIcon); // Show stat comparison vs currently equipped gear
                    return;
                }
            }

            itemDetailPopup?.Show(item, icon, slotQty); // Show item action popup (Equip, Unequip, Use)
            return;
        }

        if (_showingSkins && slot.RawData is PlayerSkinSummaryResponse skin)
        {
            Sprite icon = ResolveIcon(skin.SkinId, skin.IconUrl);
            skinDetailPopup?.ShowSkinDetails(skin, icon);
        }
    }

    // Executes core business logic for find equipped item for same slot.
    private InventoryItemResponse FindEquippedItemForSameSlot(InventoryItemResponse newItem)
    {
        if (_summary?.EquippedItems == null || newItem == null) return null;

        foreach (var eq in _summary.EquippedItems)
        {
            if (eq != null && eq.IsEquipped && IsSameEquipSlot(eq, newItem))
            {
                return eq;
            }
        }
        return null;
    }

    // Executes core business logic for handle equip item.
    private void HandleEquipItem(InventoryItemResponse item)
    {
        if (!CanEquipItem(item))
        {
            ShowActionError("Quest items cannot be equipped.");
            return;
        }

        Debug.Log($"[InventoryUIManager] EquipItem inventoryItemId={item.InventoryItemId}");
        itemDetailPopup?.Hide();

        InventoryApi.Instance.EquipItem(
            inventoryItemId: item.InventoryItemId,
            onSuccess: response =>
            {
                Debug.Log($"[InventoryUIManager] ✅ EquipItem OK");
                LoadInventory(force: true, refreshStats: true);
            },
            onError: error =>
            {
                Debug.LogError($"[InventoryUIManager] ❌ EquipItem FAIL: {error.Message}");
                ShowActionError(!string.IsNullOrEmpty(error.Message) ? error.Message : "Equip failed.");
            }
        );
    }

    // Executes core business logic for handle equip initiated.
    private void HandleEquipInitiated(InventoryItemResponse newItem)
    {
        if (!CanEquipItem(newItem))
        {
            ShowActionError("Quest items are only used for requests.");
            return;
        }

        InventoryItemResponse oldItem = FindEquippedItemForSameSlot(newItem);
        if (oldItem == null)
        {
            HandleEquipItem(newItem);
        }
        else
        {
            Sprite newIcon = ResolveIcon(newItem.ItemId, newItem.IconUrl, newItem.ItemName, newItem.ItemType);
            Sprite oldIcon = ResolveIcon(oldItem.ItemId, oldItem.IconUrl, oldItem.ItemName, oldItem.ItemType);
            itemDetailPopup?.ShowEquipComparison(newItem, newIcon, newItem.Quantity, oldItem, oldIcon);
        }
    }

    // Executes core business logic for is same equip slot.
    // Logic details: validates required non-empty string arguments.
    // Returns a boolean indicating operation success.
    private static bool IsSameEquipSlot(InventoryItemResponse a, InventoryItemResponse b)
    {
        if (a == null || b == null) return false;

        string slotA = GetEquipSlotCategory(a);
        string slotB = GetEquipSlotCategory(b);

        if (!string.IsNullOrEmpty(slotA) && !string.IsNullOrEmpty(slotB))
            return string.Equals(slotA, slotB, System.StringComparison.OrdinalIgnoreCase);

        return false;
    }

    // Executes core business logic for get equip slot category.
    private static string GetEquipSlotCategory(InventoryItemResponse item)
    {
        if (item == null) return string.Empty;

        foreach (var (slotObject, slotKeys) in EquipSlotMap)
        {
            foreach (var key in slotKeys)
            {
                if (Matches(item.EquippedSlot, key) || Matches(item.ItemSlot, key))
                    return slotObject;
            }
        }

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

    // Executes core business logic for handle unequip item.
    private void HandleUnequipItem(InventoryItemResponse item)
    {
        if (!CanEquipItem(item))
        {
            ShowActionError("This item cannot be unequipped.");
            return;
        }

        Debug.Log($"[InventoryUIManager] UnequipItem inventoryItemId={item.InventoryItemId}");
        itemDetailPopup?.Hide();

        InventoryApi.Instance.UnequipItem(
            inventoryItemId: item.InventoryItemId,
            onSuccess: response =>
            {
                Debug.Log($"[InventoryUIManager] ✅ UnequipItem OK");
                LoadInventory(force: true, refreshStats: true);
            },
            onError: error =>
            {
                Debug.LogError($"[InventoryUIManager] ❌ UnequipItem FAIL: {error.Message}");
                ShowActionError(!string.IsNullOrEmpty(error.Message) ? error.Message : "Unequip failed.");
            }
        );
    }

    // Executes core business logic for refresh inventory after equipment mutation.
    // Logic details: validates required non-empty string arguments; validates numeric boundary constraints.
    private IEnumerator RefreshInventoryAfterEquipmentMutation()
    {
        yield return new WaitForSecondsRealtime(0.15f);
        LoadInventory(force: true, refreshStats: true);
    }

    // Executes core business logic for handle consume item.
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

        Debug.Log($"[InventoryUIManager] ConsumeItem inventoryItemId={item.InventoryItemId} qty={quantity}");
        itemDetailPopup?.Hide();

        InventoryApi.Instance.ConsumeItem(
            inventoryItemId: item.InventoryItemId,
            quantity: quantity,
            onSuccess: response =>
            {
                Debug.Log($"[InventoryUIManager] ✅ ConsumeItem OK | effect={response?.EffectType} | value={response?.EffectValue}");
                ApplyConsumedItemEffect(response);
                LoadInventory(force: true, refreshStats: false);
            },
            onError: error =>
            {
                Debug.LogError($"[InventoryUIManager] ❌ ConsumeItem FAIL: {error.Message}");
                ShowActionError(!string.IsNullOrEmpty(error.Message) ? error.Message : "Item use failed.");
            }
        );
    }

    // Executes core business logic for apply consumed item effect.
    private void ApplyConsumedItemEffect(ConsumeItemResultResponse response)
    {
        if (response == null)
        {
            LoadPlayerStats();
            return;
        }

        if (response.CurrentHp.HasValue && response.MaxHp.HasValue)
        {
            if (PlayerEntity.Instance != null)
            {
                PlayerEntity.Instance.ApplyHealth(response.CurrentHp.Value, response.MaxHp.Value);
            }
            else
            {
                PlayerHUDUIManager.Instance?.ApplyHealth(response.CurrentHp.Value, response.MaxHp.Value);
            }
        }

        if (response.CurrentEnergy.HasValue && response.MaxEnergy.HasValue)
        {
            PlayerHUDUIManager.Instance?.ApplyEnergy(response.CurrentEnergy.Value, response.MaxEnergy.Value);
        }

        if (response.CorruptionLevel.HasValue)
        {
            MysticJourney.Core.Services.GameStateService.Instance.CorruptionLevel = response.CorruptionLevel.Value;
            PlayerHUDUIManager.Instance?.ApplyCorruption(response.CorruptionLevel.Value);
        }
    }

    // Executes core business logic for handle equip skin.
    private void HandleEquipSkin(PlayerSkinSummaryResponse skin)
    {
        Debug.Log($"[InventoryUIManager] EquipSkin playerSkinId={skin.PlayerSkinId} skinName={skin.SkinName}");
        skinDetailPopup?.Hide();

        InventoryApi.Instance.EquipSkin(
            playerSkinId: skin.PlayerSkinId,
            onSuccess: response =>
            {
                Debug.Log($"[InventoryUIManager] ✅ EquipSkin OK | SkinName={response?.SkinName}");
                UpdateLocalNetworkPlayerSkin(skin.SkinId);
                LoadInventory(force: true);
            },
            onError: error =>
            {
                Debug.LogError($"[InventoryUIManager] ❌ EquipSkin FAIL: {error.Message}");
                ShowActionError(!string.IsNullOrEmpty(error.Message) ? error.Message : "Equip skin failed.");
            }
        );
    }

    // Executes core business logic for handle unequip skin.
    private void HandleUnequipSkin(PlayerSkinSummaryResponse skin)
    {
        Debug.Log($"[InventoryUIManager] UnequipSkin playerSkinId={skin.PlayerSkinId} skinName={skin.SkinName}");
        skinDetailPopup?.Hide();

        InventoryApi.Instance.UnequipSkin(
            playerSkinId: skin.PlayerSkinId,
            onSuccess: _ =>
            {
                Debug.Log($"[InventoryUIManager] ✅ UnequipSkin OK");
                UpdateLocalNetworkPlayerSkin(0);
                LoadInventory(force: true);
            },
            onError: error =>
            {
                Debug.LogError($"[InventoryUIManager] ❌ UnequipSkin FAIL: {error.Message}");
                ShowActionError(!string.IsNullOrEmpty(error.Message) ? error.Message : "Unequip skin failed.");
            }
        );
    }

    // Executes core business logic for update local network player skin.
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

    // Executes core business logic for show tab.
    private void ShowTab(bool showSkins)
    {
        _showingSkins = showSkins;
        itemDetailPopup?.Hide();

        if (tabItemsHighlight) tabItemsHighlight.SetActive(!showSkins);
        if (tabSkinsHighlight) tabSkinsHighlight.SetActive(showSkins);

        UpdateTabHighlights();

        if (itemFilterBar) itemFilterBar.SetActive(!showSkins);
        if (skinFilterBar) skinFilterBar.SetActive(showSkins);

        if (uiInventory) uiInventory.gameObject.SetActive(!showSkins);
        if (uiSkinInventory) uiSkinInventory.gameObject.SetActive(showSkins);

        UpdateFilterHighlights();
        RefreshCurrentTab();
    }

    // Executes core business logic for apply sprite.
    private static void ApplySprite(Image graphic, Sprite sprite)
    {
        if (graphic == null || sprite == null) return;
        if (graphic.sprite != sprite) graphic.sprite = sprite;
    }

    // Executes core business logic for sync toggle.
    private static void SyncToggle(Image graphic, bool active)
    {
        if (graphic == null) return;
        var toggle = graphic.GetComponent<Toggle>();
        if (toggle != null && toggle.isOn != active)
            toggle.SetIsOnWithoutNotify(active);
    }

    // Executes core business logic for update tab highlights.
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

    // Executes core business logic for apply tab visual.
    private void ApplyTabVisual(Toggle tab, bool active, Sprite normalSprite)
    {
        if (tab == null) return;

        var graphic = tab.targetGraphic as Image;
        ApplySprite(graphic, active ? tabActiveSprite : normalSprite);

        if (tab.isOn != active) tab.SetIsOnWithoutNotify(active);
    }

    // Executes core business logic for set filter.
    public void SetFilter(string filterType)
    {
        SetFilter(filterType, _showingSkins);
    }

    // Executes core business logic for set filter.
    private void SetFilter(string filterType, bool isSkinFilter)
    {
        if (isSkinFilter)
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

    // Executes core business logic for refresh current tab.
    private void RefreshCurrentTab()
    {
        if (_summary == null) return;

        var displayList = new List<UIItemDisplayData>();

        if (_showingSkins)
        {
            if (uiSkinInventory == null) return;
            var skins = _summary.PlayerSkins;
            if (skins == null) { uiSkinInventory.Refresh(displayList); return; }

            string pClass = MysticJourney.Core.Services.GameStateService.Instance?.PlayerClass;

            var filteredSkins = new List<PlayerSkinSummaryResponse>();
            foreach (var skin in skins)
            {
                bool isOwned = skin.PlayerSkinId > 0 || IsDefaultSkin(skin.SkinId, skin.SkinName);

                if (_currentSkinFilter == "Owned" && !isOwned) continue;
                if (_currentSkinFilter == "Unowned" && isOwned) continue;

                if (IsSkinForAnotherClass(skin.SkinId, skin.SkinName, pClass)) continue;

                filteredSkins.Add(skin);
            }

            bool anySkinEquipped = false;
            foreach (var skin in filteredSkins)
                if (skin.IsEquipped) { anySkinEquipped = true; break; }

            foreach (var skin in filteredSkins)
            {
                Sprite icon = ResolveIcon(skin.SkinId, skin.IconUrl);
                bool isDefaultSkin = IsDefaultSkin(skin.SkinId, skin.SkinName);
                bool owned = skin.PlayerSkinId > 0 || isDefaultSkin;
                bool equipped = skin.IsEquipped || (isDefaultSkin && !anySkinEquipped);

                displayList.Add(new UIItemDisplayData
                {
                    itemId = skin.PlayerSkinId > 0 ? skin.PlayerSkinId : (owned ? -1 : 0),
                    itemName = skin.SkinName,
                    icon = icon,
                    quantity = 1,
                    rarity = skin.SkinRarity,
                    isEquipped = equipped,
                    rawData = skin
                });
            }
            uiSkinInventory.Refresh(displayList);
        }
        else
        {
            if (uiInventory == null) return;
            var allItems = BuildInventoryItemDisplaySource(_summary);

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

            const int MaxStackSize = 99;
            foreach (var item in allItems)
            {
                Sprite icon = ResolveIcon(item.ItemId, item.IconUrl, item.ItemName, item.ItemType);
                int remaining = Mathf.Max(1, item.Quantity);
                while (remaining > 0)
                {
                    int stackQty = Mathf.Min(remaining, MaxStackSize);
                    displayList.Add(new UIItemDisplayData
                    {
                        itemId = item.InventoryItemId,
                        itemName = item.ItemName,
                        icon = icon,
                        quantity = item.IsEquipped ? 1 : stackQty,
                        rarity = item.ItemRarity,
                        isEquipped = item.IsEquipped && CanEquipItem(item),
                        rawData = item
                    });
                    remaining -= stackQty;
                }
            }
            uiInventory.Refresh(displayList);
        }
    }

    // Builds the grid source from both API collections. Equipped records stay
    // separate from bag stacks so they retain their green tick and can be clicked to unequip.
    private static List<InventoryItemResponse> BuildInventoryItemDisplaySource(InventorySummaryResponse summary)
    {
        var allItems = new List<InventoryItemResponse>();

        if (summary?.EquippedItems != null)
        {
            foreach (var item in summary.EquippedItems)
            {
                if (!ShouldShowInventoryItem(item)) continue;
                allItems.Add(CloneInventoryItem(item, 1));
            }
        }

        if (summary?.BagItems == null)
            return allItems;

        var bagGroups = new Dictionary<string, InventoryItemResponse>();
        foreach (var item in summary.BagItems.OrderByDescending(x => x.Quantity))
        {
            if (!ShouldShowInventoryItem(item)) continue;

            string key = $"{item.ItemId}_{item.EnhancementLevel}";
            if (bagGroups.TryGetValue(key, out var existing))
            {
                existing.Quantity += Mathf.Max(1, item.Quantity);
            }
            else
            {
                bagGroups[key] = CloneInventoryItem(item, Mathf.Max(1, item.Quantity));
            }
        }

        allItems.AddRange(bagGroups.Values);
        return allItems;
    }

    private static InventoryItemResponse CloneInventoryItem(InventoryItemResponse item, int quantity)
    {
        return new InventoryItemResponse
        {
            InventoryItemId = item.InventoryItemId,
            PlayerProfileId = item.PlayerProfileId,
            ItemId = item.ItemId,
            ItemName = item.ItemName,
            ItemDescription = item.ItemDescription,
            ItemType = item.ItemType,
            ItemRarity = item.ItemRarity,
            ItemSlot = item.ItemSlot,
            Quantity = quantity,
            IsEquipped = item.IsEquipped,
            IsSkin = item.IsSkin,
            EnhancementLevel = item.EnhancementLevel,
            EquippedSlot = item.EquippedSlot,
            CreatedAt = item.CreatedAt,
            IconUrl = item.IconUrl,
            CorruptionReduction = item.CorruptionReduction,
            BaseHp = item.BaseHp,
            BaseAtk = item.BaseAtk,
            BaseDef = item.BaseDef,
            BonusHp = item.BonusHp,
            BonusAtk = item.BonusAtk,
            BonusDef = item.BonusDef,
            BonusCritRate = item.BonusCritRate,
            BonusCritDamage = item.BonusCritDamage
        };
    }

    // Executes core business logic for bind ui references.
    private void BindUiReferences()
    {
        if (uiInventory == null)
            uiInventory = GetComponentInChildren<InventoryPanel>(true);
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
                itemDetailPopup = UnityEngine.Object.FindFirstObjectByType<UIItemDetailPopup>(FindObjectsInactive.Include);
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
                skinDetailPopup = UnityEngine.Object.FindFirstObjectByType<UISkinDetailPopup>(FindObjectsInactive.Include);
#endif
            }
        }

        tabItemsButton = tabItemsButton != null ? tabItemsButton : FindButton("TabItemsButton", "ItemsButton", "ItemTabButton", "EquipmentTab");
        tabSkinsButton = tabSkinsButton != null ? tabSkinsButton : FindButton("TabSkinsButton", "SkinsButton", "SkinTabButton", "AppearanceTab");

        if (tabItemsToggle == null) tabItemsToggle = FindToggle("EquipmentTab", "TabItemsToggle", "ItemTabToggle");
        if (tabSkinsToggle == null) tabSkinsToggle = FindToggle("AppearanceTab", "TabSkinsToggle", "SkinTabToggle");
        if (btnSkinFilterUnowned == null) btnSkinFilterUnowned = FindToggle("BtnSkinFilterUnowned");
        tabItemsHighlight = tabItemsHighlight != null ? tabItemsHighlight : FindObject("TabItemsHighlight", "ItemsHighlight", "ItemTabHighlight");
        tabSkinsHighlight = tabSkinsHighlight != null ? tabSkinsHighlight : FindObject("TabSkinsHighlight", "SkinsHighlight", "SkinTabHighlight");

        if (loadingIndicator == null)
            loadingIndicator = FindObject("LoadingIndicator", "Loading", "Spinner");
        if (errorText == null)
            errorText = FindText("ErrorText", "MessageText", "StatusText");

        if (battlePowerText == null)
            battlePowerText = FindText("BattlePowerText ", "BattlePowerText", "BattlePower", "PowerText");

    }

    // Executes core business logic for bind events.
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

        BindFilterAction("All", false, "BtnFilterAll", "AllButton");
        BindFilterAction("Weapon", false, "BtnFilterWeapon", "WeaponButton");
        BindFilterAction("Armor", false, "BtnFilterArmor", "ArmorButton");
        BindFilterAction("Consumable", false, "BtnFilterConsumable", "PotionButton", "ConsumableButton");
        BindFilterAction("Material", false, "BtnFilterMaterial", "MaterialButton");
        BindFilterAction("QuestItem", false, "BtnFilterQuest", "QuestButton");

        BindFilterAction("All", true, "BtnSkinFilterAll");
        BindFilterAction("Owned", true, "BtnSkinFilterOwned");
        BindFilterAction("Unowned", true, btnSkinFilterUnowned, "BtnSkinFilterUnowned");

        _eventsBound = uiInventory != null || itemDetailPopup != null || skinDetailPopup != null || tabItemsButton != null || tabSkinsButton != null;
    }

    // Executes core business logic for resolve icon.
    // Logic details: validates required non-empty string arguments.
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

    // Executes core business logic for find button.
    private Button FindButton(params string[] names)
    {
        var obj = FindObject(names);
        return obj == null ? null : obj.GetComponent<Button>();
    }

    // Executes core business logic for find toggle.
    private Toggle FindToggle(params string[] names)
    {
        var obj = FindObject(names);
        return obj == null ? null : obj.GetComponent<Toggle>();
    }

    // Executes core business logic for bind filter action.
    private void BindFilterAction(string filterValue, bool isSkinFilter, params string[] names)
    {
        BindFilterAction(filterValue, isSkinFilter, null, names);
    }

    // Executes core business logic for bind filter action.
    private void BindFilterAction(string filterValue, bool isSkinFilter, Selectable preferredSelectable, params string[] names)
    {
        var obj = preferredSelectable != null ? preferredSelectable.gameObject : FindObject(names);
        if (obj == null)
        {
            Debug.LogWarning("[InventoryUIManager] BindFilterAction: Could not find object for " + filterValue);
            return;
        }

        var btn = obj.GetComponent<Button>();
        if (btn != null)
        {
            RegisterFilterVisual(FilterKey(isSkinFilter, filterValue), btn);
            btn.onClick.AddListener(() => {
                Debug.Log("[InventoryUIManager] Button clicked: " + filterValue);
                SetFilter(filterValue, isSkinFilter);
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
                    Debug.Log("[InventoryUIManager] Toggle selected: " + filterValue);
                    SetFilter(filterValue, isSkinFilter);
                }
                else
                {
                    UpdateFilterHighlights();
                }
            });
            return;
        }

        Debug.LogWarning("[InventoryUIManager] BindFilterAction: Object " + obj.name + " has neither Button nor Toggle for " + filterValue);
    }

    // Executes core business logic for register filter visual.
    private void RegisterFilterVisual(string key, Selectable selectable)
    {
        var graphic = selectable.targetGraphic as Image;
        if (graphic == null) graphic = selectable.GetComponent<Image>();
        if (graphic == null)
        {
            Debug.LogWarning("[InventoryUIManager] RegisterFilterVisual: no Image on " + selectable.name);
            return;
        }

        _filterGraphics[key] = graphic;
        if (graphic.sprite != filterActiveSprite) _filterNormalSprites[key] = graphic.sprite;
    }

    // Executes core business logic for update filter highlights.
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

    // Executes core business logic for find text.
    private TMP_Text FindText(params string[] names)
    {
        return FindComponent<TMP_Text>(names);
    }

    // Executes core business logic for component.
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

    // Executes core business logic for collect search roots.
    private List<Transform> CollectSearchRoots()
    {
        List<Transform> roots = new List<Transform>();
        roots.Add(transform);
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

    // Executes core business logic for find object.
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

    private Transform _panelRootCache;
    // Executes core business logic for resolve panel root.
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
    // Executes core business logic for update stats display.
    private void UpdateStatsDisplay()
    {
        if (_summary == null) return;
        if (totalItemsText) totalItemsText.text = $"Items: {_summary.TotalItems}";
        if (totalSkinsText) totalSkinsText.text = $"Skins: {_summary.TotalSkins}";
        if (bagCapacityText) bagCapacityText.text = $"Bag: {(_summary.BagItems?.Length ?? 0)}/{_summary.BagCapacity}";

        UpdateEquipmentSlots();
    }

    // Executes core business logic for set loading.
    // Logic details: validates required non-empty string arguments.
    private void SetLoading(bool isLoading)
    {
        if (loadingIndicator) loadingIndicator.SetActive(isLoading);
    }

    // Executes core business logic for set error.
    // Logic details: validates required non-empty string arguments.
    private void SetError(string msg)
    {
        if (errorText)
        {
            errorText.text = msg ?? string.Empty;
            errorText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
        }
    }

    // Executes core business logic for show action error.
    private void ShowActionError(string msg)
    {
        Debug.LogWarning($"[InventoryUIManager] Action error: {msg}");
        SetError(msg);

        UIPopupBox.Notify(transform, "Notice", msg);
    }

    // Executes core business logic for load player stats.
    private void LoadPlayerStats()
    {
        CharacterApi.Instance.GetMyStats(
            onSuccess: response =>
            {
                if (response != null)
                {
                    UpdatePlayerStatsUI(response);
                    PlayerHUDUIManager.Instance?.ApplyStats(response);
                    PlayerEntity.Instance?.ApplyHealth(response.CurrentHp, response.MaxHp);
                }
            },
            onError: error =>
            {
                Debug.LogWarning($"[InventoryUIManager] Failed to load character stats: {error.Message}");
            }
        );
    }

    // Executes core business logic for find stats panel.
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

    // Executes core business logic for update stat row.
    private void UpdateStatRow(Transform statsPanel, string rowName, string label, string value)
    {
        var row = statsPanel.Find(rowName);
        if (row != null)
        {
            var labelText = row.Find("Text (TMP)")?.GetComponent<TMP_Text>()
                         ?? row.Find("LabelText")?.GetComponent<TMP_Text>();
            var valueText = row.Find("ValueText")?.GetComponent<TMP_Text>();

            if (labelText != null)
                labelText.text = label;

            if (valueText != null)
            {
                valueText.textWrappingMode = TextWrappingModes.NoWrap;
                valueText.text = value;
            }
            else
                Debug.LogWarning($"[InventoryUIManager] ValueText not found or missing TMP_Text component in {rowName}");
        }
        else
        {
            Debug.LogWarning($"[InventoryUIManager] Row '{rowName}' not found in stats panel.");
        }
    }

    // Executes core business logic for update player stats ui.
    private void UpdatePlayerStatsUI(PlayerStatsResponse stats)
    {
        if (stats == null) return;

        UpdateBattlePower(stats);

        var statsPanel = FindStatsPanel();
        if (statsPanel == null)
        {
            Debug.LogWarning("[InventoryUIManager] StatsPanel not found.");
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

    // Executes core business logic for update player avatar.
    // Logic details: validates numeric boundary constraints.
    private void UpdatePlayerAvatar()
    {
        if (playerAvatarImage == null)
            playerAvatarImage = FindImage("Character", "PlayerAvatar", "AvatarImage");
        if (playerAvatarImage == null) return;

        playerAvatarImage.preserveAspect = true;

        string pClass = MysticJourney.Core.Services.GameStateService.Instance?.PlayerClass;
        Sprite sprite = null;

        if (_summary != null && _summary.PlayerSkins != null)
        {
            foreach (var skin in _summary.PlayerSkins)
            {
                if (skin == null || !skin.IsEquipped || skin.SkinId <= 0)
                    continue;
                if (IsSkinForAnotherClass(skin.SkinId, skin.SkinName, pClass))
                    continue;

                sprite = ResolveIcon(skin.SkinId, skin.IconUrl);
                if (sprite != null)
                    break;
            }
        }

        if (sprite == null)
            sprite = ResolveClassSprite(pClass);

        if (sprite != null)
            playerAvatarImage.sprite = sprite;

        bool hasSprite = playerAvatarImage.sprite != null;
        playerAvatarImage.enabled = hasSprite;
        if (hasSprite && !playerAvatarImage.gameObject.activeSelf)
            playerAvatarImage.gameObject.SetActive(true);

        if (!hasSprite)
            Debug.LogWarning($"[InventoryUIManager] No avatar sprite for class '{pClass}'. Assign knight/archer/mage idle sprites in the Inspector.");
    }

    // Executes core business logic for resolve class sprite.
    // Logic details: validates numeric boundary constraints.
    private Sprite ResolveClassSprite(string playerClass)
    {
        var c = (playerClass ?? string.Empty).Trim();
        if (c.Equals("Knight", System.StringComparison.OrdinalIgnoreCase)) return knightIdleSprite ?? ResolveClassSpriteFromDatabase(CharacterClass.Knight);
        if (c.Equals("Archer", System.StringComparison.OrdinalIgnoreCase)) return archerIdleSprite ?? ResolveClassSpriteFromDatabase(CharacterClass.Archer);
        if (c.Equals("Mage", System.StringComparison.OrdinalIgnoreCase)) return mageIdleSprite ?? ResolveClassSpriteFromDatabase(CharacterClass.Mage);
        return knightIdleSprite;
    }

    // Executes core business logic for resolve class sprite from database.
    // Logic details: validates numeric boundary constraints.
    private Sprite ResolveClassSpriteFromDatabase(CharacterClass characterClass)
    {
        if (_skinDatabase == null)
            _skinDatabase = SkinDatabaseSO.LoadDefault();
        if (_skinDatabase?.skinPrefabs == null)
            return null;

        Sprite fallback = null;
        foreach (var entry in _skinDatabase.skinPrefabs)
        {
            if (entry.characterClass != characterClass || entry.skinId <= 0)
                continue;
            if (!_skinDatabase.TryGetPreviewSprite(entry.skinId, out var sprite))
                continue;

            bool isDefault = entry.skinName != null &&
                entry.skinName.IndexOf("Default", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (isDefault)
                return sprite;

            fallback ??= sprite;
        }

        return fallback;
    }

    // Executes core business logic for readonly.
    private static readonly (string slotObject, string[] slotKeys)[] EquipSlotMap =
    {
        ("WeaponSlot",    new[] { "Weapon", "MainHand" }),
        ("HelmetSlot",    new[] { "Helmet", "Head" }),
        ("ArmorSlot",     new[] { "Armor", "Body", "Chest" }),
        ("GlovesSlot",    new[] { "Gloves", "Hands" }),
        ("BootsSlot",     new[] { "Boots", "Feet" }),
        ("PantsSlot",     new[] { "Pants", "Legs" }),
        ("RingSlot",      new[] { "Ring", "Accessory" }),
        ("NecklaceSlot",  new[] { "Necklace", "Shield", "OffHand" }),
    };

    // Caches the icon authored for each empty equipment slot so unequipping can
    // restore the slot type (weapon, helmet, armor, etc.) instead of hiding it.
    private void CacheEmptyEquipSlotIcons()
    {
        foreach (var (slotObject, slotKeys) in EquipSlotMap)
        {
            _ = slotKeys;
            if (_emptyEquipSlotIcons.ContainsKey(slotObject))
                continue;

            var iconImage = FindEquipSlotIcon(slotObject);
            if (iconImage != null && iconImage.sprite != null)
                _emptyEquipSlotIcons[slotObject] = iconImage.sprite;
        }
    }

    // Displays the slot-type placeholder while the slot is empty or while a
    // remote equipped-item icon is still loading.
    private void ShowEmptyEquipSlotIcon(string slotObject, Image iconImage)
    {
        if (_emptyEquipSlotIcons.TryGetValue(slotObject, out var emptyIcon) && emptyIcon != null)
        {
            iconImage.sprite = emptyIcon;
            iconImage.enabled = true;
            iconImage.preserveAspect = true;
            return;
        }

        iconImage.sprite = null;
        iconImage.enabled = false;
    }

    // Executes core business logic for update equipment slots.
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
                ShowEmptyEquipSlotIcon(slotObject, iconImage);
                SetEquipSlotRarity(slotObject, null, false);
                continue;
            }

            var icon = ResolveIcon(item.ItemId, item.IconUrl, item.ItemName, item.ItemType);
            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = true;
                iconImage.preserveAspect = true;
            }
            else
            {
                ShowEmptyEquipSlotIcon(slotObject, iconImage);
            }
            SetEquipSlotRarity(slotObject, item.ItemRarity, true);
        }
    }

    // Executes core business logic for set equip slot rarity.
    // Logic details: validates required non-empty string arguments.
    private void SetEquipSlotRarity(string slotObjectName, string rarity, bool visible)
    {
        // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
        var slot = FindEquipSlotObject(slotObjectName);
        if (slot == null)
            return;

        var effect = slot.GetComponent<UIRarityFrameEffect>();

        if (visible)
        {
            if (string.IsNullOrWhiteSpace(rarity))
                rarity = "Common";

            if (effect == null)
                effect = slot.AddComponent<UIRarityFrameEffect>();

            effect.Configure(rarity);

            var iconImg = FindEquipSlotIcon(slotObjectName);
            if (iconImg != null) iconImg.transform.SetAsLastSibling();
        }
        else
        {
            effect?.SetVisible(false);
        }

        var slotImage = slot.GetComponent<Image>();
        if (slotImage != null)
            slotImage.color = Color.white;
    }

    // Executes core business logic for find equip slot icon.
    private Image FindEquipSlotIcon(string slotObjectName)
    {
        // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
        var slot = FindEquipSlotObject(slotObjectName);
        if (slot == null) return null;

        var child = slot.transform.Find("Image");
        var image = child != null ? child.GetComponent<Image>() : null;
        return image != null ? image : slot.GetComponent<Image>();
    }

    // Executes core business logic for find equip slot object.
    private GameObject FindEquipSlotObject(string slotObjectName)
    {
        // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
        var slot = FindObject(slotObjectName);
        if (slot == null && slotObjectName == "RingSlot")
            slot = FindObject("AccessorySlot", "Ring");
        if (slot == null && slotObjectName == "NecklaceSlot")
            slot = FindObject("ShieldSlot", "Necklace");

        // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
        return slot;
    }

    // Executes core business logic for find equipped for slot.
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

    // Executes core business logic for matches.
    // Logic details: validates required non-empty string arguments.
    // Returns a boolean indicating operation success.
    private static bool Matches(string value, string key)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Trim().Equals(key, System.StringComparison.OrdinalIgnoreCase);
    }

    // Executes core business logic for update battle power.
    private void UpdateBattlePower(PlayerStatsResponse stats)
    {
        if (battlePowerText == null)
            battlePowerText = FindText("BattlePowerText ", "BattlePowerText", "BattlePower", "PowerText");
        if (battlePowerText == null || stats == null) return;

        battlePowerText.text = CalculateBattlePower(stats).ToString("N0");
    }

    // Executes core business logic for calculate battle power.
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

    // Executes core business logic for find image.
    private Image FindImage(params string[] names)
    {
        return FindComponent<Image>(names);
    }

    // Executes core business logic for should show inventory item.
    // Returns a boolean indicating operation success.
    private static bool ShouldShowInventoryItem(InventoryItemResponse item)
    {
        if (item == null || item.IsSkin)
            return false;

        if (item.Quantity > 0)
            return true;

        return item.IsEquipped && CanEquipItem(item);
    }

    // Executes core business logic for can equip item.
    // Returns a boolean indicating operation success.
    private static bool CanEquipItem(InventoryItemResponse item)
    {
        return IsEquipment(item);
    }

    // Executes core business logic for is consumable.
    // Returns a boolean indicating operation success.
    private static bool IsConsumable(InventoryItemResponse item)
    {
        return IsItemType(item, "Consumable") || (item != null && item.ItemName != null && item.ItemName.Contains("Lucky Ticket", System.StringComparison.OrdinalIgnoreCase));
    }

    // Executes core business logic for is equipment.
    // Returns a boolean indicating operation success.
    private static bool IsEquipment(InventoryItemResponse item)
    {
        return IsItemType(item, "Weapon") ||
               IsItemType(item, "Armor") ||
               IsItemType(item, "Accessory") ||
               IsItemType(item, "Helmet") ||
               IsItemType(item, "Gloves") ||
               IsItemType(item, "Boots") ||
               IsItemType(item, "Pants") ||
               IsItemType(item, "Ring") ||
               IsItemType(item, "Necklace") ||
               (item != null && !string.IsNullOrEmpty(item.ItemSlot) && !string.Equals(item.ItemSlot, "None", System.StringComparison.OrdinalIgnoreCase));
    }

    // Executes core business logic for is item type.
    // Logic details: validates required non-empty string arguments.
    // Returns a boolean indicating operation success.
    private static bool IsItemType(InventoryItemResponse item, string itemType)
    {
        return item != null &&
               string.Equals(item.ItemType, itemType, System.StringComparison.OrdinalIgnoreCase);
    }


    // Executes core business logic for resolve remote icon.
    // Logic details: validates required non-empty string arguments.
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


    // Executes core business logic for resolve skin prefab icon.
    // Logic details: validates required non-empty string arguments; validates numeric boundary constraints.
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

    // Executes core business logic for is default skin.
    // Logic details: validates required non-empty string arguments.
    // Returns a boolean indicating operation success.
    private bool IsDefaultSkin(int skinId, string skinName)
    {
        if (_skinDatabase == null)
            _skinDatabase = SkinDatabaseSO.LoadDefault();

        if (_skinDatabase != null && _skinDatabase.TryGetSkinData(skinId, out var skinData))
        {
            if (!string.IsNullOrWhiteSpace(skinData.skinName))
                return skinData.skinName.IndexOf("Default", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        return !string.IsNullOrWhiteSpace(skinName)
            && skinName.IndexOf("Default", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Executes core business logic for is skin for another class.
    // Logic details: validates required non-empty string arguments.
    // Returns a boolean indicating operation success.
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
