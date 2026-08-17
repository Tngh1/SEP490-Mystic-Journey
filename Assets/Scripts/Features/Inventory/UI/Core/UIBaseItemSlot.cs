using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Executes i pointer click handler operation.
public abstract class UIBaseItemSlot : MonoBehaviour, IPointerClickHandler
{
    [Header("Core UI Elements")]
    [SerializeField] protected Image iconImage;
    [SerializeField] protected TMP_Text itemNameText;
    [SerializeField] protected TMP_Text quantityText;
    [SerializeField] protected Image rarityBorder;
    private UIRarityFrameEffect rarityEffect;
    [SerializeField] protected GameObject selectHighlight;

    public Action<UIBaseItemSlot> OnSlotClicked;
    // Executes raw data operation.
    public object RawData { get; protected set; }
    // Executes display data operation.
    public UIItemDisplayData DisplayData { get; protected set; }

    // Executes setup core operation.
    public virtual void SetupCore(UIItemDisplayData data)
    {
        BindCore();
        DisplayData = data;

        if (data == null)
        {
            ClearSlot();
            return;
        }

        RawData = data.rawData;

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
            EnsureIconCentered();
        }

        if (itemNameText != null)
            itemNameText.text = data.itemName ?? string.Empty;

        if (quantityText != null)
            quantityText.text = data.quantity > 1 ? $"x{data.quantity}" : string.Empty;

        SetHighlight(false);
        SetRarityColor(data.rarity);
    }

    // Executes clear slot operation.
    public virtual void ClearSlot()
    {
        BindCore();

        RawData = null;
        DisplayData = null;
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
        if (itemNameText != null)
            itemNameText.text = string.Empty;
        if (quantityText != null)
            quantityText.text = string.Empty;
        if (rarityBorder != null)
            rarityBorder.enabled = false;
        rarityEffect?.SetVisible(false);

        SetHighlight(false);
    }

    // Executes set highlight operation.
    public void SetHighlight(bool isActive)
    {
        if (selectHighlight != null)
            selectHighlight.SetActive(isActive);
    }

    // Executes setup custom operation.
    public virtual void SetupCustom(string displayName, string amountText, Sprite icon)
    {
        BindCore();

        RawData = null;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
        }

        if (itemNameText != null)
            itemNameText.text = displayName ?? string.Empty;

        if (quantityText != null)
            quantityText.text = amountText ?? string.Empty;

        SetHighlight(false);

        rarityEffect?.SetVisible(false);
    }

    // Executes set rarity color operation.
    protected virtual void SetRarityColor(string rarity)
    {
        bool isSkinSlot = this is UIInventorySkinSlot ||
                         (DisplayData != null && (DisplayData.isSkin || string.Equals(DisplayData.category, "Skin", StringComparison.OrdinalIgnoreCase))) ||
                         RawData is MysticJourney.API.Models.Response.PlayerSkinSummaryResponse;

        if (isSkinSlot || IsConsumableOrNonEquip(DisplayData, RawData))
        {
            if (rarityBorder != null) rarityBorder.enabled = false;
            rarityEffect?.SetVisible(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(rarity))
            rarity = "Common";

        GameObject targetObj = rarityBorder != null ? rarityBorder.gameObject : gameObject;

        if (rarityEffect == null || rarityEffect.gameObject != targetObj)
        {
            if (rarityEffect != null && rarityEffect.gameObject != targetObj)
                rarityEffect.SetVisible(false);

            rarityEffect = targetObj.GetComponent<UIRarityFrameEffect>()
                ?? targetObj.AddComponent<UIRarityFrameEffect>();
        }

        rarityEffect.Configure(rarity, rarityBorder);

        if (rarityBorder != null)
            rarityBorder.enabled = false;

        if (iconImage != null) iconImage.transform.SetAsLastSibling();
        if (quantityText != null) quantityText.transform.SetAsLastSibling();
    }

    // Executes is consumable or non equip operation.
    // Validates input parameters against null or empty values.
    // Evaluates conditions and returns a boolean result.
    public static bool IsConsumableOrNonEquip(UIItemDisplayData data, object rawData)
    {
        if (data != null && (data.isSkin || string.Equals(data.category, "Skin", System.StringComparison.OrdinalIgnoreCase)))
            return true;

        if (rawData is MysticJourney.API.Models.Response.PlayerSkinSummaryResponse)
            return true;

        if (data != null && data.isEquipped)
            return false;

        string category = data?.category;
        string name = data?.itemName;

        if (rawData is MysticJourney.API.Models.Response.InventoryItemResponse invItem)
        {
            if (invItem.IsEquipped || !string.IsNullOrEmpty(invItem.ItemSlot) || !string.IsNullOrEmpty(invItem.EquippedSlot))
                return false;

            category = !string.IsNullOrEmpty(invItem.ItemType) ? invItem.ItemType : category;
            name = !string.IsNullOrEmpty(invItem.ItemName) ? invItem.ItemName : name;

            if (!string.IsNullOrEmpty(category))
            {
                string catUpper = category.Trim();
                if (string.Equals(catUpper, "Weapon", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(catUpper, "Armor", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(catUpper, "Helmet", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(catUpper, "Gloves", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(catUpper, "Boots", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(catUpper, "Ring", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(catUpper, "Necklace", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(catUpper, "Shield", System.StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        if (!string.IsNullOrEmpty(category))
        {
            string cat = category.Trim();
            if (string.Equals(cat, "Consumable", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cat, "Potion", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cat, "Material", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cat, "Ticket", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cat, "QuestItem", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cat, "Quest", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cat, "Currency", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(name))
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("potion") || n.Contains("máu") || n.Contains("ticket") || n.Contains("vé") ||
                n.Contains("flour") || n.Contains("stone") || n.Contains("scroll") || n.Contains("key"))
            {
                return true;
            }
        }

        return false;
    }


    // Executes on pointer click operation.
    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (RawData != null)
            OnSlotClicked?.Invoke(this);
    }

    // Executes ensure icon centered operation.
    protected void EnsureIconCentered()
    {
        if (iconImage == null) return;
        if (this is UIShopSlot) return;

        RectTransform rect = iconImage.rectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }
    }

    // Executes bind core operation.
    protected void BindCore()
    {
        if (iconImage == null)
            iconImage = FindChild("Icon", "ItemIcon", "Image")?.GetComponent<Image>();
        if (itemNameText == null)
            itemNameText = FindChild("Name", "ItemName", "Title", "TitleText")?.GetComponent<TMP_Text>();
        if (quantityText == null)
            quantityText = FindChild("Quantity", "QuantityText", "Amount", "AmountText", "ItemAmountText")?.GetComponent<TMP_Text>();
        if (rarityBorder == null)
            rarityBorder = FindChild("RarityBorder", "Border", "Frame")?.GetComponent<Image>();
        if (selectHighlight == null)
            selectHighlight = FindChild("SelectHighlight", "Highlight", "Selected")?.gameObject;

        EnsureIconCentered();
    }

    // Executes find child operation.
    private Transform FindChild(params string[] names)
    {
        var children = GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            for (var j = 0; j < names.Length; j++)
            {
                if (children[i] != null && children[i].name == names[j])
                    return children[i];
            }
        }

        return null;
    }

    // Executes rarity to color operation.
    // Validates input parameters against null or empty values.
    private static Color RarityToColor(string rarity)
    {
        if (string.IsNullOrWhiteSpace(rarity))
            return Color.white;

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
}
