using System;
using System.Collections.Generic;
using System.Globalization;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;

public class UIShop : MonoBehaviour
{
    public static UIShop Instance;

    [Header("Shop UI Settings")]
    [Tooltip("Prefab for Shop Slot")]
    [SerializeField] private UIShopSlot shopSlotPrefab;

    [Tooltip("Content Transform containing Grid Layout Group")]
    [SerializeField] private Transform contentParent;

    [Header("Categories & Confirm Popup")]
    public UITabGroup categoryTabGroup;
    public UIConfirmPurchase confirmPurchasePanel;

    [Header("API")]
    [SerializeField] private bool loadFromApiOnEnable = true;
    [SerializeField] private int pageSize = 50;
    [SerializeField] private bool includeSoldOut;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private TMP_Text statusText;

    private readonly List<UIShopSlot> slots = new List<UIShopSlot>();
    private List<UIItemDisplayData> allCurrentItems = new List<UIItemDisplayData>();
    private readonly string[] categoryMapping = { "All", "Weapon", "Armor", "Consumable", "Material", "Gacha" };

    private string currentCategory = "All";
    private bool requestInFlight;
    private bool purchaseInFlight;
    private bool eventsBound;

    public Action<UIBaseItemSlot> OnShopItemClicked;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(gameObject);
    }

    private void OnEnable()
    {
        BindEvents();

        if (loadFromApiOnEnable)
            LoadShop();
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
    }

    private void BindEvents()
    {
        if (eventsBound)
            return;

        if (categoryTabGroup != null)
            categoryTabGroup.onTabSelected.AddListener(OnCategoryTabSelected);

        if (confirmPurchasePanel != null)
            confirmPurchasePanel.OnConfirmPurchase += HandlePurchaseConfirmed;

        eventsBound = true;
    }

    public void LoadShop(bool force = false)
    {
        if (requestInFlight)
            return;

        requestInFlight = true;
        SetLoading(true);
        SetStatus(null);
        LoadShopPage(1, new List<UIItemDisplayData>());
    }

    public void RefreshShop(List<UIItemDisplayData> shopItems)
    {
        allCurrentItems = shopItems ?? new List<UIItemDisplayData>();
        FilterAndDisplay(currentCategory);
    }

    private void LoadShopPage(int page, List<UIItemDisplayData> aggregate)
    {
        ShopApi.Instance.GetPlayerShopItems(
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
                    LoadShopPage(page + 1, aggregate);
                    return;
                }

                requestInFlight = false;
                SetLoading(false);
                RefreshShop(aggregate);
            },
            onError: error =>
            {
                requestInFlight = false;
                SetLoading(false);
                SetStatus($"Cannot load shop: {error.Message}");
                Debug.LogError($"[UIShop] LoadShop FAIL: {error.Message}");
            },
            includeSoldOut: includeSoldOut);
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
            price = ToLegacyPrice(unitPrice),
            unitPrice = unitPrice,
            currency = NormalizeCurrency(item?.Currency),
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
            rawData = item
        };
    }

    private void OnCategoryTabSelected(int index)
    {
        currentCategory = index >= 0 && index < categoryMapping.Length ? categoryMapping[index] : "All";
        FilterAndDisplay(currentCategory);
    }

    private void FilterAndDisplay(string category)
    {
        currentCategory = string.IsNullOrWhiteSpace(category) ? "All" : category;
        List<UIItemDisplayData> filteredList = new List<UIItemDisplayData>();

        if (currentCategory == "All")
        {
            filteredList.AddRange(allCurrentItems);
        }
        else
        {
            foreach (var item in allCurrentItems)
            {
                if (!string.IsNullOrEmpty(item.category) && item.category.Equals(currentCategory, StringComparison.OrdinalIgnoreCase))
                    filteredList.Add(item);
            }
        }

        for (int i = 0; i < filteredList.Count; i++)
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
            slots[i].SetupShop(filteredList[i]);
        }

        for (int i = filteredList.Count; i < slots.Count; i++)
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
        if (itemData == null || itemData.shopItemId <= 0 || quantity <= 0 || purchaseInFlight)
            return;

        purchaseInFlight = true;
        SetLoading(true);
        SetStatus(null);

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
                LoadShop(force: true);
            },
            onError: error =>
            {
                purchaseInFlight = false;
                SetLoading(false);
                SetStatus($"Purchase failed: {error.Message}");
                Debug.LogError($"[UIShop] Purchase FAIL: {error.Message}");
            });
    }

    private Sprite ResolveIcon(string itemName, string itemType)
    {
        return ItemIconDatabase.Instance != null ? ItemIconDatabase.Instance.GetIcon(itemName, itemType) : null;
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
