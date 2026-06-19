using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models.Response;

// =============================================================================
// UIItemDetailPopup – UC 20.2 View Item Detail
// Hiển thị chi tiết 1 item được chọn từ UIInventory.
// Hỗ trợ 2 loại:
//   • InventoryItemResponse  → Show()      (item thường)
//   • PlayerSkinSummaryResponse → ShowSkin() (skin)
// Cung cấp các nút hành động để gọi lên InventoryManager:
//   • Equip / Unequip  (UC 20.4 / 20.5)  – với item thường
//   • Equip Skin / Unequip Skin (UC 20.6 / 20.7) – với skin
//   • Consume          (UC 20.3)          – với consumable
// =============================================================================
public class UIItemDetailPopup : MonoBehaviour
{
    [Header("Text Fields")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemTypeText;
    [SerializeField] private TMP_Text itemRarityText;
    [SerializeField] private TMP_Text itemDescriptionText;
    [SerializeField] private TMP_Text quantityText;

    [Header("Stat Fields")]
    [SerializeField] private TMP_Text baseHpText;
    [SerializeField] private TMP_Text baseAtkText;
    [SerializeField] private TMP_Text baseDefText;
    [SerializeField] private TMP_Text bonusHpText;
    [SerializeField] private TMP_Text bonusAtkText;
    [SerializeField] private TMP_Text bonusDefText;
    [SerializeField] private TMP_Text bonusCritRateText;
    [SerializeField] private TMP_Text bonusCritDamageText;

    [Header("Icon")]
    [SerializeField] private Image itemIcon;

    [Header("Action Buttons")]
    [SerializeField] private Button equipButton;
    [SerializeField] private Button unequipButton;
    [SerializeField] private Button consumeButton;
    [SerializeField] private Button equipSkinButton;
    [SerializeField] private Button unequipSkinButton;
    [SerializeField] private Button closeButton;

    [Header("Stat Panel")]
    [SerializeField] private GameObject statPanel; // ẩn với Consumable/Skin

    // Dữ liệu item đang hiển thị (từ API response)
    private InventoryItemResponse _currentItem;
    private PlayerSkinSummaryResponse _currentSkin;
    private bool _isSkinMode = false;

    // Callback lên InventoryManager – Items
    public Action<InventoryItemResponse> OnEquipClicked;
    public Action<InventoryItemResponse> OnUnequipClicked;
    public Action<InventoryItemResponse> OnConsumeClicked;
    // Callback lên InventoryManager – Skins (PlayerSkinId đúng)
    public Action<PlayerSkinSummaryResponse> OnEquipSkinClicked;
    public Action<PlayerSkinSummaryResponse> OnUnequipSkinClicked;

    private void Awake()
    {
        // Gắn sự kiện cho các nút
        if (equipButton)      equipButton.onClick.AddListener(HandleEquip);
        if (unequipButton)    unequipButton.onClick.AddListener(HandleUnequip);
        if (consumeButton)    consumeButton.onClick.AddListener(HandleConsume);
        if (equipSkinButton)  equipSkinButton.onClick.AddListener(HandleEquipSkin);
        if (unequipSkinButton) unequipSkinButton.onClick.AddListener(HandleUnequipSkin);
        if (closeButton)      closeButton.onClick.AddListener(Hide);
    }

    // =========================================================================
    // UC 20.2 – Mở popup cho InventoryItem thông thường
    // =========================================================================

    // Mở popup cho Skin (PlayerSkinSummaryResponse)
    public void ShowSkin(PlayerSkinSummaryResponse skin, Sprite icon = null)
    {
        _isSkinMode  = true;
        _currentSkin = skin;
        _currentItem = null;
        gameObject.SetActive(true);

        if (itemNameText)        itemNameText.text        = skin.SkinName ?? "Unknown";
        if (itemTypeText)        itemTypeText.text        = $"Skin – {skin.SkinType}";
        if (itemRarityText)      itemRarityText.text      = skin.SkinRarity ?? "";
        if (itemDescriptionText) itemDescriptionText.text = skin.SkinDescription ?? "";
        if (quantityText)        quantityText.text        = "x1";

        if (itemIcon != null)
        {
            if (icon != null) { itemIcon.sprite = icon; itemIcon.enabled = true; }
            else itemIcon.enabled = false;
        }

        if (statPanel) statPanel.SetActive(false); // Skin không có stat

        // Nút skin
        if (equipButton)      equipButton.gameObject.SetActive(false);
        if (unequipButton)    unequipButton.gameObject.SetActive(false);
        if (consumeButton)    consumeButton.gameObject.SetActive(false);
        if (equipSkinButton)   equipSkinButton.gameObject.SetActive(!skin.IsEquipped);
        if (unequipSkinButton) unequipSkinButton.gameObject.SetActive(skin.IsEquipped);
    }

    // =========================================================================
    // UC 20.2 – Mở popup cho InventoryItem thông thường
    // =========================================================================
    public void Show(InventoryItemResponse item, Sprite icon = null)
    {
        _isSkinMode  = false;
        _currentSkin = null;
        _currentItem = item;
        gameObject.SetActive(true);

        // --- Thông tin cơ bản ---
        if (itemNameText)        itemNameText.text        = item.ItemName ?? "Unknown";
        if (itemTypeText)        itemTypeText.text        = item.ItemType ?? "";
        if (itemRarityText)      itemRarityText.text      = item.ItemRarity ?? "";
        if (itemDescriptionText) itemDescriptionText.text = item.ItemDescription ?? "";
        if (quantityText)        quantityText.text        = $"x{item.Quantity}";

        // --- Icon ---
        if (itemIcon != null)
        {
            if (icon != null)
            {
                itemIcon.sprite  = icon;
                itemIcon.enabled = true;
            }
            else
            {
                itemIcon.enabled = false;
            }
        }

        // --- Stats (chỉ hiện với Equipment) ---
        bool isEquipment = item.ItemType == "Weapon" || item.ItemType == "Armor" ||
                           item.ItemType == "Accessory" || item.ItemType == "Helmet" ||
                           item.ItemType == "Gloves"   || item.ItemType == "Boots"  ||
                           item.ItemType == "Ring"      || item.ItemType == "Necklace";

        if (statPanel) statPanel.SetActive(isEquipment);

        if (isEquipment)
        {
            SetStatText(baseHpText,          "Base HP",        item.BaseHp);
            SetStatText(baseAtkText,         "Base ATK",       item.BaseAtk);
            SetStatText(baseDefText,         "Base DEF",       item.BaseDef);
            SetStatText(bonusHpText,         "Bonus HP",       item.BonusHp);
            SetStatText(bonusAtkText,        "Bonus ATK",      item.BonusAtk);
            SetStatText(bonusDefText,        "Bonus DEF",      item.BonusDef);
            SetStatText(bonusCritRateText,   "Crit Rate",      item.BonusCritRate,   isFloat: true);
            SetStatText(bonusCritDamageText, "Crit Damage",    item.BonusCritDamage, isFloat: true);
        }

        // --- Hiển thị / ẩn nút theo loại item ---
        RefreshButtons(item);
    }

    // =========================================================================
    // Ẩn popup
    // =========================================================================
    public void Hide()
    {
        gameObject.SetActive(false);
        _currentItem = null;
        _currentSkin = null;
        _isSkinMode  = false;
    }

    // =========================================================================
    // Cập nhật trạng thái nút sau khi equip/unequip
    // =========================================================================
    public void UpdateItemState(InventoryItemResponse updatedItem)
    {
        if (_currentItem == null || updatedItem == null) return;
        if (_currentItem.InventoryItemId != updatedItem.InventoryItemId) return;

        _currentItem = updatedItem;
        RefreshButtons(updatedItem);
    }

    // =========================================================================
    // Internal helpers
    // =========================================================================
    private void RefreshButtons(InventoryItemResponse item)
    {
        bool isSkin       = item.IsSkin;
        bool isEquipped   = item.IsEquipped;
        bool isConsumable = item.ItemType == "Consumable";

        // Nút Item thường (Equip / Unequip)
        if (equipButton)   equipButton.gameObject.SetActive(!isSkin && !isEquipped && !isConsumable);
        if (unequipButton) unequipButton.gameObject.SetActive(!isSkin && isEquipped);

        // Nút Skin (Equip Skin / Unequip Skin)
        if (equipSkinButton)   equipSkinButton.gameObject.SetActive(isSkin && !isEquipped);
        if (unequipSkinButton) unequipSkinButton.gameObject.SetActive(isSkin && isEquipped);

        // Nút Consume
        if (consumeButton) consumeButton.gameObject.SetActive(isConsumable);
    }

    private void SetStatText(TMP_Text label, string name, float value, bool isFloat = false)
    {
        if (label == null) return;
        label.text = isFloat
            ? $"{name}: {value:F1}%"
            : $"{name}: {(int)value}";
    }

    private void HandleEquip()
    {
        if (_currentItem != null) OnEquipClicked?.Invoke(_currentItem);
    }

    private void HandleUnequip()
    {
        if (_currentItem != null) OnUnequipClicked?.Invoke(_currentItem);
    }

    private void HandleConsume()
    {
        if (_currentItem != null) OnConsumeClicked?.Invoke(_currentItem);
    }

    private void HandleEquipSkin()
    {
        if (_isSkinMode && _currentSkin != null) OnEquipSkinClicked?.Invoke(_currentSkin);
    }

    private void HandleUnequipSkin()
    {
        if (_isSkinMode && _currentSkin != null) OnUnequipSkinClicked?.Invoke(_currentSkin);
    }
}
