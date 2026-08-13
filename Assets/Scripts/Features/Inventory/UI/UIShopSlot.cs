using System;
using System.Globalization;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class UIShopSlot : UIBaseItemSlot
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

    private void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(OnBuyButtonClicked);
    }

    public void SetupShop(UIItemDisplayData data)
    {
        if (data == null)
        {
            ClearSlot();
            return;
        }

        base.SetupCore(data);
        RawData = data;

        // 1. Tên vật phẩm: hiển thị tên sạch (không đính kèm số lượng), cân chỉnh căn giữa + Auto-size
        TMP_Text nameLabel = shopNameText != null ? shopNameText : itemNameText;
        if (nameLabel != null)
        {
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 20f;
            nameLabel.fontSizeMax = 30f;
            nameLabel.alignment = TextAlignmentOptions.Center;
            nameLabel.margin = new Vector4(2, 2, 2, 2);
            nameLabel.text = data.itemName ?? string.Empty;
        }

        // 2. Tồn kho / Số lượng bán: Hiển thị ở TMP_Text "Quanlity" (quantityText) góc dưới ảnh Icon cho Daily Deals.
        // Shop bình thường: KHÔNG hiển thị tồn kho.
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
                // Shop bình thường: ẩn tồn kho
                quantityText.text = string.Empty;
            }
        }

        // 3. Giá tiền & Hiển thị Gem / Coin
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
                gemPriceText.enableAutoSizing = true;
                gemPriceText.fontSizeMin = 12f;
                gemPriceText.fontSizeMax = 22f;
                gemPriceText.richText = true;
                gemPriceText.alignment = TextAlignmentOptions.Center;
                gemPriceText.margin = new Vector4(2, 0, 2, 0);
                gemPriceText.text = FormatDisplayPrice(data);
            }
        }
        else
        {
            if (priceGroup != null) priceGroup.SetActive(true);
            if (gemGroup != null) gemGroup.SetActive(false);
            
            if (priceText != null)
            {
                priceText.enableAutoSizing = true;
                priceText.fontSizeMin = 12f;
                priceText.fontSizeMax = 22f;
                priceText.richText = true;
                priceText.alignment = TextAlignmentOptions.Center;
                priceText.margin = new Vector4(2, 0, 2, 0);
                priceText.text = FormatDisplayPrice(data);
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

    private void OnBuyButtonClicked()
    {
        if (RawData != null)
            OnSlotClicked?.Invoke(this);
    }

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

    private static string FormatPrice(decimal amount, string currency)
    {
        string formatted = amount.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
        return $"${formatted}";
    }
}
