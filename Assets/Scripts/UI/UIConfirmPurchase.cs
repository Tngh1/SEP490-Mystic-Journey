using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIConfirmPurchase : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text titleText;
    public TMP_Text itemNameText;
    public Image itemIcon;
    public TMP_Text itemPriceText;

    [Header("Quantity Controls")]
    public TMP_Text quantityText;
    public Button plusButton;
    public Button minusButton;
    public Button maxButton;

    [Header("Totals")]
    public TMP_Text totalPriceText;
    public TMP_Text currencyNameText;

    [Header("Action Buttons")]
    public Button confirmButton;
    public Button cancelButton;

    private UIItemDisplayData currentItem;
    private int currentQuantity = 1;
    private int maxQuantity = 99;

    public event Action<UIItemDisplayData, int> OnConfirmPurchase;

    private void Awake()
    {
        if (plusButton != null) { plusButton.onClick.RemoveAllListeners(); plusButton.onClick.AddListener(IncreaseQuantity); }
        if (minusButton != null) { minusButton.onClick.RemoveAllListeners(); minusButton.onClick.AddListener(DecreaseQuantity); }
        if (maxButton != null) { maxButton.onClick.RemoveAllListeners(); maxButton.onClick.AddListener(SetMaxQuantity); }
        if (confirmButton != null) { confirmButton.onClick.RemoveAllListeners(); confirmButton.onClick.AddListener(Confirm); }
        if (cancelButton != null) { cancelButton.onClick.RemoveAllListeners(); cancelButton.onClick.AddListener(Cancel); }
    }

    public void Setup(UIItemDisplayData itemData)
    {
        currentItem = itemData;
        maxQuantity = currentItem?.GetMaxPurchaseQuantity() ?? 0;
        currentQuantity = maxQuantity > 0 ? 1 : 0;

        if (titleText != null) titleText.text = "Confirm Purchase";

        if (itemNameText != null) 
        {
            itemNameText.text = currentItem?.itemName ?? string.Empty;
            if (currentItem != null && currentItem.weeklyPurchaseLimit > 0)
            {
                int remaining = Mathf.Max(0, currentItem.remainingWeeklyPurchases);
                itemNameText.text += $" (Tuần: {remaining}/{currentItem.weeklyPurchaseLimit})";
            }
            else if (currentItem != null && currentItem.dailyPurchaseLimit > 0)
            {
                int remaining = Mathf.Max(0, currentItem.remainingDailyPurchases);
                itemNameText.text += $" (Ngày: {remaining}/{currentItem.dailyPurchaseLimit})";
            }
        }
        if (itemIcon != null)
        {
            itemIcon.sprite = currentItem?.icon;
            itemIcon.enabled = currentItem?.icon != null;
        }
        if (itemPriceText != null)
        {
            itemPriceText.richText = true;
            itemPriceText.text = FormatDisplayPrice(currentItem);
        }
        if (currencyNameText != null) currencyNameText.text = string.IsNullOrWhiteSpace(currentItem?.currency) ? "Gold" : currentItem.currency;

        UpdateUI();
    }

    private void IncreaseQuantity()
    {
        if (currentQuantity < maxQuantity)
        {
            currentQuantity++;
            UpdateUI();
        }
    }

    private void DecreaseQuantity()
    {
        if (currentQuantity > 1)
        {
            currentQuantity--;
            UpdateUI();
        }
    }

    private void SetMaxQuantity()
    {
        if (currentItem == null) return;

        decimal price = currentItem.EffectiveUnitPrice;

        // Tính max theo số tiền đang có trong túi
        int affordableQty = maxQuantity; // fallback: không có balance cache → dùng max giới hạn cũ
        if (price > 0)
        {
            string currency = (currentItem.currency ?? "Gold").Trim();
            bool isGems = currency.Equals("Gem", StringComparison.OrdinalIgnoreCase) ||
                          currency.Equals("Gems", StringComparison.OrdinalIgnoreCase) ||
                          currency.Equals("Diamond", StringComparison.OrdinalIgnoreCase);

            decimal balance = isGems ? PlayerHUDController.CachedGems : PlayerHUDController.CachedGold;

            if (balance >= 0) // balance đã được cache (>= 0)
            {
                // Số lượng tối đa mua được với số tiền hiện có
                affordableQty = (int)Math.Floor(balance / price);
            }
        }

        // Clamp: không vượt stock / daily / weekly limit, tối thiểu 1
        currentQuantity = Mathf.Clamp(Mathf.Min(affordableQty, maxQuantity), 1, maxQuantity);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (quantityText != null) quantityText.text = currentQuantity.ToString();

        if (totalPriceText != null)
        {
            decimal total = (currentItem?.EffectiveUnitPrice ?? 0) * currentQuantity;
            totalPriceText.text = FormatAmount(total);
        }

        bool canConfirm = currentItem != null && currentItem.canPurchase && currentQuantity > 0;
        if (confirmButton != null) confirmButton.interactable = canConfirm;
        if (plusButton != null) plusButton.interactable = currentQuantity > 0 && currentQuantity < maxQuantity;
        if (minusButton != null) minusButton.interactable = currentQuantity > 1;
        if (maxButton != null) maxButton.interactable = maxQuantity > 1 && currentQuantity < maxQuantity;
    }

    private void Confirm()
    {
        if (currentItem == null || currentQuantity <= 0 || !currentItem.canPurchase)
            return;

        OnConfirmPurchase?.Invoke(currentItem, currentQuantity);
        gameObject.SetActive(false);
    }

    private void Cancel()
    {
        gameObject.SetActive(false);
    }

    private static string FormatDisplayPrice(UIItemDisplayData item)
    {
        if (item == null)
            return FormatAmount(0);

        string currentPrice = FormatAmount(item.EffectiveUnitPrice);
        if (!item.HasDealPrice)
            return currentPrice;

        string originalPrice = FormatAmount(item.originalUnitPrice);
        return $"<s><color=#9CA3AF>{originalPrice}</color></s> <b><color=#FFD34D>{currentPrice}</color></b>";
    }

    private static string FormatAmount(decimal amount)
    {
        return amount.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
    }
}
