using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models.Response;

public class UIItemDetailPopup : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private GameObject equipComparisonPanel;
    [SerializeField] private GameObject consumePanel;
    [SerializeField] private GameObject skinPanel;

    // --- DETAIL PANEL ---
    [Header("Detail Panel - Text")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemTypeText;
    [SerializeField] private TMP_Text itemRarityText;
    [SerializeField] private TMP_Text itemDescriptionText;
    [SerializeField] private TMP_Text quantityText;

    [Header("Detail Panel - Stats")]
    [SerializeField] private GameObject statPanel;
    [SerializeField] private TMP_Text baseHpText;
    [SerializeField] private TMP_Text baseAtkText;
    [SerializeField] private TMP_Text baseDefText;
    [SerializeField] private TMP_Text bonusHpText;
    [SerializeField] private TMP_Text bonusAtkText;
    [SerializeField] private TMP_Text bonusDefText;
    [SerializeField] private TMP_Text bonusCritRateText;
    [SerializeField] private TMP_Text bonusCritDamageText;

    [Header("Detail Panel - Icon")]
    [SerializeField] private Image itemIcon;

    [Header("Detail Panel - Buttons")]
    [SerializeField] private Button equipButton;
    [SerializeField] private Button unequipButton;
    [SerializeField] private Button consumeButton;
    [SerializeField] private Button equipSkinButton;
    [SerializeField] private Button unequipSkinButton;
    [SerializeField] private Button closeDetailButton;

    // --- EQUIP COMPARISON PANEL ---
    [Header("Equip Comparison - Old Item")]
    [SerializeField] private TMP_Text oldItemName;
    [SerializeField] private TMP_Text oldItemType;
    [SerializeField] private TMP_Text oldItemStats;
    [SerializeField] private TMP_Text oldItemEffect;
    [SerializeField] private Image oldItemIcon;

    [Header("Equip Comparison - New Item")]
    [SerializeField] private TMP_Text newItemName;
    [SerializeField] private TMP_Text newItemType;
    [SerializeField] private TMP_Text newItemStats;
    [SerializeField] private TMP_Text newItemEffect;
    [SerializeField] private Image newItemIcon;

    [Header("Equip Comparison - Buttons")]
    [SerializeField] private Button confirmEquipButton;
    [SerializeField] private Button cancelEquipButton;

    // --- CONSUME PANEL ---
    [Header("Consume Panel - Fields")]
    [SerializeField] private TMP_Text consumeName;
    [SerializeField] private TMP_Text consumeDesc;
    [SerializeField] private TMP_Text consumeOwnedText;
    [SerializeField] private TMP_Text consumeQuantityText;
    [SerializeField] private Image consumeIcon;

    [Header("Consume Panel - Buttons")]
    [SerializeField] private Button btnMinus;
    [SerializeField] private Button btnPlus;
    [SerializeField] private Button btnMax;
    [SerializeField] private Button confirmConsumeButton;
    [SerializeField] private Button cancelConsumeButton;

    // --- SKIN PANEL ---
    [Header("Skin Panel - Fields")]
    [SerializeField] private TMP_Text skinTitleText;
    [SerializeField] private TMP_Text skinNameText;
    [SerializeField] private Image skinIcon;
    
    [Header("Skin Panel - Buttons")]
    [SerializeField] private Button confirmSkinButton;
    [SerializeField] private Button cancelSkinButton;

    // Dữ liệu item đang hiển thị
    private InventoryItemResponse _currentItem;
#pragma warning disable CS0414
    private PlayerSkinSummaryResponse _currentSkin;
    private bool _isSkinMode = false;
#pragma warning restore CS0414
    private int _consumeQuantity = 1;
    private Sprite _currentIcon;

    // Callbacks
    public Action<InventoryItemResponse> OnEquipInitiated; // Khi click Equip ở Detail -> InventoryManager tìm đồ đang mặc để so sánh
    public Action<InventoryItemResponse> OnEquipConfirmed; // Khi click Equip ở Comparison -> Thực sự mặc
    public Action<InventoryItemResponse> OnUnequipClicked; // Bấm gỡ
    public Action<InventoryItemResponse, int> OnConsumeConfirmed; // Thực sự dùng item với số lượng

    private void Awake()
    {
        // Detail Buttons
        if (equipButton)      equipButton.onClick.AddListener(HandleEquipInitiated);
        if (unequipButton)    unequipButton.onClick.AddListener(HandleUnequip);
        if (consumeButton)    consumeButton.onClick.AddListener(HandleConsumeButtonClick);
        if (closeDetailButton) closeDetailButton.onClick.AddListener(Hide);

        // Equip Comparison Buttons
        if (confirmEquipButton) confirmEquipButton.onClick.AddListener(HandleEquipConfirmed);
        if (cancelEquipButton)  cancelEquipButton.onClick.AddListener(() => SwitchPanel(detailPanel));

        // Consume Buttons
        if (btnMinus) btnMinus.onClick.AddListener(() => ChangeConsumeQty(-1));
        if (btnPlus)  btnPlus.onClick.AddListener(() => ChangeConsumeQty(1));
        if (btnMax)   btnMax.onClick.AddListener(() => ChangeConsumeQty(9999));
        if (confirmConsumeButton) confirmConsumeButton.onClick.AddListener(HandleConsumeConfirmed);
        if (cancelConsumeButton)  cancelConsumeButton.onClick.AddListener(Hide);
    }

    private void SwitchPanel(GameObject activePanel)
    {
        if (detailPanel) detailPanel.SetActive(activePanel == detailPanel);
        if (equipComparisonPanel) equipComparisonPanel.SetActive(activePanel == equipComparisonPanel);
        if (consumePanel) consumePanel.SetActive(activePanel == consumePanel);
        if (skinPanel) skinPanel.SetActive(activePanel == skinPanel);
    }

    // =========================================================================
    // UC 20.2 - Show Detail
    // =========================================================================
    public void Show(InventoryItemResponse item, Sprite icon = null)
    {
        if (item == null)
        {
            Hide();
            return;
        }

        _isSkinMode = false;
        _currentSkin = null;
        _currentItem = item;
        _currentIcon = icon;
        gameObject.SetActive(true);

        bool isConsumable = IsConsumable(item);

        if (isConsumable)
        {
            ShowConsumePanel();
        }
        else
        {
            SwitchPanel(detailPanel);
        }

        if (itemNameText)        itemNameText.text        = item.ItemName ?? "Unknown";
        if (itemTypeText)        itemTypeText.text        = item.ItemType ?? "";
        if (itemRarityText)      itemRarityText.text      = item.ItemRarity ?? "";
        if (itemDescriptionText) 
        {
            itemDescriptionText.text = item.ItemDescription ?? "";
            if (item.CorruptionReduction > 0)
            {
                itemDescriptionText.text += $"\n<color=yellow>Giảm Hắc hóa: -{item.CorruptionReduction}</color>";
            }
        }
        if (quantityText)        quantityText.text        = $"x{item.Quantity}";

        if (itemIcon != null)
        {
            if (icon != null) { itemIcon.sprite = icon; itemIcon.enabled = true; }
            else itemIcon.enabled = false;
        }

        bool isEquipment = IsEquipment(item);

        if (statPanel) statPanel.SetActive(isEquipment);

        if (isEquipment)
        {
            SetStatText(baseHpText, "Base HP", item.BaseHp);
            SetStatText(baseAtkText, "Base ATK", item.BaseAtk);
            SetStatText(baseDefText, "Base DEF", item.BaseDef);
            SetStatText(bonusHpText, "Bonus HP", item.BonusHp);
            SetStatText(bonusAtkText, "Bonus ATK", item.BonusAtk);
            SetStatText(bonusDefText, "Bonus DEF", item.BonusDef);
            SetStatText(bonusCritRateText, "Crit Rate", item.BonusCritRate, true);
            SetStatText(bonusCritDamageText, "Crit Dmg", item.BonusCritDamage, true);
        }

        RefreshButtons(item);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        _currentItem = null;
    }

    public void UpdateItemState(InventoryItemResponse updatedItem)
    {
        if (_currentItem == null || updatedItem == null) return;
        if (_currentItem.InventoryItemId != updatedItem.InventoryItemId) return;
        _currentItem = updatedItem;
        RefreshButtons(updatedItem);
    }

    private void RefreshButtons(InventoryItemResponse item)
    {
        if (item == null)
            return;

        bool isEquipment = IsEquipment(item);
        bool isEquipped = item.IsEquipped && isEquipment;
        bool isConsumable = IsConsumable(item);

        if (equipButton)   equipButton.gameObject.SetActive(!isEquipped && isEquipment);
        if (unequipButton) unequipButton.gameObject.SetActive(isEquipped);

        if (consumeButton) consumeButton.gameObject.SetActive(isConsumable && item.Quantity > 0);
        if (equipSkinButton) equipSkinButton.gameObject.SetActive(false);
        if (unequipSkinButton) unequipSkinButton.gameObject.SetActive(false);
    }

    private void SetStatText(TMP_Text label, string name, float value, bool isFloat = false)
    {
        if (label == null) return;
        label.text = isFloat ? $"{name}: {value:F1}%" : $"{name}: {(int)value}";
    }

    // =========================================================================
    // ACTIONS
    // =========================================================================
    private void HandleEquipInitiated()
    {
        if (_currentItem != null && IsEquipment(_currentItem)) OnEquipInitiated?.Invoke(_currentItem);
    }

    private void HandleEquipConfirmed()
    {
        if (_currentItem != null && IsEquipment(_currentItem)) OnEquipConfirmed?.Invoke(_currentItem);
    }

    private void HandleUnequip()
    {
        if (_currentItem != null && IsEquipment(_currentItem)) OnUnequipClicked?.Invoke(_currentItem);
    }

    // =========================================================================
    // EQUIP COMPARISON PANEL
    // =========================================================================
    public void ShowEquipComparison(InventoryItemResponse equippedItem, Sprite oldIcon = null)
    {
        SwitchPanel(equipComparisonPanel);

        // Fill old item (Equipped)
        if (equippedItem != null)
        {
            if (oldItemName) oldItemName.text = equippedItem.ItemName;
            if (oldItemType) oldItemType.text = equippedItem.ItemType;
            if (oldItemStats) oldItemStats.text = BuildStatsString(equippedItem);
            if (oldItemEffect) oldItemEffect.text = "None"; // Or fetch from item if applicable
            if (oldItemIcon) {
                if (oldIcon != null) { oldItemIcon.sprite = oldIcon; oldItemIcon.enabled = true; }
                else oldItemIcon.enabled = false;
            }
        }
        else
        {
            if (oldItemName) oldItemName.text = "None";
            if (oldItemType) oldItemType.text = "";
            if (oldItemStats) oldItemStats.text = "";
            if (oldItemEffect) oldItemEffect.text = "";
            if (oldItemIcon) oldItemIcon.enabled = false;
        }

        // Fill new item (New)
        if (_currentItem != null)
        {
            if (newItemName) newItemName.text = _currentItem.ItemName;
            if (newItemType) newItemType.text = _currentItem.ItemType;
            if (newItemStats) newItemStats.text = BuildStatsString(_currentItem, equippedItem);
            if (newItemEffect) newItemEffect.text = "None";
            if (newItemIcon) {
                if (_currentIcon != null) { newItemIcon.sprite = _currentIcon; newItemIcon.enabled = true; }
                else newItemIcon.enabled = false;
            }
        }
    }

    private string BuildStatsString(InventoryItemResponse item, InventoryItemResponse oldItem = null)
    {
        string s = "";
        void AddStat(string name, float val, float? oldVal = null, bool isPct = false) {
            if (val > 0) {
                string vStr = isPct ? $"{val:F1}%" : $"{(int)val}";
                string diffStr = "";
                if (oldVal.HasValue) {
                    if (val > oldVal.Value) diffStr = " <color=green>↑</color>";
                    else if (val < oldVal.Value) diffStr = " <color=red>↓</color>";
                }
                s += $"{name}: +{vStr}{diffStr}\n";
            }
        }
        AddStat("HP", item.BaseHp + item.BonusHp, oldItem != null ? oldItem.BaseHp + oldItem.BonusHp : (float?)null);
        AddStat("ATK", item.BaseAtk + item.BonusAtk, oldItem != null ? oldItem.BaseAtk + oldItem.BonusAtk : (float?)null);
        AddStat("DEF", item.BaseDef + item.BonusDef, oldItem != null ? oldItem.BaseDef + oldItem.BonusDef : (float?)null);
        AddStat("Crit Rate", item.BonusCritRate, oldItem?.BonusCritRate, true);
        AddStat("Crit Dmg", item.BonusCritDamage, oldItem?.BonusCritDamage, true);
        return s;
    }

    // =========================================================================
    // CONSUME PANEL
    // =========================================================================
    private void HandleConsumeButtonClick()
    {
        if (_currentItem != null && _currentItem.ItemName != null && _currentItem.ItemName.Contains("Lucky Ticket", StringComparison.OrdinalIgnoreCase))
        {
            OnConsumeConfirmed?.Invoke(_currentItem, 1);
        }
        else
        {
            ShowConsumePanel();
        }
    }

    private void ShowConsumePanel()
    {
        if (_currentItem == null || !IsConsumable(_currentItem) || _currentItem.Quantity <= 0) return;
        SwitchPanel(consumePanel);

        _consumeQuantity = 1;
        
        if (consumeName) consumeName.text = _currentItem.ItemName;
        if (consumeDesc) consumeDesc.text = _currentItem.ItemDescription;
        if (consumeOwnedText) consumeOwnedText.text = $"Quantity owned: {_currentItem.Quantity}";
        if (consumeIcon) {
            if (_currentIcon != null) { consumeIcon.sprite = _currentIcon; consumeIcon.enabled = true; }
            else consumeIcon.enabled = false;
        }

        UpdateConsumeQuantityText();
    }

    private void ChangeConsumeQty(int delta)
    {
        if (_currentItem == null) return;
        
        if (delta == 9999) {
            _consumeQuantity = _currentItem.Quantity;
        } else {
            _consumeQuantity += delta;
        }
        
        if (_consumeQuantity < 1) _consumeQuantity = 1;
        if (_consumeQuantity > _currentItem.Quantity) _consumeQuantity = _currentItem.Quantity;

        UpdateConsumeQuantityText();
    }

    private void UpdateConsumeQuantityText()
    {
        if (consumeQuantityText) consumeQuantityText.text = _consumeQuantity.ToString();
    }

    private void HandleConsumeConfirmed()
    {
        if (_currentItem != null && IsConsumable(_currentItem) && _currentItem.Quantity > 0)
            OnConsumeConfirmed?.Invoke(_currentItem, _consumeQuantity);
    }

    private static bool IsConsumable(InventoryItemResponse item)
    {
        return IsItemType(item, "Consumable") || (item != null && item.ItemName != null && item.ItemName.Contains("Lucky Ticket", StringComparison.OrdinalIgnoreCase));
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
               string.Equals(item.ItemType, itemType, StringComparison.OrdinalIgnoreCase);
    }
}
