using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIShopSlot : UIBaseItemSlot
{
    [Header("Shop Specifics")]
    [SerializeField] private GameObject priceGroup;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Image currencyIconImage;

    public void SetupShop(UIItemDisplayData data)
    {
        if (data == null)
        {
            ClearSlot();
            return;
        }

        base.SetupCore(data);

        // Hi?n th? giá ti?n và lo?i ti?n (Vàng, Gem...)
        if (priceGroup != null)
        {
            priceGroup.SetActive(true);
            priceText.text = data.price.ToString();
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
    }
}