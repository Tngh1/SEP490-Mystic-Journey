using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class UIShopSlot : UIBaseItemSlot
{
    [Header("Shop Specifics")]
    [FormerlySerializedAs("nameText")]
    [SerializeField] private TMP_Text shopNameText;       // H?ng c?c NameText
    [SerializeField] private GameObject priceGroup;   // H?ng c?c Coin (c?c cha)
    [SerializeField] private TMP_Text priceText;      // H?ng c?c CoinText
    [SerializeField] private Image currencyIconImage; // (T�y ch?n: N?u c� icon V�ng/Gem c?nh text)
    [SerializeField] private Button buyButton;        // H?ng c?c BuyButton

    private void Awake()
    {
        // G?n s? ki?n: Khi n�t Mua b? b?m, n� s? h�t l�n b�o cho Manager bi?t
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

        // G?i L�i ?? v? Icon, Khung, S? l??ng
        base.SetupCore(data);

        // 1. V? T�n V?t Ph?m
        if (shopNameText != null)
        {
            shopNameText.text = data.itemName;
        }

        // 2. V? Gi� Ti?n
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
        if (shopNameText != null) shopNameText.text = string.Empty;
    }

    // X? l� khi n�t "Mua" ???c b?m
    private void OnBuyButtonClicked()
    {
        // D�ng chung lu?ng Click c?a class Cha ?? truy?n Data sang ShopManager
        OnSlotClicked?.Invoke(this);
    }
}