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
        InitializeUI();
    }

    private void OnEnable()
    {
        InitializeUI();
        UpdateUI();
    }

    private void InitializeUI()
    {
        EnsureButtonsBound();
        TryAutoBindCurrencyIcon();
        EnsureMaxButton();
        ArrangeQuantityGroup();
        BindButtonListeners();
    }

    private void BindButtonListeners()
    {
        if (minusButton != null)
        {
            minusButton.onClick.RemoveAllListeners();
            minusButton.onClick.AddListener(DecreaseQuantity);
        }

        if (plusButton != null)
        {
            plusButton.onClick.RemoveAllListeners();
            plusButton.onClick.AddListener(IncreaseQuantity);
        }

        if (maxButton != null)
        {
            maxButton.onClick.RemoveAllListeners();
            maxButton.onClick.AddListener(SetMaxQuantity);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(Confirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(Cancel);
        }
    }

    public void Setup(UIItemDisplayData itemData)
    {
        InitializeUI();
        currentItem = itemData;
        waitingForBalance = currentItem != null && !HasCachedBalance(currentItem);
        maxQuantity = waitingForBalance
            ? 0
            : CalculateAffordableQuantity(currentItem, PlayerHUDUIManager.CachedGold, PlayerHUDUIManager.CachedGems);
        currentQuantity = maxQuantity > 0 ? 1 : 0;
        if (waitingForBalance)
            PlayerHUDUIManager.Instance?.RefreshCurrencyBalance();

        if (titleText != null)
        {
            titleText.enableAutoSizing = false;
            titleText.fontSize = 26f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.enableWordWrapping = false;
            titleText.overflowMode = TextOverflowModes.Overflow;
            titleText.text = "Confirm Purchase";
        }

        if (itemNameText != null)
        {
            itemNameText.enableAutoSizing = false;
            itemNameText.enableWordWrapping = true;
            itemNameText.fontSize = 28f;
            itemNameText.fontStyle = FontStyles.Bold;
            itemNameText.overflowMode = TextOverflowModes.Overflow;
            itemNameText.alignment = TextAlignmentOptions.Center;
            itemNameText.margin = Vector4.zero;
            itemNameText.text = currentItem?.itemName ?? string.Empty;
            if (itemNameText.rectTransform != null)
                itemNameText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40f);
        }

        if (itemIcon != null)
        {
            itemIcon.sprite = currentItem?.icon;
            itemIcon.enabled = currentItem?.icon != null;
        }

        if (itemPriceText != null)
        {
            itemPriceText.enableAutoSizing = false;
            itemPriceText.fontSize = 26f;
            itemPriceText.fontStyle = FontStyles.Bold;
            itemPriceText.richText = true;
            itemPriceText.overflowMode = TextOverflowModes.Overflow;
            itemPriceText.text = FormatDisplayPrice(currentItem);
        }

        if (currencyNameText != null)
        {
            currencyNameText.enableAutoSizing = false;
            currencyNameText.fontSize = 22f;
            currencyNameText.fontStyle = FontStyles.Bold;
            currencyNameText.text = string.IsNullOrWhiteSpace(currentItem?.currency) ? "Gold" : currentItem.currency;
        }

        UpdateCurrencyIcon();
        ArrangeQuantityGroup();
        UpdateUI();
    }

    private void Update()
    {
        if (!waitingForBalance || currentItem == null || !HasCachedBalance(currentItem)) return;
        waitingForBalance = false;
        maxQuantity = CalculateAffordableQuantity(currentItem, PlayerHUDUIManager.CachedGold, PlayerHUDUIManager.CachedGems);
        currentQuantity = maxQuantity > 0 ? 1 : 0;
        UpdateUI();
    }

    private void IncreaseQuantity()
    {
        if (maxQuantity > 0 && currentQuantity >= maxQuantity) return;
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
        if (currentQuantity >= maxQuantity) return;
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
        return isGems ? PlayerHUDUIManager.CachedGems >= 0 : PlayerHUDUIManager.CachedGold >= 0;
    }

    private void UpdateUI()
    {
        if (titleText != null)
        {
            titleText.enableAutoSizing = false;
            titleText.fontSize = 26f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.text = "Confirm Purchase";
        }

        if (itemNameText != null)
        {
            itemNameText.enableAutoSizing = false;
            itemNameText.fontSize = 28f;
            itemNameText.fontStyle = FontStyles.Bold;
        }

        if (quantityText != null)
        {
            quantityText.enableAutoSizing = false;
            quantityText.fontSize = 26f;
            quantityText.fontStyle = FontStyles.Bold;
            quantityText.text = currentQuantity.ToString();
        }

        if (totalPriceText != null)
        {
            totalPriceText.enableAutoSizing = false;
            totalPriceText.fontSize = 26f;
            totalPriceText.fontStyle = FontStyles.Bold;
            totalPriceText.overflowMode = TextOverflowModes.Overflow;
            totalPriceText.text = FormatAmount((currentItem?.EffectiveUnitPrice ?? 0) * currentQuantity);
        }

        bool canConfirm = currentItem != null && currentItem.canPurchase && currentQuantity > 0;
        if (confirmButton != null) confirmButton.interactable = canConfirm;

        if (minusButton != null)
        {
            minusButton.interactable = currentQuantity > 1;
        }
        if (plusButton != null)
        {
            plusButton.interactable = maxQuantity > 0 && currentQuantity < maxQuantity;
        }
        if (maxButton != null)
        {
            maxButton.interactable = maxQuantity > 1 && currentQuantity < maxQuantity;
        }
    }

    private void Confirm()
    {
        if (currentItem == null || currentQuantity <= 0 || !currentItem.canPurchase) return;
        OnConfirmPurchase?.Invoke(currentItem, currentQuantity);
        gameObject.SetActive(false);
    }

    private void Cancel() => gameObject.SetActive(false);

    private void AutoBindAllReferences()
    {
        TMP_Text[] allTexts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var txt in allTexts)
        {
            string n = txt.name.ToLower();
            string val = txt.text.ToLower();

            if (titleText == null && (n.Contains("title") || n.Contains("header") || val.Contains("comfirm") || val.Contains("confirm")))
            {
                titleText = txt;
            }
            else if (itemNameText == null && (n.Contains("itemname") || n.Contains("name") || n.Contains("item")))
            {
                itemNameText = txt;
            }
            else if (totalPriceText == null && (n.Contains("total") || n.Contains("sum") || n.Contains("price")))
            {
                totalPriceText = txt;
            }
        }

        Button[] allButtons = GetComponentsInChildren<Button>(true);
        foreach (var btn in allButtons)
        {
            string n = btn.name.ToLower();
            TMP_Text label = btn.GetComponentInChildren<TMP_Text>(true);
            string txtVal = label != null ? label.text.ToLower() : "";

            if (confirmButton == null && (n.Contains("confirm") || n.Contains("buy") || txtVal.Contains("buy") || txtVal.Contains("confirm")))
            {
                confirmButton = btn;
            }
            else if (cancelButton == null && (n.Contains("cancel") || n.Contains("close") || txtVal.Contains("cancel") || txtVal.Contains("close")))
            {
                cancelButton = btn;
            }
        }
    }

    private void EnsureButtonsBound()
    {
        AutoBindAllReferences();

        Button[] allButtons = GetComponentsInChildren<Button>(true);

        foreach (var b in allButtons)
        {
            if (b == confirmButton || b == cancelButton || b.name == "DimBackground") continue;

            string n = b.name.ToLower();
            TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
            string txt = t != null ? t.text.Trim() : "";

            if (minusButton == null && (n.Contains("minus") || n.Contains("sub") || n.Contains("dec") || txt == "-" || txt == "—"))
            {
                minusButton = b;
            }
            else if (plusButton == null && (n.Contains("plus") || n.Contains("add") || n.Contains("inc") || txt == "+"))
            {
                plusButton = b;
            }
            else if (maxButton == null && (n.Contains("max") || txt.Equals("max", StringComparison.OrdinalIgnoreCase)))
            {
                maxButton = b;
            }
        }

        Transform groupTransform = transform.Find("InnerBox/QuantityGroup") ??
                                   transform.Find("QuantityGroup") ??
                                   transform.Find("InnerBox/Bg/QuantityGroup") ??
                                   (plusButton != null ? plusButton.transform.parent : (minusButton != null ? minusButton.transform.parent : null));

        if (groupTransform == null)
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.IndexOf("quantity", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    groupTransform = child;
                    break;
                }
            }
        }

        if (groupTransform != null)
        {
            var validBtns = new System.Collections.Generic.List<Button>();
            foreach (Button b in groupTransform.GetComponentsInChildren<Button>(true))
            {
                if (b != confirmButton && b != cancelButton && b.name != "DimBackground")
                    validBtns.Add(b);
            }

            validBtns.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            if (minusButton == null && validBtns.Count > 0) minusButton = validBtns[0];
            if (plusButton == null && validBtns.Count > 1) plusButton = validBtns[1];
            if (maxButton == null && validBtns.Count > 2) maxButton = validBtns[2];
        }
    }

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
        if (maxButton == null && plusButton != null && plusButton.transform.parent != null)
        {
            Transform parent = plusButton.transform.parent;
            foreach (Transform child in parent)
            {
                if (child.gameObject != plusButton.gameObject && child.gameObject != minusButton?.gameObject &&
                   (child.name.Equals("MaxButton", StringComparison.OrdinalIgnoreCase) ||
                    child.name.Equals("Max", StringComparison.OrdinalIgnoreCase) ||
                    child.name.Contains("Max")))
                {
                    maxButton = child.GetComponent<Button>();
                    if (maxButton != null) break;
                }
            }

            if (maxButton == null)
            {
                maxButton = Instantiate(plusButton, parent);
                maxButton.name = "MaxButton";
            }
        }

        if (maxButton != null)
        {
            var csf = maxButton.GetComponent<ContentSizeFitter>();
            if (csf != null) UnityEngine.Object.Destroy(csf);

            var maxRect = maxButton.GetComponent<RectTransform>();
            if (maxRect != null)
            {
                maxRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
                maxRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 44f);
                maxRect.sizeDelta = new Vector2(100f, 44f);
                maxRect.localScale = Vector3.one;
            }

            // Force stretch all background images / frames inside Max button to full 100x44 size
            var childImages = maxButton.GetComponentsInChildren<Image>(true);
            foreach (var img in childImages)
            {
                if (img != null && img.rectTransform != null)
                {
                    img.rectTransform.anchorMin = Vector2.zero;
                    img.rectTransform.anchorMax = Vector2.one;
                    img.rectTransform.offsetMin = Vector2.zero;
                    img.rectTransform.offsetMax = Vector2.zero;
                    img.rectTransform.sizeDelta = Vector2.zero;
                }
            }

            TMP_Text maxTxt = maxButton.GetComponentInChildren<TMP_Text>(true);
            if (maxTxt == null)
            {
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(maxButton.transform, false);
                maxTxt = labelObject.GetComponent<TextMeshProUGUI>();
            }

            maxTxt.gameObject.SetActive(true);
            maxTxt.text = "Max";
            maxTxt.alignment = TextAlignmentOptions.Center;
            maxTxt.fontStyle = FontStyles.Bold;
            maxTxt.enableAutoSizing = false;
            maxTxt.fontSize = 22f;
            maxTxt.raycastTarget = false;

            RectTransform txtRect = maxTxt.GetComponent<RectTransform>();
            if (txtRect != null)
            {
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.offsetMin = Vector2.zero;
                txtRect.offsetMax = Vector2.zero;
            }
        }
    }

    private void OnValidate()
    {
        ArrangeQuantityGroup();
    }

    private void ArrangeQuantityGroup()
    {
        Transform groupTransform = plusButton != null ? plusButton.transform.parent : (minusButton != null ? minusButton.transform.parent : null);
        if (groupTransform == null) return;

        if (groupTransform is RectTransform groupRect)
        {
            groupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 48f);
        }

        HorizontalLayoutGroup hlg = groupTransform.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
        {
            hlg = groupTransform.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 10f;
        hlg.padding = new RectOffset(2, 2, 2, 2);

        if (minusButton != null) minusButton.transform.SetSiblingIndex(0);
        if (quantityText != null) quantityText.transform.SetSiblingIndex(1);
        if (plusButton != null) plusButton.transform.SetSiblingIndex(2);
        if (maxButton != null) maxButton.transform.SetSiblingIndex(3);

        EnsureLayoutElement(minusButton != null ? minusButton.gameObject : null, 44f, 44f);
        EnsureLayoutElement(quantityText != null ? quantityText.gameObject : null, 60f, 44f);
        EnsureLayoutElement(plusButton != null ? plusButton.gameObject : null, 44f, 44f);
        EnsureLayoutElement(maxButton != null ? maxButton.gameObject : null, 100f, 44f);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(groupTransform as RectTransform);
    }

    private static void EnsureLayoutElement(GameObject go, float targetWidth, float targetHeight)
    {
        if (go == null) return;

        var csf = go.GetComponent<ContentSizeFitter>();
        if (csf != null) UnityEngine.Object.Destroy(csf);

        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
            rect.sizeDelta = new Vector2(targetWidth, targetHeight);
            rect.localScale = Vector3.one;
        }

        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();

        le.minWidth = targetWidth;
        le.preferredWidth = targetWidth;
        le.flexibleWidth = 0f;

        le.minHeight = targetHeight;
        le.preferredHeight = targetHeight;
        le.flexibleHeight = 0f;
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
