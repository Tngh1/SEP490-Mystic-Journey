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
    [SerializeField] private Image iconRarity;

    [Header("Rarity Icons")]
    [SerializeField] private Sprite iconCommon;
    [SerializeField] private Sprite iconUncommon;
    [SerializeField] private Sprite iconRare;
    [SerializeField] private Sprite iconEpic;
    [SerializeField] private Sprite iconLegendary;
    [SerializeField] private Sprite iconMythic;

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
        BindConsumeQuantityText();

        if (iconRarity == null && detailPanel != null)
        {
            Transform t = detailPanel.transform.Find("IconRarity");
            if (t != null) iconRarity = t.GetComponent<Image>();
        }

        ConfigureNameText(itemNameText);
        ConfigureNameText(oldItemName);
        ConfigureNameText(newItemName);
        ConfigureNameText(consumeName);
        ConfigureNameText(skinNameText);

        SetupHoverEffects();

        // Detail Buttons
        if (equipButton)      equipButton.onClick.AddListener(HandleEquipInitiated);
        if (unequipButton)    unequipButton.onClick.AddListener(HandleUnequip);
        if (consumeButton)    consumeButton.onClick.AddListener(HandleConsumeButtonClick);
        if (closeDetailButton) closeDetailButton.onClick.AddListener(Hide);

        // Equip Comparison Buttons
        if (confirmEquipButton) confirmEquipButton.onClick.AddListener(HandleEquipConfirmed);
        if (cancelEquipButton)  cancelEquipButton.onClick.AddListener(Hide);

        // Consume Buttons
        if (btnMinus) btnMinus.onClick.AddListener(() => ChangeConsumeQty(-1));
        if (btnPlus)  btnPlus.onClick.AddListener(() => ChangeConsumeQty(1));
        if (btnMax)   btnMax.onClick.AddListener(SetMaxSmartQuantity);
        if (confirmConsumeButton) confirmConsumeButton.onClick.AddListener(HandleConsumeConfirmed);
        if (cancelConsumeButton)  cancelConsumeButton.onClick.AddListener(Hide);
    }

    private void BindConsumeQuantityText()
    {
        if (consumeQuantityText != null || consumePanel == null)
            return;

        Transform quantityTransform = consumePanel.transform.Find("QuantityText");
        if (quantityTransform != null)
            consumeQuantityText = quantityTransform.GetComponent<TMP_Text>();

        if (consumeQuantityText == null)
            Debug.LogWarning("[UIItemDetailPopup] Consume quantity text is not assigned.", this);
    }

    private void SetupHoverEffects()
    {
        foreach (var btn in GetComponentsInChildren<Button>(true))
        {
            if (btn == null || btn.name == "DimBackground") continue;
            if (btn.GetComponent<UIHoverScaleEffect>() == null)
            {
                btn.gameObject.AddComponent<UIHoverScaleEffect>();
            }
        }
    }

    private void SwitchPanel(GameObject activePanel)
    {
        if (detailPanel) detailPanel.SetActive(false);
        if (equipComparisonPanel) equipComparisonPanel.SetActive(false);
        if (consumePanel) consumePanel.SetActive(false);
        if (skinPanel) skinPanel.SetActive(false);

        if (activePanel) activePanel.SetActive(true);
    }

    private int _currentSlotStackQuantity = 99;

    // =========================================================================
    // UC 20.2 - Show Detail
    // =========================================================================
    public void Show(InventoryItemResponse item, Sprite icon = null, int slotStackQuantity = 99)
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
        _currentSlotStackQuantity = slotStackQuantity > 0 ? slotStackQuantity : 99;

        bool isConsumable = IsConsumable(item);

        if (isConsumable)
        {
            ShowConsumePanel();
        }
        else
        {
            SwitchPanel(detailPanel);
        }

        gameObject.SetActive(true);

        Color rarityColor = GetRarityColor(item.ItemRarity);

        if (itemNameText)
        {
            ConfigureNameText(itemNameText);
            itemNameText.text = item.ItemName ?? "Unknown";
            itemNameText.color = rarityColor;
        }
        if (itemTypeText)        itemTypeText.text        = !string.IsNullOrEmpty(item.ItemSlot) ? item.ItemSlot : (item.ItemType ?? "");
        if (itemRarityText)
        {
            itemRarityText.text = item.ItemRarity ?? "";
            itemRarityText.color = rarityColor;
        }
        if (itemDescriptionText) 
        {
            itemDescriptionText.enableWordWrapping = true;
            itemDescriptionText.text = item.ItemDescription ?? "";
            if (item.CorruptionReduction > 0)
            {
                itemDescriptionText.text += $"\n<color=yellow>Giảm Hắc hóa: -{item.CorruptionReduction}</color>";
            }
        }
        if (quantityText)        quantityText.text        = $"x{_currentSlotStackQuantity}";

        if (iconRarity != null)
        {
            Sprite rSprite = GetRarityIcon(item.ItemRarity);
            iconRarity.sprite = rSprite;
            iconRarity.enabled = rSprite != null;
        }

        if (itemIcon != null)
        {
            if (icon != null) { itemIcon.sprite = icon; itemIcon.enabled = true; }
            else itemIcon.enabled = false;
            ApplyRarityGlowToImage(itemIcon, item.ItemRarity);
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
        SwitchPanel(null);
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
        PopulateEquipComparison(equippedItem, oldIcon);
    }

    public void ShowEquipComparison(
        InventoryItemResponse newItem,
        Sprite newIcon,
        int slotStackQuantity,
        InventoryItemResponse equippedItem,
        Sprite oldIcon = null)
    {
        if (newItem == null)
        {
            Hide();
            return;
        }

        _isSkinMode = false;
        _currentSkin = null;
        _currentItem = newItem;
        _currentIcon = newIcon;
        _currentSlotStackQuantity = slotStackQuantity > 0 ? slotStackQuantity : 99;

        // Chọn panel trước khi bật root để panel của item trước không xuất hiện lại.
        SwitchPanel(equipComparisonPanel);
        gameObject.SetActive(true);
        PopulateEquipComparison(equippedItem, oldIcon);
    }

    private void PopulateEquipComparison(InventoryItemResponse equippedItem, Sprite oldIcon)
    {
        EnsureStatIconsAsset();

        // Fill old item (Equipped)
        if (equippedItem != null)
        {
            if (oldItemName)
            {
                ConfigureNameText(oldItemName);
                oldItemName.text = equippedItem.ItemName;
                oldItemName.color = GetRarityColor(equippedItem.ItemRarity);
            }
            if (oldItemType) oldItemType.text = !string.IsNullOrEmpty(equippedItem.ItemSlot) ? equippedItem.ItemSlot : (equippedItem.ItemType ?? "");
            if (oldItemStats) oldItemStats.text = BuildStatsString(equippedItem);
            if (oldItemEffect) oldItemEffect.text = "None";
            if (oldItemIcon) {
                if (oldIcon != null) { oldItemIcon.sprite = oldIcon; oldItemIcon.enabled = true; }
                else oldItemIcon.enabled = false;
                ApplyRarityGlowToImage(oldItemIcon, equippedItem?.ItemRarity);
            }
        }
        else
        {
            if (oldItemName)
            {
                ConfigureNameText(oldItemName);
                oldItemName.text = "None";
                oldItemName.color = Color.white;
            }
            if (oldItemType) oldItemType.text = "";
            if (oldItemStats) oldItemStats.text = "";
            if (oldItemEffect) oldItemEffect.text = "";
            if (oldItemIcon) { oldItemIcon.enabled = false; ApplyRarityGlowToImage(oldItemIcon, null); }
        }

        // Fill new item (New)
        if (_currentItem != null)
        {
            if (newItemName)
            {
                ConfigureNameText(newItemName);
                newItemName.text = _currentItem.ItemName;
                newItemName.color = GetRarityColor(_currentItem.ItemRarity);
            }
            if (newItemType) newItemType.text = !string.IsNullOrEmpty(_currentItem.ItemSlot) ? _currentItem.ItemSlot : (_currentItem.ItemType ?? "");
            if (newItemStats) newItemStats.text = BuildStatsString(_currentItem, equippedItem);
            if (newItemEffect) newItemEffect.text = "None";
            if (newItemIcon) {
                if (_currentIcon != null) { newItemIcon.sprite = _currentIcon; newItemIcon.enabled = true; }
                else newItemIcon.enabled = false;
                ApplyRarityGlowToImage(newItemIcon, _currentItem?.ItemRarity);
            }
        }
    }

    private void EnsureStatIconsAsset()
    {
        var statIconsAsset = Resources.Load<TMP_SpriteAsset>("Sprite Assets/StatIcons");
        if (statIconsAsset != null)
        {
            if (oldItemStats != null) oldItemStats.spriteAsset = statIconsAsset;
            if (newItemStats != null) newItemStats.spriteAsset = statIconsAsset;
        }
    }

    private string BuildStatsString(InventoryItemResponse item, InventoryItemResponse oldItem = null)
    {
        string s = "";
        void AddStat(string iconName, string name, float val, float? oldVal = null, bool isPct = false) {
            if (val > 0) {
                string vStr = isPct ? $"{val:F1}%" : $"{(int)val}";
                string diffStr = "";
                if (oldVal.HasValue) {
                    if (val > oldVal.Value) diffStr = " <color=green>↑</color>";
                    else if (val < oldVal.Value) diffStr = " <color=red>↓</color>";
                }
                s += $"<sprite name=\"{iconName}\"> {name}: +{vStr}{diffStr}\n";
            }
        }
        AddStat("HPStats", "HP", item.BaseHp + item.BonusHp, oldItem != null ? oldItem.BaseHp + oldItem.BonusHp : (float?)null);
        AddStat("DMGStats", "ATK", item.BaseAtk + item.BonusAtk, oldItem != null ? oldItem.BaseAtk + oldItem.BonusAtk : (float?)null);
        AddStat("DEFStats", "DEF", item.BaseDef + item.BonusDef, oldItem != null ? oldItem.BaseDef + oldItem.BonusDef : (float?)null);
        AddStat("CritStats", "Crit Rate", item.BonusCritRate, oldItem?.BonusCritRate, true);
        AddStat("CritDMGStats", "Crit Dmg", item.BonusCritDamage, oldItem?.BonusCritDamage, true);
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
        
        Color rarityColor = GetRarityColor(_currentItem.ItemRarity);
        if (consumeName)
        {
            ConfigureNameText(consumeName);
            consumeName.text = _currentItem.ItemName;
            consumeName.color = rarityColor;
        }
        if (consumeDesc)
        {
            consumeDesc.enableWordWrapping = true;
            consumeDesc.overflowMode = TextOverflowModes.Overflow;
            consumeDesc.text = _currentItem.ItemDescription;
        }
        if (consumeOwnedText) consumeOwnedText.text = $"Quantity owned: {_currentSlotStackQuantity}";
        if (consumeIcon) {
            if (_currentIcon != null) { consumeIcon.sprite = _currentIcon; consumeIcon.enabled = true; }
            else consumeIcon.enabled = false;
            ApplyRarityGlowToImage(consumeIcon, null);
        }

        UpdateConsumeQuantityText();
        AdjustConsumePanelLayout();
    }

    private int GetMaxUsableInCurrentSlot()
    {
        int maxSlotCap = _currentSlotStackQuantity > 0 ? _currentSlotStackQuantity : 99;
        return Mathf.Clamp(maxSlotCap, 1, 99);
    }

    private void ChangeConsumeQty(int delta)
    {
        if (_currentItem == null) return;
        int maxUsable = GetMaxUsableInCurrentSlot();
        _consumeQuantity = Mathf.Clamp(_consumeQuantity + delta, 1, maxUsable);
        UpdateConsumeQuantityText();
    }

    /// <summary>
    /// Nút "Max" thông minh cho consumable hồi máu:
    /// - Nếu item là heal potion (BaseHp/BonusHp > 0, hoặc tên chứa Potion/Health/Heal/HP):
    ///     • HP đã đầy        → 1 (không lãng phí)
    ///     • HP chưa đầy + biết heal amount → tính số bình vừa đủ fill HP nhưng KHÔNG vượt quá Max Stack 99 của ô này
    ///     • HP chưa đầy + không biết heal amount → max hết stack ô này (tối đa 99)
    /// - Nếu không phải heal potion (EXP book, material...) → max hết stack ô này (tối đa 99).
    /// </summary>
    private void SetMaxSmartQuantity()
    {
        if (_currentItem == null) return;

        int maxUsable = GetMaxUsableInCurrentSlot();

        // Lấy lượng hồi máu: từ BaseHp, BonusHp hoặc tự đọc số từ ItemDescription
        int healPerItem = ParseHealAmount(_currentItem);

        // Nhận diện heal potion: qua stat, qua số heal đọc được, hoặc qua tên item
        bool isHealItem = healPerItem > 0 || IsHealConsumableByName(_currentItem.ItemName);

        // Lấy HP hiện tại và HP tối đa của người chơi từ mọi nguồn
        int currentHp = 0;
        int maxHp = 0;

        if (PlayerHUDController.Instance != null && PlayerHUDController.Instance.MaxHp > 0)
        {
            currentHp = PlayerHUDController.Instance.CurrentHp;
            maxHp = PlayerHUDController.Instance.MaxHp;
        }
        else if (PlayerEntity.Instance != null && PlayerEntity.Instance.MaxHealth > 0)
        {
            currentHp = PlayerEntity.Instance.CurrentHealth;
            maxHp = PlayerEntity.Instance.MaxHealth;
        }
        else if (NetworkPlayer.Local != null && NetworkPlayer.Local.MaxHp > 0)
        {
            currentHp = NetworkPlayer.Local.CurrentHp;
            maxHp = NetworkPlayer.Local.MaxHp;
        }

        if (isHealItem && maxHp > 0)
        {
            int hpDeficit = maxHp - currentHp;

            if (hpDeficit <= 0)
            {
                // HP đã đầy → đặt 1 và thông báo cho người chơi
                _consumeQuantity = 1;
                UIPopupBox.Notify(transform, "Notice", "Your HP is already full!\nNo need to use more health potions.");
            }
            else if (healPerItem > 0)
            {
                // Tính chính xác số bình vừa đủ để lấp đầy HP thiếu (không lãng phí và không vượt quá ô 99 này)
                int needed = Mathf.CeilToInt((float)hpDeficit / (float)healPerItem);
                _consumeQuantity = Mathf.Clamp(needed, 1, maxUsable);
            }
            else
            {
                // Không đọc được số heal → dùng tối đa ô này (tối đa 99)
                _consumeQuantity = maxUsable;
            }
        }
        else
        {
            // Vật phẩm khác (sách EXP, nguyên liệu...) → dùng tối đa ô này (tối đa 99)
            _consumeQuantity = maxUsable;
        }

        UpdateConsumeQuantityText();
    }

    /// <summary>
    /// Đọc lượng HP hồi từ BaseHp, BonusHp hoặc trích xuất số trong ItemDescription (ví dụ "Hồi 50 HP" -> 50).
    /// </summary>
    private static int ParseHealAmount(InventoryItemResponse item)
    {
        if (item == null) return 0;
        if (item.BaseHp > 0) return item.BaseHp;
        if (item.BonusHp > 0) return item.BonusHp;

        if (string.IsNullOrEmpty(item.ItemDescription)) return 0;

        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                item.ItemDescription,
                @"(?:heal|heals|hồi|restore|restores|\+)?\s*(\d+)\s*(?:hp|health|máu)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success && match.Groups.Count > 1 && int.TryParse(match.Groups[1].Value, out int amount) && amount > 0)
            {
                return amount;
            }
        }
        catch { }

        return 0;
    }

    /// <summary>Nhận diện heal potion qua tên item khi BaseHp và BonusHp đều = 0.</summary>
    private static bool IsHealConsumableByName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return false;
        return itemName.IndexOf("Potion",  StringComparison.OrdinalIgnoreCase) >= 0 ||
               itemName.IndexOf("Health",  StringComparison.OrdinalIgnoreCase) >= 0 ||
               itemName.IndexOf("Heal",    StringComparison.OrdinalIgnoreCase) >= 0 ||
               itemName.IndexOf("HP",      StringComparison.OrdinalIgnoreCase) >= 0 ||
               itemName.IndexOf("Bình",    StringComparison.OrdinalIgnoreCase) >= 0 ||
               itemName.IndexOf("Máu",     StringComparison.OrdinalIgnoreCase) >= 0;
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

    public static Color GetRarityColor(string rarity)
    {
        if (string.IsNullOrWhiteSpace(rarity)) return Color.white;

        switch (rarity.Trim().ToLowerInvariant())
        {
            case "common": return Color.white;
            case "uncommon": return new Color(0.35f, 0.9f, 0.45f); // Bright Green
            case "rare": return new Color(0.35f, 0.62f, 1f);     // Bright Blue
            case "epic": return new Color(0.75f, 0.45f, 1f);     // Bright Purple
            case "legendary": return new Color(1f, 0.72f, 0.2f); // Gold / Yellow
            case "mythic": return new Color(1f, 0.3f, 0.3f);     // Red
            default: return Color.white;
        }
    }

    private static void ApplyRarityGlowToImage(Image targetImage, string rarity)
    {
        if (targetImage == null) return;
        GameObject targetObj = targetImage.transform.parent != null &&
                               (targetImage.transform.parent.name.Contains("Icon", StringComparison.OrdinalIgnoreCase) ||
                                targetImage.transform.parent.name.Contains("Frame", StringComparison.OrdinalIgnoreCase))
            ? targetImage.transform.parent.gameObject
            : targetImage.gameObject;

        UIRarityFrameEffect effect = targetObj.GetComponent<UIRarityFrameEffect>()
                                     ?? targetObj.AddComponent<UIRarityFrameEffect>();

        if (!string.IsNullOrEmpty(rarity))
        {
            effect.Configure(rarity);
        }
        else
        {
            effect.SetVisible(false);
        }
    }

    private static void ConfigureNameText(TMP_Text text)
    {
        if (text == null) return;
        text.enableWordWrapping = true;
        text.enableAutoSizing = false;
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Bold;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = Vector4.zero;
        if (text.rectTransform != null)
        {
            if (text.rectTransform.rect.height < 32f)
                text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 36f);
            if (text.rectTransform.rect.width < 150f)
                text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 220f);
        }
    }

    private void AdjustConsumePanelLayout()
    {
        if (consumePanel == null) return;

        Canvas.ForceUpdateCanvases();
        if (consumeName != null) consumeName.ForceMeshUpdate();
        if (consumeDesc != null) consumeDesc.ForceMeshUpdate();
        if (consumeOwnedText != null) consumeOwnedText.ForceMeshUpdate();

        Transform descParent = consumeDesc != null ? consumeDesc.transform.parent : null;
        Transform ownedParent = consumeOwnedText != null ? consumeOwnedText.transform.parent : null;

        if (descParent != null && descParent == ownedParent)
        {
            VerticalLayoutGroup vlg = descParent.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(descParent as RectTransform);
                return;
            }
        }

        if (consumeDesc != null && consumeOwnedText != null)
        {
            RectTransform nameRect = consumeName != null ? consumeName.rectTransform : null;
            RectTransform descRect = consumeDesc.rectTransform;
            RectTransform ownedRect = consumeOwnedText.rectTransform;

            float descStartY = descRect.anchoredPosition.y;
            if (nameRect != null && consumeName != null)
            {
                float nameHeight = Mathf.Max(consumeName.preferredHeight, nameRect.rect.height);
                nameRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nameHeight);
                float nameTopY = nameRect.anchoredPosition.y;
                descStartY = nameTopY - nameHeight - 4f;
                descRect.anchoredPosition = new Vector2(descRect.anchoredPosition.x, descStartY);
            }

            float descHeight = Mathf.Max(consumeDesc.preferredHeight, descRect.rect.height);
            descRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, descHeight);

            float ownedStartY = descStartY - descHeight - 6f;
            ownedRect.anchoredPosition = new Vector2(ownedRect.anchoredPosition.x, ownedStartY);

            Canvas.ForceUpdateCanvases();
        }
    }

    private Sprite GetRarityIcon(string rarity)
    {
        if (string.IsNullOrWhiteSpace(rarity)) return iconCommon;

        switch (rarity.Trim().ToLowerInvariant())
        {
            case "common": return iconCommon;
            case "uncommon": return iconUncommon;
            case "rare": return iconRare;
            case "epic": return iconEpic;
            case "legendary": return iconLegendary;
            case "mythic": return iconMythic;
            default: return iconCommon;
        }
    }
}
