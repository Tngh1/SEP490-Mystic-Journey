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

        if (shopNameText != null)
        {
            shopNameText.text = data.itemName ?? string.Empty;
            if (data.weeklyPurchaseLimit > 0)
            {
                int remaining = Mathf.Max(0, data.remainingWeeklyPurchases);
                shopNameText.text += $" ({remaining}/{data.weeklyPurchaseLimit})";
            }
        }

        string curr = data.currency;
        if (string.IsNullOrWhiteSpace(curr)) curr = "Gold"; // Mặc định là Gold

        bool isGem = curr.Equals("Gem", System.StringComparison.OrdinalIgnoreCase) || 
                     curr.Equals("Gems", System.StringComparison.OrdinalIgnoreCase);

        // Bật/tắt Gem hoặc Coin tuỳ theo server
        if (isGem && gemGroup != null)
        {
            // Hiện Gem, Ẩn Coin
            if (priceGroup != null) priceGroup.SetActive(false);
            gemGroup.SetActive(true);
            
            if (gemPriceText != null)
            {
                gemPriceText.richText = true;
                gemPriceText.text = FormatDisplayPrice(data);
            }
        }
        else
        {
            // Hiện Coin, Ẩn Gem
            if (priceGroup != null) priceGroup.SetActive(true);
            if (gemGroup != null) gemGroup.SetActive(false);
            
            if (priceText != null)
            {
                priceText.richText = true;
                priceText.text = FormatDisplayPrice(data);
            }
        }

        // Vẫn giữ logic update ảnh cũ nếu user dùng 1 cái Image xài chung
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
        
        if (shopNameText != null) shopNameText.text = string.Empty;
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
        return $"<s><color=#9CA3AF>{originalPrice}</color></s> <b><color=#FFD34D>{currentPrice}</color></b>";
    }

    private static string FormatPrice(decimal amount, string currency)
    {
        string formatted = amount.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
        
        // Nếu không có tên tiền tệ, mặc định thêm chữ $ phía trước cho đẹp giống Prefab
        if (string.IsNullOrWhiteSpace(currency))
        {
            return $"${formatted}";
        }
        
        // Nếu tiền tệ là Gold, Gems... thì có thể để phía sau hoặc tuỳ biến
        return $"${formatted}"; // Luôn có dấu $ như ý user muốn
    }
}
