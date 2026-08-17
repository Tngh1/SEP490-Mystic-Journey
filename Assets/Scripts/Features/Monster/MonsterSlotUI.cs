using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models.Response;
using System;

// Executes mono behaviour operation.
public class MonsterSlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [Tooltip("Optional: new slot UI no longer has a name label")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button slotButton;
    [Tooltip("Optional: highlight border when slot is selected")]
    [SerializeField] private GameObject activeDeco;

    private PlayerMonsterCatalogItem _monsterData;

    // Executes init operation.
    public void Init(PlayerMonsterCatalogItem data, Action<PlayerMonsterCatalogItem> onClickAction, Sprite iconSprite)
    {
        _monsterData = data;

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => onClickAction?.Invoke(_monsterData));

            if (slotButton.GetComponent<UIHoverScaleEffect>() == null)
                slotButton.gameObject.AddComponent<UIHoverScaleEffect>();
        }

        if (nameText != null)
            nameText.text = _monsterData.IsDiscovered ? _monsterData.Name : "???";

        if (iconImage != null)
        {
            iconImage.color = _monsterData.IsDiscovered ? Color.white : Color.black;
            if (iconSprite != null) iconImage.sprite = iconSprite;
        }

        SetSelected(false);
    }

    // Executes set selected operation.
    public void SetSelected(bool selected)
    {
        if (activeDeco != null) activeDeco.SetActive(selected);
    }
}
