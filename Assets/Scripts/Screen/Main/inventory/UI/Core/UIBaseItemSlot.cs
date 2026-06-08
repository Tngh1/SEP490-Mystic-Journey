using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public abstract class UIBaseItemSlot : MonoBehaviour, IPointerClickHandler
{
    [Header("Core UI Elements")]
    [SerializeField] protected Image iconImage;
    //[SerializeField] protected TMP_Text quantityText;
    //[SerializeField] protected Image rarityBorder;
    //[SerializeField] protected GameObject selectHighlight;

    public Action<UIBaseItemSlot> OnSlotClicked;
    public object RawData { get; protected set; }

    public virtual void SetupCore(UIItemDisplayData data)
    {
        RawData = data.rawData;

        if (data.icon == null)
        {
            ClearSlot();
            return;
        }

        iconImage.enabled = true;
        iconImage.sprite = data.icon;

        // T?i ?u sinh chu?i, không hi?n s? n?u ch? có 1 món
      //  quantityText.text = data.quantity > 1 ? data.quantity.ToString() : string.Empty;

        //SetHighlight(false);
        //SetRarityColor(data.rarity);
    }

    public virtual void ClearSlot()
    {
        RawData = null;
        iconImage.enabled = false;
      //  quantityText.text = string.Empty;
     //   SetHighlight(false);
    }

    public void SetHighlight(bool isActive)
    {
       // if (selectHighlight != null) selectHighlight.SetActive(isActive);
    }

    protected virtual void SetRarityColor(string rarity)
    {
        // Logic ??i màu vi?n theo ph?m ch?t. Mira thêm mã màu t??ng ?ng vào ?ây nhé!
       // if (rarityBorder == null || string.IsNullOrEmpty(rarity)) return;

        switch (rarity.ToLower())
        {
            //case "legendary": rarityBorder.color = Color.yellow; break;
            //case "epic": rarityBorder.color = Color.magenta; break;
            //case "rare": rarityBorder.color = Color.cyan; break;
            //default: rarityBorder.color = Color.white; break;
        }
    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        OnSlotClicked?.Invoke(this);
    }
}