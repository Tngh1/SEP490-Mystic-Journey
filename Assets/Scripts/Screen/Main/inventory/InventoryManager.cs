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
//       - Equip Item       (UC 20.4)  – dùng InventoryItemId
//       - Unequip Item     (UC 20.5)  – dùng InventoryItemId
//       - Consume Item     (UC 20.3)  – dùng InventoryItemId
//       - Equip Skin       (UC 20.6)  – dùng PlayerSkinId (KHÁC InventoryItemId)
//       - Unequip Skin     (UC 20.7)  – dùng PlayerSkinId (KHÁC InventoryItemId)
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
    [SerializeField] private UIItemDetailPopup itemDetailPopup;

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

    [Header("State")]
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private TMP_Text errorText;

    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------
    private InventorySummaryResponse _summary;
    private bool _showingSkins = false;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        // Đăng ký sự kiện click slot từ UIInventory
        if (uiInventory != null)
            uiInventory.OnInventorySlotClicked += HandleSlotClicked;

        // Đăng ký sự kiện action từ popup
        if (itemDetailPopup != null)
        {
            itemDetailPopup.OnEquipClicked       += HandleEquipItem;
            itemDetailPopup.OnUnequipClicked     += HandleUnequipItem;
            itemDetailPopup.OnConsumeClicked     += HandleConsumeItem;
            itemDetailPopup.OnEquipSkinClicked   += HandleEquipSkin;
            itemDetailPopup.OnUnequipSkinClicked += HandleUnequipSkin;
        }

        // Tab buttons
        if (tabItemsButton) tabItemsButton.onClick.AddListener(() => ShowTab(false));
        if (tabSkinsButton) tabSkinsButton.onClick.AddListener(() => ShowTab(true));
    }

    private void OnEnable()
    {
        // Load inventory mỗi khi mở panel
        LoadInventory();
    }

    // =========================================================================
    // UC 20.1 – Load Inventory
    // =========================================================================
    public void LoadInventory()
    {
        SetLoading(true);
        SetError(null);
        itemDetailPopup?.Hide();

        InventoryApi.Instance.GetInventory(
            onSuccess: response =>
            {
                SetLoading(false);
                if (response?.Data == null)
                {
                    SetError("Không tải được dữ liệu inventory.");
                    return;
                }

                _summary = response.Data;
                UpdateStatsDisplay();
                RefreshCurrentTab();
            },
            onError: error =>
            {
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
        if (slot?.RawData == null) return;

        // Tab Items: rawData là InventoryItemResponse
        if (!_showingSkins && slot.RawData is InventoryItemResponse item)
        {
            Sprite icon = ItemIconDatabase.Instance?.GetIcon(item.ItemId);
            itemDetailPopup?.Show(item, icon);
            return;
        }

        // Tab Skins: rawData là PlayerSkinSummaryResponse
        if (_showingSkins && slot.RawData is PlayerSkinSummaryResponse skin)
        {
            Sprite icon = ItemIconDatabase.Instance?.GetIcon(skin.SkinId);
            itemDetailPopup?.ShowSkin(skin, icon);
        }
    }

    // =========================================================================
    // UC 20.4 – Equip Item (dùng InventoryItemId)
    // =========================================================================
    private void HandleEquipItem(InventoryItemResponse item)
    {
        Debug.Log($"[InventoryManager] EquipItem inventoryItemId={item.InventoryItemId}");

        InventoryApi.Instance.EquipItem(
            inventoryItemId: item.InventoryItemId,
            onSuccess: response =>
            {
                Debug.Log($"[InventoryManager] ✅ EquipItem OK");
                itemDetailPopup?.UpdateItemState(response.Data?.Item);
                LoadInventory();
            },
            onError: error =>
            {
                Debug.LogError($"[InventoryManager] ❌ EquipItem FAIL: {error.Message}");
                ShowActionError($"Equip thất bại: {error.Message}");
            }
        );
    }

    // =========================================================================
    // UC 20.5 – Unequip Item (dùng InventoryItemId)
    // =========================================================================
    private void HandleUnequipItem(InventoryItemResponse item)
    {
        Debug.Log($"[InventoryManager] UnequipItem inventoryItemId={item.InventoryItemId}");

        InventoryApi.Instance.UnequipItem(
            inventoryItemId: item.InventoryItemId,
            onSuccess: response =>
            {
                Debug.Log($"[InventoryManager] ✅ UnequipItem OK");
                itemDetailPopup?.UpdateItemState(response.Data?.Item);
                LoadInventory();
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
    private void HandleConsumeItem(InventoryItemResponse item)
    {
        Debug.Log($"[InventoryManager] ConsumeItem inventoryItemId={item.InventoryItemId} qty=1");

        InventoryApi.Instance.ConsumeItem(
            inventoryItemId: item.InventoryItemId,
            quantity: 1,
            onSuccess: _ =>
            {
                Debug.Log($"[InventoryManager] ✅ ConsumeItem OK");
                itemDetailPopup?.Hide();
                LoadInventory();
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

        InventoryApi.Instance.EquipSkin(
            playerSkinId: skin.PlayerSkinId,
            onSuccess: response =>
            {
                Debug.Log($"[InventoryManager] ✅ EquipSkin OK | SkinName={response.Data?.SkinName}");
                LoadInventory();
                itemDetailPopup?.Hide();
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

        InventoryApi.Instance.UnequipSkin(
            playerSkinId: skin.PlayerSkinId,
            onSuccess: _ =>
            {
                Debug.Log($"[InventoryManager] ✅ UnequipSkin OK");
                LoadInventory();
                itemDetailPopup?.Hide();
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

        RefreshCurrentTab();
    }

    private void RefreshCurrentTab()
    {
        if (_summary == null || uiInventory == null) return;

        var displayList = new List<UIItemDisplayData>();

        if (_showingSkins)
        {
            // ── Tab Skins: lấy từ _summary.PlayerSkins ─────────────────────
            // (có PlayerSkinId đúng để dùng khi equip/unequip)
            var skins = _summary.PlayerSkins;
            if (skins == null) { uiInventory.Refresh(displayList); return; }

            foreach (var skin in skins)
            {
                Sprite icon = ItemIconDatabase.Instance?.GetIcon(skin.SkinId);
                displayList.Add(new UIItemDisplayData
                {
                    itemId     = skin.PlayerSkinId,  // dùng PlayerSkinId làm id hiển thị
                    itemName   = skin.SkinName,
                    icon       = icon,
                    quantity   = 1,
                    rarity     = skin.SkinRarity,
                    isEquipped = skin.IsEquipped,
                    rawData    = skin                 // PlayerSkinSummaryResponse để popup dùng
                });
            }
        }
        else
        {
            // ── Tab Items: lấy từ EquippedItems + BagItems ─────────────────
            // Lọc ra item thông thường (không phải skin)
            var allItems = new List<InventoryItemResponse>();

            if (_summary.EquippedItems != null)
                foreach (var it in _summary.EquippedItems)
                    if (!it.IsSkin) allItems.Add(it);

            if (_summary.BagItems != null)
                foreach (var it in _summary.BagItems)
                    if (!it.IsSkin) allItems.Add(it);

            foreach (var item in allItems)
            {
                Sprite icon = ItemIconDatabase.Instance?.GetIcon(item.ItemId);
                displayList.Add(new UIItemDisplayData
                {
                    itemId     = item.InventoryItemId,
                    itemName   = item.ItemName,
                    icon       = icon,
                    quantity   = item.Quantity,
                    rarity     = item.ItemRarity,
                    isEquipped = item.IsEquipped,
                    rawData    = item  // InventoryItemResponse để popup dùng
                });
            }
        }

        uiInventory.Refresh(displayList);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private void UpdateStatsDisplay()
    {
        if (_summary == null) return;
        if (totalItemsText)  totalItemsText.text  = $"Items: {_summary.TotalItems}";
        if (totalSkinsText)  totalSkinsText.text  = $"Skins: {_summary.TotalSkins}";
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
}
