using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    public object RawData { get; protected set; }
    public UIItemDisplayData DisplayData { get; protected set; }

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
        }

        if (itemNameText != null)
            itemNameText.text = data.itemName ?? string.Empty;

        if (quantityText != null)
            quantityText.text = data.quantity > 1 ? $"x{data.quantity}" : string.Empty;

        SetHighlight(false);
        SetRarityColor(data.rarity);
    }

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

    public void SetHighlight(bool isActive)
    {
        if (selectHighlight != null)
            selectHighlight.SetActive(isActive);
    }

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

    protected virtual void SetRarityColor(string rarity)
    {
        if (rarityBorder == null)
            return;

        // Keep a real sprite frame when the prefab provides one. The current inventory prefab
        // has no frame sprite, so use a generated four-edge effect that never covers the icon.
        if (rarityBorder.sprite != null)
        {
            rarityEffect?.SetVisible(false);
            rarityBorder.enabled = true;
            rarityBorder.color = UIRarityFrameEffect.GetRarityColor(rarity);
            return;
        }

        rarityBorder.enabled = false;
        if (rarityEffect == null)
            rarityEffect = rarityBorder.GetComponent<UIRarityFrameEffect>()
                ?? rarityBorder.gameObject.AddComponent<UIRarityFrameEffect>();

        rarityEffect.Configure(rarity);
    }


    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (RawData != null)
            OnSlotClicked?.Invoke(this);
    }

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
    }

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
