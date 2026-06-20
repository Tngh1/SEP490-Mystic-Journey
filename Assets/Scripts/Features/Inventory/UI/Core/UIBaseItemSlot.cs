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

        // T?i ?u sinh chu?i, kh�ng hi?n s? n?u ch? c� 1 m�n
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
        // Rarity border color logic is commented out pending implementation.
    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        OnSlotClicked?.Invoke(this);
    }
}