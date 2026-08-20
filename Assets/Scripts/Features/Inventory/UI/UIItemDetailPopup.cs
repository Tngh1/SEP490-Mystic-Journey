using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models.Response;

// Executes mono behaviour operation.
public class UIItemDetailPopup : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private GameObject equipComparisonPanel;
    [SerializeField] private GameObject consumePanel;
    [SerializeField] private GameObject skinPanel;

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

    [Header("Skin Panel - Fields")]
    [SerializeField] private TMP_Text skinTitleText;
    [SerializeField] private TMP_Text skinNameText;
    [SerializeField] private Image skinIcon;

    [Header("Skin Panel - Buttons")]
    [SerializeField] private Button confirmSkinButton;
    [SerializeField] private Button cancelSkinButton;

    private InventoryItemResponse _currentItem;
#pragma warning disable CS0414
    private PlayerSkinSummaryResponse _currentSkin;
    private bool _isSkinMode = false;
#pragma warning restore CS0414
    private int _consumeQuantity = 1;
    private Sprite _currentIcon;

    public Action<InventoryItemResponse> OnEquipInitiated;
    public Action<InventoryItemResponse> OnEquipConfirmed;
    public Action<InventoryItemResponse> OnUnequipClicked;
    public Action<InventoryItemResponse, int> OnConsumeConfirmed;

    // Initializes internal component caches and dependencies for UIItemDetailPopup upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
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

        if (equipButton)      equipButton.onClick.AddListener(HandleEquipInitiated);
        if (unequipButton)    unequipButton.onClick.AddListener(HandleUnequip);
        if (consumeButton)    consumeButton.onClick.AddListener(HandleConsumeButtonClick);
        if (closeDetailButton) closeDetailButton.onClick.AddListener(Hide);

        if (confirmEquipButton) confirmEquipButton.onClick.AddListener(HandleEquipConfirmed);
        if (cancelEquipButton)  cancelEquipButton.onClick.AddListener(Hide);

        if (btnMinus) btnMinus.onClick.AddListener(() => ChangeConsumeQty(-1));
        if (btnPlus)  btnPlus.onClick.AddListener(() => ChangeConsumeQty(1));
        if (btnMax)   btnMax.onClick.AddListener(SetMaxSmartQuantity);
        if (confirmConsumeButton) confirmConsumeButton.onClick.AddListener(HandleConsumeConfirmed);
        if (cancelConsumeButton)  cancelConsumeButton.onClick.AddListener(Hide);
    }

    // Executes bind consume quantity text operation.
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

    // Executes setup hover effects operation.
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

    // Executes switch panel operation.
    // Validates input parameters against null or empty values.
    private void SwitchPanel(GameObject activePanel)
    {
        if (detailPanel) detailPanel.SetActive(false);
        if (equipComparisonPanel) equipComparisonPanel.SetActive(false);
        if (consumePanel) consumePanel.SetActive(false);
        if (skinPanel) skinPanel.SetActive(false);

        if (activePanel) activePanel.SetActive(true);
    }

    private int _currentSlotStackQuantity = 99;

    // Executes show operation.
    // Validates input parameters against null or empty values.
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

    // Update visibility for the current state; it updates active.
    public void Hide()
    {
        SwitchPanel(null);
        gameObject.SetActive(false);
        _currentItem = null;
    }

    // Executes update item state operation.
    public void UpdateItemState(InventoryItemResponse updatedItem)
    {
        if (_currentItem == null || updatedItem == null) return;
        if (_currentItem.InventoryItemId != updatedItem.InventoryItemId) return;
        _currentItem = updatedItem;
        RefreshButtons(updatedItem);
    }

    // Executes refresh buttons operation.
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

    // Executes set stat text operation.
    private void SetStatText(TMP_Text label, string name, float value, bool isFloat = false)
    {
        if (label == null) return;
        label.text = isFloat ? $"{name}: {value:F1}%" : $"{name}: {(int)value}";
    }

    // Executes handle equip initiated operation.
    private void HandleEquipInitiated()
    {
        if (_currentItem != null && IsEquipment(_currentItem)) OnEquipInitiated?.Invoke(_currentItem);
    }

    // Executes handle equip confirmed operation.
    private void HandleEquipConfirmed()
    {
        if (_currentItem != null && IsEquipment(_currentItem)) OnEquipConfirmed?.Invoke(_currentItem);
    }

    // Executes handle unequip operation.
    private void HandleUnequip()
    {
        if (_currentItem != null && IsEquipment(_currentItem)) OnUnequipClicked?.Invoke(_currentItem);
    }

    // Executes show equip comparison operation.
    public void ShowEquipComparison(InventoryItemResponse equippedItem, Sprite oldIcon = null)
    {
        SwitchPanel(equipComparisonPanel);
        PopulateEquipComparison(equippedItem, oldIcon);
    }

    // Update visibility for equip comparison using new item, new icon, slot stack quantity, and equipped item; it updates navigation or visibility through hide and updates active and guards invalid or unavailable states.
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

        SwitchPanel(equipComparisonPanel);
        gameObject.SetActive(true);
        PopulateEquipComparison(equippedItem, oldIcon);
    }

    // Executes populate equip comparison operation.
    // Validates input parameters against null or empty values.
    private void PopulateEquipComparison(InventoryItemResponse equippedItem, Sprite oldIcon)
    {
        EnsureStatIconsAsset();

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

    // Executes ensure stat icons asset operation.
    private void EnsureStatIconsAsset()
    {
        var statIconsAsset = Resources.Load<TMP_SpriteAsset>("Sprite Assets/StatIcons");
        if (statIconsAsset != null)
        {
            if (oldItemStats != null) oldItemStats.spriteAsset = statIconsAsset;
            if (newItemStats != null) newItemStats.spriteAsset = statIconsAsset;
        }
    }

    // Executes build stats string operation.
    private string BuildStatsString(InventoryItemResponse item, InventoryItemResponse oldItem = null)
    {
        if (item == null) return "";
        string s = "";
        void AddStat(string iconName, string name, float val, float? oldVal = null, bool isPct = false) {
            if (val > 0 || (oldVal.HasValue && oldVal.Value > 0)) {
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

    // Executes handle consume button click operation.
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

    // Executes show consume panel operation.
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

    // Executes get max usable in current slot operation.
    private int GetMaxUsableInCurrentSlot()
    {
        int maxSlotCap = _currentSlotStackQuantity > 0 ? _currentSlotStackQuantity : 99;
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        return Mathf.Clamp(maxSlotCap, 1, 99);
    }

    // Executes change consume qty operation.
    private void ChangeConsumeQty(int delta)
    {
        if (_currentItem == null) return;
        int maxUsable = GetMaxUsableInCurrentSlot();
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        _consumeQuantity = Mathf.Clamp(_consumeQuantity + delta, 1, maxUsable);
        UpdateConsumeQuantityText();
    }

    // Executes set max smart quantity operation.
    private void SetMaxSmartQuantity()
    {
        if (_currentItem == null) return;

        int maxUsable = GetMaxUsableInCurrentSlot();

        int healPerItem = ParseHealAmount(_currentItem);

        bool isHealItem = healPerItem > 0 || IsHealConsumableByName(_currentItem.ItemName);

        int currentHp = 0;
        int maxHp = 0;

        if (PlayerHUDUIManager.Instance != null && PlayerHUDUIManager.Instance.MaxHp > 0)
        {
            currentHp = PlayerHUDUIManager.Instance.CurrentHp;
            maxHp = PlayerHUDUIManager.Instance.MaxHp;
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
                _consumeQuantity = 1;
                UIPopupBox.Notify(transform, "Notice", "Your HP is already full!\nNo need to use more health potions.");
            }
            else if (healPerItem > 0)
            {
                int needed = Mathf.CeilToInt((float)hpDeficit / (float)healPerItem);
                // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
                _consumeQuantity = Mathf.Clamp(needed, 1, maxUsable);
            }
            else
            {
                _consumeQuantity = maxUsable;
            }
        }
        else
        {
            _consumeQuantity = maxUsable;
        }

        UpdateConsumeQuantityText();
    }

    // Executes parse heal amount operation.
    // Validates input parameters against null or empty values.
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

    // Executes is heal consumable by name operation.
    // Validates input parameters against null or empty values.
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


    // Executes update consume quantity text operation.
    private void UpdateConsumeQuantityText()
    {
        if (consumeQuantityText) consumeQuantityText.text = _consumeQuantity.ToString();
    }

    // Executes handle consume confirmed operation.
    private void HandleConsumeConfirmed()
    {
        if (_currentItem != null && IsConsumable(_currentItem) && _currentItem.Quantity > 0)
            OnConsumeConfirmed?.Invoke(_currentItem, _consumeQuantity);
    }

    // Executes is consumable operation.
    private static bool IsConsumable(InventoryItemResponse item)
    {
        return IsItemType(item, "Consumable") || (item != null && item.ItemName != null && item.ItemName.Contains("Lucky Ticket", StringComparison.OrdinalIgnoreCase));
    }

    // Executes is equipment operation.
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

    // Executes is item type operation.
    // Validates input parameters against null or empty values.
    private static bool IsItemType(InventoryItemResponse item, string itemType)
    {
        return item != null &&
               string.Equals(item.ItemType, itemType, StringComparison.OrdinalIgnoreCase);
    }

    // Executes get rarity color operation.
    // Validates input parameters against null or empty values.
    public static Color GetRarityColor(string rarity)
    {
        if (string.IsNullOrWhiteSpace(rarity)) return Color.white;

        switch (rarity.Trim().ToLowerInvariant())
        {
            case "common": return Color.white;
            case "uncommon": return new Color(0.35f, 0.9f, 0.45f);
            case "rare": return new Color(0.35f, 0.62f, 1f);
            case "epic": return new Color(0.75f, 0.45f, 1f);
            case "legendary": return new Color(1f, 0.72f, 0.2f);
            case "mythic": return new Color(1f, 0.3f, 0.3f);
            default: return Color.white;
        }
    }

    // Executes apply rarity glow to image operation.
    // Validates input parameters against null or empty values.
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

    // Executes configure name text operation.
    private static void ConfigureNameText(TMP_Text text)
    {
        if (text == null) return;
        text.enableWordWrapping = true;
        text.enableAutoSizing = false;
        text.fontSize = 30f;
        text.fontStyle = FontStyles.Bold;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = Vector4.zero;
        if (text.rectTransform != null)
        {
            if (text.rectTransform.rect.height < 40f)
                text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 42f);
            if (text.rectTransform.rect.width < 150f)
                text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 220f);
        }
    }

    // Executes adjust consume panel layout operation.
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
                float nameHeight = Mathf.Max(consumeName.preferredHeight, nameRect.rect.height, 42f);
                nameRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nameHeight);
                float nameBottomY = nameRect.anchoredPosition.y - (nameRect.pivot.y * nameHeight);
                descStartY = nameBottomY - ((1f - descRect.pivot.y) * descRect.rect.height) - 8f;
                descRect.anchoredPosition = new Vector2(descRect.anchoredPosition.x, descStartY);
            }

            float descHeight = Mathf.Max(consumeDesc.preferredHeight, descRect.rect.height);
            descRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, descHeight);

            float descBottomY = descStartY - (descRect.pivot.y * descHeight);
            float ownedHeight = Mathf.Max(consumeOwnedText.preferredHeight, ownedRect.rect.height);
            float ownedStartY = descBottomY - ((1f - ownedRect.pivot.y) * ownedHeight) - 8f;
            ownedRect.anchoredPosition = new Vector2(ownedRect.anchoredPosition.x, ownedStartY);

            Canvas.ForceUpdateCanvases();
        }
    }

    // Executes get rarity icon operation.
    // Validates input parameters against null or empty values.
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
