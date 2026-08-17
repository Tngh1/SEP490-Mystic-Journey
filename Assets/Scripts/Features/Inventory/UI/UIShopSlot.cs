using System;
using System.Globalization;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Serialization;

using UnityEngine.EventSystems;

// Executes i pointer exit handler operation.
public class UIShopSlot : UIBaseItemSlot, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Shop Specifics")]
    [FormerlySerializedAs("nameText")]
    [SerializeField] private TMP_Text shopNameText;
    [Tooltip("Group chứa Coin mặc định")]
    [SerializeField] private GameObject priceGroup;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Image currencyIconImage;

    [Header("Gem Currency (Tùy chọn)")]
    [Tooltip("Group chứa Gem")]
    [SerializeField] private GameObject gemGroup;
    [SerializeField] private TMP_Text gemPriceText;
    [SerializeField] private Button buyButton;

    // Initializes internal component caches and dependencies for UIShopSlot upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(OnBuyButtonClicked);
    }

    // Executes on pointer enter operation.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (DisplayData != null)
        {
            var tooltip = UIShopItemTooltip.GetOrCreate(GetComponentInParent<Canvas>());
            tooltip?.ShowTooltip(DisplayData, transform as RectTransform);
        }
    }

    // Executes on pointer exit operation.
    // Validates input parameters against null or empty values.
    public void OnPointerExit(PointerEventData eventData)
    {
        UIShopItemTooltip.Instance?.HideTooltip();
    }

    // Executes setup shop operation.
    public void SetupShop(UIItemDisplayData data)
    {
        if (data == null)
        {
            ClearSlot();
            return;
        }

        base.SetupCore(data);
        RawData = data;

        if (iconImage != null)
        {
            iconImage.rectTransform.anchoredPosition = new Vector2(0f, 27.8f);
        }

        if (shopNameText == null && itemNameText == null)
        {
            shopNameText = transform.Find("NameText")?.GetComponent<TMP_Text>()
                        ?? transform.Find("TitleText")?.GetComponent<TMP_Text>()
                        ?? transform.Find("Name")?.GetComponent<TMP_Text>();
        }

        TMP_Text nameLabel = shopNameText != null ? shopNameText : itemNameText;
        if (nameLabel != null)
        {
            nameLabel.enableAutoSizing = false;
            nameLabel.enableWordWrapping = true;
            nameLabel.fontSize = 20f;
            nameLabel.fontStyle = FontStyles.Bold;
            nameLabel.overflowMode = TextOverflowModes.Overflow;
            nameLabel.alignment = TextAlignmentOptions.Center;
            nameLabel.margin = Vector4.zero;
            nameLabel.text = data.itemName ?? string.Empty;
        }

        if (quantityText != null)
        {
            quantityText.enableAutoSizing = true;
            quantityText.fontSizeMin = 15f;
            quantityText.fontSizeMax = 26f;

            bool isDailyDeal = string.Equals(data.shopSection, "DailyDeals", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(data.shopSection, "DailyDeal", System.StringComparison.OrdinalIgnoreCase) ||
                               data.HasDealPrice ||
                               data.dailyPurchaseLimit > 0;

            if (isDailyDeal)
            {
                if (data.dailyPurchaseLimit > 0)
                {
                    int remaining = Mathf.Max(0, data.remainingDailyPurchases >= 0 ? data.remainingDailyPurchases : data.dailyPurchaseLimit - data.purchasedToday);
                    quantityText.text = $"x{remaining}";
                }
                else if (data.weeklyPurchaseLimit > 0)
                {
                    int remaining = Mathf.Max(0, data.remainingWeeklyPurchases >= 0 ? data.remainingWeeklyPurchases : data.weeklyPurchaseLimit - data.purchasedThisWeek);
                    quantityText.text = $"x{remaining}";
                }
                else if (!data.isUnlimitedStock && data.stock >= 0)
                {
                    quantityText.text = $"x{data.stock}";
                }
                else if (data.quantity > 1)
                {
                    quantityText.text = $"x{data.quantity}";
                }
                else
                {
                    quantityText.text = string.Empty;
                }
            }
            else
            {
                quantityText.text = string.Empty;
            }
        }

        string curr = data.currency;
        if (string.IsNullOrWhiteSpace(curr)) curr = "Gold";

        bool isGem = curr.Equals("Gem", StringComparison.OrdinalIgnoreCase) ||
                     curr.Equals("Gems", StringComparison.OrdinalIgnoreCase);

        if (isGem && gemGroup != null)
        {
            if (priceGroup != null) priceGroup.SetActive(false);
            gemGroup.SetActive(true);

            if (gemPriceText != null)
            {
                gemPriceText.enableAutoSizing = false;
                gemPriceText.fontSize = 24f;
                gemPriceText.fontStyle = FontStyles.Bold;
                gemPriceText.richText = true;
                gemPriceText.alignment = TextAlignmentOptions.Center;
                gemPriceText.overflowMode = TextOverflowModes.Overflow;
                gemPriceText.margin = Vector4.zero;
                gemPriceText.text = FormatDisplayPrice(data);
                if (gemPriceText.rectTransform != null && gemPriceText.rectTransform.rect.height < 28f)
                    gemPriceText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 30f);
            }
        }
        else
        {
            if (priceGroup != null) priceGroup.SetActive(true);
            if (gemGroup != null) gemGroup.SetActive(false);

            if (priceText != null)
            {
                priceText.enableAutoSizing = false;
                priceText.fontSize = 24f;
                priceText.fontStyle = FontStyles.Bold;
                priceText.richText = true;
                priceText.alignment = TextAlignmentOptions.Center;
                priceText.overflowMode = TextOverflowModes.Overflow;
                priceText.margin = Vector4.zero;
                priceText.text = FormatDisplayPrice(data);
                if (priceText.rectTransform != null && priceText.rectTransform.rect.height < 28f)
                    priceText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 30f);
            }
        }

        if (currencyIconImage != null && data.currencyIcon != null)
        {
            currencyIconImage.sprite = data.currencyIcon;
            currencyIconImage.enabled = true;
        }

        if (buyButton != null)
            buyButton.interactable = data.canPurchase && data.GetMaxPurchaseQuantity() > 0;
    }

    // Executes clear slot operation.
    public override void ClearSlot()
    {
        base.ClearSlot();
        if (priceGroup != null) priceGroup.SetActive(false);
        if (gemGroup != null) gemGroup.SetActive(false);

        TMP_Text nameLabel = shopNameText != null ? shopNameText : itemNameText;
        if (nameLabel != null) nameLabel.text = string.Empty;
        if (quantityText != null) quantityText.text = string.Empty;
        if (buyButton != null) buyButton.interactable = true;
    }

    // Executes on buy button clicked operation.
    private void OnBuyButtonClicked()
    {
        if (RawData != null)
            OnSlotClicked?.Invoke(this);
    }

    // Executes format display price operation.
    private static string FormatDisplayPrice(UIItemDisplayData data)
    {
        if (data == null)
            return FormatPrice(0, string.Empty);

        string currentPrice = FormatPrice(data.EffectiveUnitPrice, string.Empty);
        if (!data.HasDealPrice)
            return currentPrice;

        string originalPrice = FormatPrice(data.originalUnitPrice, string.Empty);
        return $"<size=80%><s><color=#9CA3AF>{originalPrice}</color></s></size> <b><color=#FFD34D>{currentPrice}</color></b>";
    }

    // Executes format price operation.
    private static string FormatPrice(decimal amount, string currency)
    {
        string formatted = amount.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
        return formatted;
    }
}
