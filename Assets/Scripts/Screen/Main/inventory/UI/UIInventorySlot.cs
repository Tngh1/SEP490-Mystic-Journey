using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIInventorySlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private GameObject highlight;

    private InventoryItemData currentData;

    public void SetData(InventoryItemData data)
    {
        Debug.Log("SET DATA");

        currentData = data;

        if (data == null)
        {
            Debug.Log("DATA NULL");

            Clear();
            return;
        }

        Debug.Log("ITEM ID: " + data.itemId);

        icon.enabled = true;

        icon.sprite =
            ItemIconDatabase.Instance.GetIcon(data.itemId);

        Debug.Log("SPRITE SET");

        quantityText.text =
            data.quantity > 1
            ? data.quantity.ToString()
            : "";

        highlight.SetActive(false);
    }

    public void Clear()
    {
        currentData = null;

        icon.sprite = null;
        icon.enabled = false;

        quantityText.text = "";

        highlight.SetActive(false);
    }
}