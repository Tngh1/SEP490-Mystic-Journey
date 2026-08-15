using System;
using System.Collections.Generic;
using System.Globalization;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIShop : MonoBehaviour
{
    public static UIShop Instance;

    private const string DailyDealsCategory = "Daily Deals";
    private const string DailyDealCategoryAlias = "DailyDeal";
    private const string DailyCategoryAlias = "Daily";
    private const string TodayCategoryAlias = "Today";
    private const string AllCategory = "All";
    private const string SkinCategory = "Skin";

    [Header("Shop UI Settings")]
    [Tooltip("Prefab for Shop Slot")]
    [SerializeField] private UIShopSlot shopSlotPrefab;

    [Tooltip("Content Transform containing Grid Layout Group")]
    [SerializeField] private Transform contentParent;

    [Header("Categories & Confirm Popup")]
    public UITabGroup categoryTabGroup;
    public UIConfirmPurchase confirmPurchasePanel;
    [SerializeField] private Image flagTagImage;
    [SerializeField] private Sprite[] categoryFlags;

    [Header("Daily Refresh")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private TMP_Text refreshCountText;
    [SerializeField] private GameObject refreshUIContainer;

    [Header("API")]
    [SerializeField] private bool loadFromApiOnEnable = true;
    [SerializeField] private int pageSize = 50;
    [SerializeField] private bool includeSoldOut;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private TMP_Text statusText;

    [Header("Category Mapping")]
    [SerializeField] private string[] categoryMapping = { DailyDealsCategory, AllCategory, "Weapon", "Armor", "Consumable", "Material", "Gacha", SkinCategory };

    private readonly List<UIShopSlot> slots = new List<UIShopSlot>();
    private List<UIItemDisplayData> allCurrentItems = new List<UIItemDisplayData>();

    private string currentCategory = DailyDealsCategory;
    private bool requestInFlight;
    private bool purchaseInFlight;
    private bool eventsBound;
    private ShopRefreshStatusResponse currentRefreshStatus;

    public Action<UIBaseItemSlot> OnShopItemClicked;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(gameObject);

        EnsureSkinCategoryTab();
            
        // Force Grid Layout Group to Fixed Column Count = 3
        if (contentParent != null)
        {
            var grid = contentParent.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
            }
        }
    }

    private void OnEnable()
    {
        BindEvents();
        UpdateRefreshVisibility();

        if (loadFromApiOnEnable)
            LoadShop();
        else
            LoadRefreshStatus();
    }

    private void Start()
    {
        BindEvents();
    }

    private void OnDestroy()
    {
        if (categoryTabGroup != null)
            categoryTabGroup.onTabSelected.RemoveListener(OnCategoryTabSelected);

        if (confirmPurchasePanel != null)
            confirmPurchasePanel.OnConfirmPurchase -= HandlePurchaseConfirmed;

        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(HandleRefreshClicked);
    }

    private void BindEvents()
    {
        if (eventsBound)
            return;

        TryAutoBindRefreshControls();

        if (categoryTabGroup != null)
            categoryTabGroup.onTabSelected.AddListener(OnCategoryTabSelected);

        if (confirmPurchasePanel != null)
            confirmPurchasePanel.OnConfirmPurchase += HandlePurchaseConfirmed;

        if (refreshButton != null)
            refreshButton.onClick.AddListener(HandleRefreshClicked);

        eventsBound = true;
        UpdateRefreshButton();
    }

    public void LoadShop(bool force = false)
    {
        LoadCurrentCategory();
    }

    public void RefreshShop(List<UIItemDisplayData> shopItems)
    {
        allCurrentItems = shopItems ?? new List<UIItemDisplayData>();
        DisplayItems(allCurrentItems);
        UpdateRefreshVisibility();
    }

    public void RefreshDailyShop()
    {
        if (!IsDailyDealsCategory(currentCategory))
            return;

        if (requestInFlight || purchaseInFlight)
            return;

        if (currentRefreshStatus != null && !currentRefreshStatus.CanRefresh)
        {
            SetStatus("No daily deal refreshes left today.");
            UpdateRefreshButton();
            return;
        }

        requestInFlight = true;
        SetLoading(true);
        SetStatus(null);
        UpdateRefreshButton();

        ShopApi.Instance.RefreshPlayerShop(
            page: 1,
            pageSize: 10,
            onSuccess: response =>
            {
                requestInFlight = false;
                SetLoading(false);
                ApplyRefreshStatus(response?.RefreshStatus);
                RefreshShop(MapShopItems(response?.Shop?.Items));
                SetStatus(response?.Message);
                UpdateRefreshButton();
            },
            onError: error =>
            {
                requestInFlight = false;
                SetLoading(false);
                SetStatus($"Refresh failed: {error.Message}");
                Debug.LogError($"[UIShop] Refresh FAIL: {error.Message}");
                LoadRefreshStatus();
                UpdateRefreshButton();
            },
            includeSoldOut: includeSoldOut);
    }

    private void LoadCurrentCategory()
    {
        if (requestInFlight)
            return;

        requestInFlight = true;
        SetLoading(true);
        SetStatus(null);
        UpdateRefreshVisibility();
        UpdateRefreshButton();

        if (IsDailyDealsCategory(currentCategory))
        {
            LoadDailyDeals();
            return;
        }

        if (IsSkinCategory(currentCategory))
        {
            LoadSkins();
            return;
        }

        string itemType = IsAllCategory(currentCategory) ? null : currentCategory;
        LoadFixedShopPage(1, new List<UIItemDisplayData>(), itemType);
    }

    private void LoadSkins()
    {
        ShopApi.Instance.GetSkins(
            onSuccess: response =>
            {
                requestInFlight = false;
                SetLoading(false);
                var items = new List<UIItemDisplayData>();
                response = response ?? Array.Empty<SkinShopItemResponse>();
                for (int i = 0; i < response.Length; i++)
                    items.Add(MapSkinShopItem(response[i]));
                RefreshShop(items);
                UpdateRefreshButton();
            },
            onError: error =>
            {
                requestInFlight = false;
                SetLoading(false);
                SetStatus($"Cannot load skin shop: {error.Message}");
                Debug.LogError($"[UIShop] LoadSkins FAIL: {error.Message}");
                UpdateRefreshButton();
            });
    }

    private void LoadDailyDeals()
    {
        ShopApi.Instance.GetDailyDeals(
            onSuccess: response =>
            {
                requestInFlight = false;
                SetLoading(false);
                RefreshShop(MapShopItems(response?.Items));
                LoadRefreshStatus();
                UpdateRefreshButton();
            },
            onError: error =>
            {
                requestInFlight = false;
                SetLoading(false);
                SetStatus($"Cannot load daily deals: {error.Message}");
                Debug.LogError($"[UIShop] LoadDailyDeals FAIL: {error.Message}");
                UpdateRefreshButton();
            },
            includeSoldOut: includeSoldOut);
    }

    private void LoadFixedShopPage(int page, List<UIItemDisplayData> aggregate, string itemType)
    {
        ShopApi.Instance.GetFixedShopItems(
            page: page,
            pageSize: Mathf.Max(1, pageSize),
            onSuccess: response =>
            {
                var responseItems = response?.Items ?? Array.Empty<ShopItemPublicResponse>();
                for (int i = 0; i < responseItems.Length; i++)
                    aggregate.Add(MapShopItem(responseItems[i]));

                int totalCount = response?.TotalCount ?? aggregate.Count;
                bool hasNextPage = responseItems.Length > 0 && aggregate.Count < totalCount;
                if (hasNextPage)
                {
                    LoadFixedShopPage(page + 1, aggregate, itemType);
                    return;
                }

                requestInFlight = false;
                SetLoading(false);
                RefreshShop(aggregate);
                UpdateRefreshButton();
            },
            onError: error =>
            {
                requestInFlight = false;
                SetLoading(false);
                SetStatus($"Cannot load shop: {error.Message}");
                Debug.LogError($"[UIShop] LoadFixedShop FAIL: {error.Message}");
                UpdateRefreshButton();
            },
            itemType: itemType,
            includeSoldOut: includeSoldOut);
    }

    private void LoadRefreshStatus()
    {
        ShopApi.Instance.GetPlayerShopRefreshStatus(
            onSuccess: status =>
            {
                ApplyRefreshStatus(status);
                UpdateRefreshButton();
            },
            onError: error =>
            {
                Debug.LogWarning($"[UIShop] Load refresh status FAIL: {error.Message}");
                UpdateRefreshButton();
            });
    }

    private List<UIItemDisplayData> MapShopItems(ShopItemPublicResponse[] responseItems)
    {
        var items = new List<UIItemDisplayData>();
        if (responseItems == null)
            responseItems = Array.Empty<ShopItemPublicResponse>();

        for (int i = 0; i < responseItems.Length; i++)
            items.Add(MapShopItem(responseItems[i]));

        return items;
    }

    private UIItemDisplayData MapShopItem(ShopItemPublicResponse item)
    {
        decimal unitPrice = item?.Price ?? 0;
        string itemName = item?.ItemName ?? string.Empty;
        string itemType = item?.ItemType ?? "Other";

        return new UIItemDisplayData
        {
            shopItemId = item?.ShopItemId ?? 0,
            itemId = item?.ItemId ?? 0,
            itemName = itemName,
            icon = ResolveIcon(itemName, itemType),
            quantity = 0,
            rarity = item?.Rarity,
            category = string.IsNullOrWhiteSpace(itemType) ? "Other" : itemType,
            shopSection = item?.ShopSection,
            price = ToLegacyPrice(unitPrice),
            unitPrice = unitPrice,
            originalUnitPrice = item?.OriginalPrice ?? 0,
            currency = NormalizeCurrency(item?.Currency),
            currencyIcon = ResolveCurrencyIcon(item?.Currency),
            stock = item?.Stock ?? -1,
            isUnlimitedStock = item?.IsUnlimitedStock ?? true,
            dailyPurchaseLimit = item?.DailyPurchaseLimit ?? 0,
            purchasedToday = item?.PurchasedToday ?? 0,
            remainingDailyPurchases = item?.RemainingDailyPurchases ?? -1,
            canPurchase = item?.CanPurchase ?? false,
            unavailableReason = item?.UnavailableReason,
            weeklyPurchaseLimit = item?.WeeklyPurchaseLimit ?? 0,
            purchasedThisWeek = item?.PurchasedThisWeek ?? 0,
            remainingWeeklyPurchases = item?.RemainingWeeklyPurchases ?? -1,
            baseHp = item?.BaseHp ?? 0,
            baseAtk = item?.BaseAtk ?? 0,
            baseDef = item?.BaseDef ?? 0,
            bonusHp = item?.BonusHp ?? 0,
            bonusAtk = item?.BonusAtk ?? 0,
            bonusDef = item?.BonusDef ?? 0,
            bonusCritRate = item?.BonusCritRate ?? 0f,
            bonusCritDamage = item?.BonusCritDamage ?? 0f,
            description = item?.Description,
            slot = item?.Slot,
            rawData = item
        };
    }

    private UIItemDisplayData MapSkinShopItem(SkinShopItemResponse skin)
    {
        decimal price = skin?.Price ?? 0;
        return new UIItemDisplayData
        {
            skinId = skin?.SkinId ?? 0,
            isSkin = true,
            itemName = skin?.SkinName ?? string.Empty,
            icon = ResolveSkinIcon(skin?.SkinId ?? 0),
            quantity = 1,
            rarity = skin?.Rarity,
            category = SkinCategory,
            shopSection = SkinCategory,
            price = ToLegacyPrice(price),
            unitPrice = price,
            currency = NormalizeCurrency(skin?.Currency),
            currencyIcon = ResolveCurrencyIcon(skin?.Currency),
            stock = skin != null && skin.IsOwned ? 0 : 1,
            isUnlimitedStock = false,
            canPurchase = skin?.CanPurchase ?? false,
            unavailableReason = skin?.UnavailableReason,
            rawData = skin
        };
    }

    private void OnCategoryTabSelected(int index)
    {
        currentCategory = index >= 0 && index < categoryMapping.Length
            ? NormalizeCategory(categoryMapping[index])
            : AllCategory;

        UpdateFlagTag(index);

        LoadCurrentCategory();
    }

    private void UpdateFlagTag(int index)
    {
        if (flagTagImage == null)
        {
            var t = transform.Find("LeftPanel/FlagTag") ?? transform.Find("FlagTag");
            if (t == null)
            {
                var children = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i] != null && children[i].name.Equals("FlagTag", StringComparison.OrdinalIgnoreCase))
                    {
                        flagTagImage = children[i];
                        break;
                    }
                }
            }
            else
            {
                flagTagImage = t.GetComponent<Image>();
            }
        }

        if (flagTagImage == null)
            return;

        if (IsSkinCategory(currentCategory))
        {
            Sprite skinIcon = ResolveSkinIcon(GetPremiumSkinIdForCurrentClass());
            if (skinIcon != null)
            {
                flagTagImage.sprite = skinIcon;
                flagTagImage.enabled = true;
            }
            return;
        }

        if (categoryFlags != null && index >= 0 && index < categoryFlags.Length)
        {
            if (categoryFlags[index] != null)
            {
                flagTagImage.sprite = categoryFlags[index];
                flagTagImage.enabled = true;
            }
        }
    }

    private void DisplayItems(List<UIItemDisplayData> items)
    {
        var displayItems = items ?? new List<UIItemDisplayData>();

        for (int i = 0; i < displayItems.Count; i++)
        {
            if (i >= slots.Count)
            {
                if (shopSlotPrefab == null || contentParent == null)
                {
                    Debug.LogError("[UIShop] Missing shopSlotPrefab or contentParent.");
                    return;
                }

                UIShopSlot newSlot = Instantiate(shopSlotPrefab, contentParent);
                newSlot.OnSlotClicked += HandleSlotClicked;
                newSlot.transform.localScale = Vector3.one;
                slots.Add(newSlot);
            }

            slots[i].gameObject.SetActive(true);
            slots[i].SetupShop(displayItems[i]);
        }

        for (int i = displayItems.Count; i < slots.Count; i++)
        {
            slots[i].ClearSlot();
            slots[i].gameObject.SetActive(false);
        }
    }

    private void HandleSlotClicked(UIBaseItemSlot clickedSlot)
    {
        if (purchaseInFlight)
            return;

        UIItemDisplayData data = clickedSlot?.RawData as UIItemDisplayData;
        if (data == null)
            return;

        if (!data.canPurchase || data.GetMaxPurchaseQuantity() <= 0)
        {
            SetStatus(string.IsNullOrWhiteSpace(data.unavailableReason) ? "This item cannot be purchased." : data.unavailableReason);
            return;
        }

        Debug.Log($"[UIShop] Item clicked: {data.itemName} price {FormatAmount(data.EffectiveUnitPrice)} {data.currency}");

        if (confirmPurchasePanel != null)
        {
            confirmPurchasePanel.gameObject.SetActive(true);
            confirmPurchasePanel.Setup(data);
        }
        else
        {
            HandlePurchaseConfirmed(data, 1);
        }
    }

    private void HandlePurchaseConfirmed(UIItemDisplayData itemData, int quantity)
    {
        if (itemData == null || quantity <= 0 || purchaseInFlight)
            return;

        if ((itemData.isSkin && itemData.skinId <= 0) || (!itemData.isSkin && itemData.shopItemId <= 0))
            return;

        purchaseInFlight = true;
        SetLoading(true);
        SetStatus(null);
        UpdateRefreshButton();

        if (itemData.isSkin)
        {
            ShopApi.Instance.PurchaseSkin(
                itemData.skinId,
                response =>
                {
                    purchaseInFlight = false;
                    SetLoading(false);
                    SetStatus(response?.Message);
                    if (response?.Balance != null)
                        PlayerHUDController.Instance?.ApplyCurrencyBalance(response.Balance);
                    else
                        PlayerHUDController.Instance?.RefreshCurrencyBalance();
                    InventoryManager.RefreshAny(refreshStats: false);
                    LoadCurrentCategory();
                    UpdateRefreshButton();
                },
                error =>
                {
                    purchaseInFlight = false;
                    SetLoading(false);
                    SetStatus($"Purchase failed: {error.Message}");
                    UIPopupBox.Notify(transform, "Purchase Failed", error.Message);
                    UpdateRefreshButton();
                });
            return;
        }

        ShopApi.Instance.PurchaseItem(
            shopItemId: itemData.shopItemId,
            quantity: quantity,
            onSuccess: response =>
            {
                purchaseInFlight = false;
                SetLoading(false);
                SetStatus(response?.Message);

                if (response?.Balance != null)
                    PlayerHUDController.Instance?.ApplyCurrencyBalance(response.Balance);
                else
                    PlayerHUDController.Instance?.RefreshCurrencyBalance();

                InventoryManager.RefreshAny(refreshStats: false);
                LoadCurrentCategory();
                UpdateRefreshButton();
            },
            onError: error =>
            {
                purchaseInFlight = false;
                SetLoading(false);
                SetStatus($"Purchase failed: {error.Message}");
                Debug.LogError($"[UIShop] Purchase FAIL: {error.Message}");

                // statusText chưa được gán trong Main.unity nên SetStatus chỉ ghi console — thiếu tiền
                // trước giờ hoàn toàn im lặng với người chơi. BE ném BadRequestException cho MỌI lỗi mua
                // (thiếu tiền, hết hàng, quá giới hạn) nên errorCode đều là BAD_REQUEST, không phân biệt
                // được; chỉ message là khác nhau ("Not enough Gold." / "Sold out." / ...). Vì vậy đưa
                // thẳng message của BE vào popup thay vì đoán loại lỗi bằng cách so khớp chuỗi.
                // ponytail: message tiếng Anh lấy nguyên từ BE; muốn đa ngôn ngữ thì BE phải trả
                // errorCode riêng cho từng trường hợp rồi client map sang chuỗi dịch.
                UIPopupBox.Notify(transform, "Purchase Failed", error.Message);

                UpdateRefreshButton();
            });
    }

    private void HandleRefreshClicked()
    {
        RefreshDailyShop();
    }

    private void ApplyRefreshStatus(ShopRefreshStatusResponse status)
    {
        currentRefreshStatus = status;

        if (refreshCountText == null)
            return;

        if (status == null)
        {
            refreshCountText.text = string.Empty;
            return;
        }

        refreshCountText.text = $"Refresh: {Mathf.Max(0, status.RefreshesRemainingToday)}/{Mathf.Max(0, status.MaxDailyRefreshes)}";
    }

    private void UpdateRefreshButton()
    {
        if (refreshButton == null)
            return;

        bool canRefresh = IsDailyDealsCategory(currentCategory) &&
                          (currentRefreshStatus == null || currentRefreshStatus.CanRefresh);
        refreshButton.interactable = canRefresh && !requestInFlight && !purchaseInFlight;
    }

    private void UpdateRefreshVisibility()
    {
        bool isDailyDeals = IsDailyDealsCategory(currentCategory);

        if (refreshUIContainer != null)
        {
            refreshUIContainer.SetActive(isDailyDeals);
            return;
        }

        if (refreshButton != null)
            refreshButton.gameObject.SetActive(isDailyDeals);

        if (refreshCountText != null)
            refreshCountText.gameObject.SetActive(isDailyDeals);
    }

    private Transform GetAutoBindRoot()
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name.EndsWith("Panel", StringComparison.OrdinalIgnoreCase))
                return current;

            current = current.parent;
        }

        return transform;
    }

    private void TryAutoBindRefreshControls()
    {
        Transform root = GetAutoBindRoot();

        if (refreshButton == null)
        {
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name.Equals("RefreshButton", StringComparison.OrdinalIgnoreCase))
                {
                    refreshButton = buttons[i];
                    break;
                }
            }
        }

        if (refreshCountText == null)
        {
            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null &&
                    (texts[i].name.Equals("RefreshCountText", StringComparison.OrdinalIgnoreCase) ||
                     texts[i].name.Equals("Freshcount", StringComparison.OrdinalIgnoreCase)))
                {
                    refreshCountText = texts[i];
                    break;
                }
            }
        }
    }

    private Sprite ResolveIcon(string itemName, string itemType)
    {
        return ItemIconDatabase.Instance != null ? ItemIconDatabase.Instance.GetIcon(itemName, itemType) : null;
    }

    private Sprite ResolveCurrencyIcon(string currency)
    {
        if (ItemIconDatabase.Instance == null) return null;
        string normalized = NormalizeCurrency(currency);
        string key = normalized.Equals("Gems", StringComparison.OrdinalIgnoreCase) ? "Gem" : "Gold";
        return ItemIconDatabase.Instance.GetIcon(key, "Currency");
    }

    private static Sprite ResolveSkinIcon(int skinId)
    {
        var database = SkinDatabaseSO.LoadDefault();
        return database != null ? database.GetPreviewSprite(skinId) : null;
    }

    private void EnsureSkinCategoryTab()
    {
        bool mappingHasSkin = false;
        for (int i = 0; i < categoryMapping.Length; i++)
            mappingHasSkin |= IsSkinCategory(categoryMapping[i]);

        if (!mappingHasSkin)
        {
            Array.Resize(ref categoryMapping, categoryMapping.Length + 1);
            categoryMapping[categoryMapping.Length - 1] = SkinCategory;
        }

        if (categoryTabGroup == null) return;
        for (int i = 0; i < categoryTabGroup.tabButtons.Count; i++)
        {
            Button existing = categoryTabGroup.tabButtons[i];
            if (existing != null && existing.name.Equals("Tab_Skin", StringComparison.OrdinalIgnoreCase))
                return;
        }

        if (categoryTabGroup.tabButtons.Count == 0) return;
        Button template = categoryTabGroup.tabButtons[categoryTabGroup.tabButtons.Count - 1];
        if (template == null) return;

        Button skinTab = Instantiate(template, template.transform.parent);
        skinTab.name = "Tab_Skin";
        skinTab.transform.SetAsLastSibling();
        skinTab.onClick.RemoveAllListeners();
        Sprite icon = ResolveSkinIcon(GetPremiumSkinIdForCurrentClass());
        Image image = skinTab.GetComponent<Image>();
        if (image != null && icon != null) image.sprite = icon;
        categoryTabGroup.tabButtons.Add(skinTab);
    }

    private static int GetPremiumSkinIdForCurrentClass()
    {
        string playerClass = MysticJourney.Core.Services.GameStateService.Instance?.PlayerClass;
        if (string.Equals(playerClass, "Knight", StringComparison.OrdinalIgnoreCase)) return 5;
        if (string.Equals(playerClass, "Mage", StringComparison.OrdinalIgnoreCase)) return 6;
        return 4;
    }

    private void SetLoading(bool isLoading)
    {
        if (loadingIndicator != null)
            loadingIndicator.SetActive(isLoading);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        if (!string.IsNullOrEmpty(message))
            Debug.Log($"[UIShop] {message}");
    }

    private static string NormalizeCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return AllCategory;

        if (category.Equals(TodayCategoryAlias, StringComparison.OrdinalIgnoreCase) ||
            category.Equals(DailyCategoryAlias, StringComparison.OrdinalIgnoreCase) ||
            category.Equals(DailyDealCategoryAlias, StringComparison.OrdinalIgnoreCase) ||
            category.Equals(DailyDealsCategory, StringComparison.OrdinalIgnoreCase))
            return DailyDealsCategory;

        return category.Trim();
    }

    private static bool IsDailyDealsCategory(string category)
        => NormalizeCategory(category).Equals(DailyDealsCategory, StringComparison.OrdinalIgnoreCase);

    private static bool IsAllCategory(string category)
        => string.IsNullOrWhiteSpace(category) || category.Equals(AllCategory, StringComparison.OrdinalIgnoreCase);

    private static bool IsSkinCategory(string category)
        => !string.IsNullOrWhiteSpace(category) && category.Equals(SkinCategory, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCurrency(string currency)
    {
        return string.IsNullOrWhiteSpace(currency) ? "Gold" : currency.Trim();
    }

    private static int ToLegacyPrice(decimal price)
    {
        if (price <= 0) return 0;
        if (price >= int.MaxValue) return int.MaxValue;
        return decimal.ToInt32(decimal.Round(price, 0, MidpointRounding.AwayFromZero));
    }

    private static string FormatAmount(decimal amount)
    {
        return amount.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
    }
}
