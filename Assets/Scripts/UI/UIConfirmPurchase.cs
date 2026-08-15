using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private Image currencyIconImage;

    [Header("Action Buttons")]
    public Button confirmButton;
    public Button cancelButton;

    private UIItemDisplayData currentItem;
    private int currentQuantity = 1;
    private int maxQuantity = 99;
    private bool waitingForBalance;

    public event Action<UIItemDisplayData, int> OnConfirmPurchase;

    private void Awake()
    {
        TryAutoBindCurrencyIcon();
        EnsureMaxButton();
        if (plusButton != null) { plusButton.onClick.RemoveAllListeners(); plusButton.onClick.AddListener(IncreaseQuantity); }
        if (minusButton != null) { minusButton.onClick.RemoveAllListeners(); minusButton.onClick.AddListener(DecreaseQuantity); }
        if (maxButton != null) { maxButton.onClick.RemoveAllListeners(); maxButton.onClick.AddListener(SetMaxQuantity); }
        if (confirmButton != null) { confirmButton.onClick.RemoveAllListeners(); confirmButton.onClick.AddListener(Confirm); }
        if (cancelButton != null) { cancelButton.onClick.RemoveAllListeners(); cancelButton.onClick.AddListener(Cancel); }
    }

    public void Setup(UIItemDisplayData itemData)
    {
        currentItem = itemData;
        waitingForBalance = currentItem != null && !HasCachedBalance(currentItem);
        maxQuantity = waitingForBalance
            ? 0
            : CalculateAffordableQuantity(currentItem, PlayerHUDController.CachedGold, PlayerHUDController.CachedGems);
        currentQuantity = maxQuantity > 0 ? 1 : 0;
        if (waitingForBalance)
            PlayerHUDController.Instance?.RefreshCurrencyBalance();

        if (titleText != null) titleText.text = "Confirm Purchase";
        if (itemNameText != null)
        {
            itemNameText.text = currentItem?.itemName ?? string.Empty;
            if (currentItem != null && currentItem.weeklyPurchaseLimit > 0)
                itemNameText.text += $" (Tuần: {Mathf.Max(0, currentItem.remainingWeeklyPurchases)}/{currentItem.weeklyPurchaseLimit})";
            else if (currentItem != null && currentItem.dailyPurchaseLimit > 0)
                itemNameText.text += $" (Ngày: {Mathf.Max(0, currentItem.remainingDailyPurchases)}/{currentItem.dailyPurchaseLimit})";
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
        if (currencyNameText != null)
            currencyNameText.text = string.IsNullOrWhiteSpace(currentItem?.currency) ? "Gold" : currentItem.currency;

        UpdateCurrencyIcon();
        UpdateUI();
    }

    private void Update()
    {
        if (!waitingForBalance || currentItem == null || !HasCachedBalance(currentItem)) return;
        waitingForBalance = false;
        maxQuantity = CalculateAffordableQuantity(currentItem, PlayerHUDController.CachedGold, PlayerHUDController.CachedGems);
        currentQuantity = maxQuantity > 0 ? 1 : 0;
        UpdateUI();
    }

    private void IncreaseQuantity()
    {
        if (currentQuantity >= maxQuantity) return;
        currentQuantity++;
        UpdateUI();
    }

    private void DecreaseQuantity()
    {
        if (currentQuantity <= 1) return;
        currentQuantity--;
        UpdateUI();
    }

    private void SetMaxQuantity()
    {
        if (currentItem == null || maxQuantity <= 0) return;
        currentQuantity = maxQuantity;
        UpdateUI();
    }

    public static int CalculateAffordableQuantity(UIItemDisplayData item, decimal gold, decimal gems)
    {
        if (item == null) return 0;

        int itemLimit = item.GetMaxPurchaseQuantity();
        decimal price = item.EffectiveUnitPrice;
        if (itemLimit <= 0 || price <= 0) return itemLimit;

        string currency = (item.currency ?? "Gold").Trim();
        bool isGems = currency.Equals("Gem", StringComparison.OrdinalIgnoreCase) ||
                      currency.Equals("Gems", StringComparison.OrdinalIgnoreCase) ||
                      currency.Equals("Diamond", StringComparison.OrdinalIgnoreCase);
        decimal balance = isGems ? gems : gold;
        if (balance < 0) return itemLimit;

        decimal affordable = Math.Floor(balance / price);
        if (affordable <= 0) return 0;
        return affordable >= itemLimit ? itemLimit : (int)affordable;
    }

    private static bool HasCachedBalance(UIItemDisplayData item)
    {
        string currency = item?.currency ?? "Gold";
        bool isGems = currency.Equals("Gem", StringComparison.OrdinalIgnoreCase) ||
                      currency.Equals("Gems", StringComparison.OrdinalIgnoreCase) ||
                      currency.Equals("Diamond", StringComparison.OrdinalIgnoreCase);
        return isGems ? PlayerHUDController.CachedGems >= 0 : PlayerHUDController.CachedGold >= 0;
    }

    private void UpdateUI()
    {
        if (quantityText != null) quantityText.text = currentQuantity.ToString();
        if (totalPriceText != null)
            totalPriceText.text = FormatAmount((currentItem?.EffectiveUnitPrice ?? 0) * currentQuantity);

        bool canConfirm = currentItem != null && currentItem.canPurchase && currentQuantity > 0;
        if (confirmButton != null) confirmButton.interactable = canConfirm;
        if (plusButton != null) plusButton.interactable = currentQuantity > 0 && currentQuantity < maxQuantity;
        if (minusButton != null) minusButton.interactable = currentQuantity > 1;
        if (maxButton != null) maxButton.interactable = maxQuantity > 1 && currentQuantity < maxQuantity;
    }

    private void Confirm()
    {
        if (currentItem == null || currentQuantity <= 0 || !currentItem.canPurchase) return;
        OnConfirmPurchase?.Invoke(currentItem, currentQuantity);
        gameObject.SetActive(false);
    }

    private void Cancel() => gameObject.SetActive(false);

    private void TryAutoBindCurrencyIcon()
    {
        if (currencyIconImage != null) return;
        Transform icon = transform.Find("InnerBox/Bg/Image");
        if (icon != null) currencyIconImage = icon.GetComponent<Image>();
    }

    private void UpdateCurrencyIcon()
    {
        TryAutoBindCurrencyIcon();
        if (currencyIconImage == null || currentItem == null) return;

        Sprite icon = currentItem.currencyIcon;
        if (icon == null && ItemIconDatabase.Instance != null)
        {
            string currency = currentItem.currency ?? "Gold";
            bool isGems = currency.Equals("Gem", StringComparison.OrdinalIgnoreCase) ||
                          currency.Equals("Gems", StringComparison.OrdinalIgnoreCase) ||
                          currency.Equals("Diamond", StringComparison.OrdinalIgnoreCase);
            icon = ItemIconDatabase.Instance.GetIcon(isGems ? "Gem" : "Gold", "Currency");
        }

        if (icon != null) currencyIconImage.sprite = icon;
        currencyIconImage.enabled = icon != null;
    }

    private void EnsureMaxButton()
    {
        if (maxButton != null || plusButton == null || plusButton.transform.parent == null) return;

        maxButton = Instantiate(plusButton, plusButton.transform.parent);
        maxButton.name = "MaxButton";
        maxButton.transform.SetAsLastSibling();
        maxButton.onClick.RemoveAllListeners();
        for (int i = 0; i < maxButton.transform.childCount; i++)
            maxButton.transform.GetChild(i).gameObject.SetActive(false);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(maxButton.transform, false);
        var rect = (RectTransform)labelObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "MAX";
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = 13f;
        label.raycastTarget = false;
        if (quantityText != null) label.font = quantityText.font;

        var layout = maxButton.gameObject.GetComponent<LayoutElement>();
        if (layout == null) layout = maxButton.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = 55f;
        layout.preferredWidth = 62f;
        layout.flexibleWidth = 0f;

        if (plusButton.transform.parent is RectTransform groupRect)
            groupRect.sizeDelta = new Vector2(groupRect.sizeDelta.x + 74f, groupRect.sizeDelta.y);
    }

    private static string FormatDisplayPrice(UIItemDisplayData item)
    {
        if (item == null) return FormatAmount(0);
        string currentPrice = FormatAmount(item.EffectiveUnitPrice);
        if (!item.HasDealPrice) return currentPrice;
        return $"<s><color=#9CA3AF>{FormatAmount(item.originalUnitPrice)}</color></s> <b><color=#FFD34D>{currentPrice}</color></b>";
    }

    private static string FormatAmount(decimal amount)
        => amount.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
}
