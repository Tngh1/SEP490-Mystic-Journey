using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIShopSlot : UIBaseItemSlot
{
    [Header("Shop Specifics")]
    [SerializeField] private TMP_Text nameText;       // H?ng c?c NameText
    [SerializeField] private GameObject priceGroup;   // H?ng c?c Coin (c?c cha)
    [SerializeField] private TMP_Text priceText;      // H?ng c?c CoinText
    [SerializeField] private Image currencyIconImage; // (Tùy ch?n: N?u có icon Vàng/Gem c?nh text)
    [SerializeField] private Button buyButton;        // H?ng c?c BuyButton

    private void Awake()
    {
        // G?n s? ki?n: Khi nút Mua b? b?m, nó s? hét lên báo cho Manager bi?t
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(OnBuyButtonClicked);
        }
    }

    public void SetupShop(UIItemDisplayData data)
    {
        if (data == null)
        {
            ClearSlot();
            return;
        }

        // G?i Lõi ?? v? Icon, Khung, S? l??ng
        base.SetupCore(data);

        // 1. V? Tên V?t Ph?m
        if (nameText != null)
        {
            nameText.text = data.itemName;
        }

        // 2. V? Giá Ti?n
        if (priceGroup != null)
        {
            priceGroup.SetActive(true);
            if (priceText != null) priceText.text = data.price.ToString();

            if (data.currencyIcon != null && currencyIconImage != null)
            {
                currencyIconImage.sprite = data.currencyIcon;
            }
        }
    }

    public override void ClearSlot()
    {
        base.ClearSlot();
        if (priceGroup != null) priceGroup.SetActive(false);
        if (nameText != null) nameText.text = string.Empty;
    }

    // X? lý khi nút "Mua" ???c b?m
    private void OnBuyButtonClicked()
    {
        // Dùng chung lu?ng Click c?a class Cha ?? truy?n Data sang ShopManager
        OnSlotClicked?.Invoke(this);
    }
}