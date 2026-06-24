using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models.Response;
using System;

public class MonsterSlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button slotButton;

    private PlayerMonsterCatalogItem _monsterData;

    // Thêm tham số Sprite iconSprite vào hàm Init
    public void Init(PlayerMonsterCatalogItem data, Action<PlayerMonsterCatalogItem> onClickAction, Sprite iconSprite)
    {
        _monsterData = data;

        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(() => onClickAction?.Invoke(_monsterData));

        if (_monsterData.IsDiscovered)
        {
            nameText.text = _monsterData.Name;
            iconImage.color = Color.white; // Màu gốc

            // Gắn hình ảnh
            if (iconSprite != null) iconImage.sprite = iconSprite;
        }
        else
        {
            nameText.text = "???";
            iconImage.color = Color.black; // Đổi màu thành đen xì

            // Vẫn gắn hình ảnh nhưng nó sẽ hiển thị dưới dạng bóng đen
            if (iconSprite != null) iconImage.sprite = iconSprite;
        }
    }
}