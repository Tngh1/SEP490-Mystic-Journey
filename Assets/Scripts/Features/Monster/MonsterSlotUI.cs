using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models.Response;
using System;

public class MonsterSlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [Tooltip("Optional: new slot UI no longer has a name label")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button slotButton;
    [Tooltip("Optional: highlight border when slot is selected")]
    [SerializeField] private GameObject activeDeco;

    private PlayerMonsterCatalogItem _monsterData;

    // Thêm tham số Sprite iconSprite vào hàm Init
    public void Init(PlayerMonsterCatalogItem data, Action<PlayerMonsterCatalogItem> onClickAction, Sprite iconSprite)
    {
        _monsterData = data;

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => onClickAction?.Invoke(_monsterData));

            // Cùng hiệu ứng rê chuột mà HUD đang dùng
            if (slotButton.GetComponent<UIHoverScaleEffect>() == null)
                slotButton.gameObject.AddComponent<UIHoverScaleEffect>();
        }

        // nameText có thể null: slot mới chỉ hiện icon, không có nhãn tên
        if (nameText != null)
            nameText.text = _monsterData.IsDiscovered ? _monsterData.Name : "???";

        if (iconImage != null)
        {
            // Chưa gặp thì hiện dưới dạng bóng đen
            iconImage.color = _monsterData.IsDiscovered ? Color.white : Color.black;
            if (iconSprite != null) iconImage.sprite = iconSprite;
        }

        SetSelected(false);
    }

    /// <summary>Bật/tắt viền sáng cho slot đang được chọn.</summary>
    public void SetSelected(bool selected)
    {
        if (activeDeco != null) activeDeco.SetActive(selected);
    }
}