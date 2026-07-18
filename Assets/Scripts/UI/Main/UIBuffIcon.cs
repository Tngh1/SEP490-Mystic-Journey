using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIBuffIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image borderImage;

    private ActiveBuff _buff;
    private bool _isHovering;

    public void Setup(ActiveBuff buff)
    {
        _buff = buff;
        if (borderImage != null)
        {
            borderImage.color = _buff.IsDebuff ? Color.red : Color.green;
        }

        if (iconImage != null && !string.IsNullOrEmpty(_buff.IconName))
        {
            Sprite sprite = Resources.Load<Sprite>("Icons/Buffs/" + _buff.IconName);
            if (sprite != null)
            {
                iconImage.sprite = sprite;
                iconImage.color = Color.white;
            }
        }
    }

    private void Update()
    {
        if (_isHovering && UISimpleTooltip.Instance != null && _buff != null)
        {
            string typeStr = _buff.IsDebuff ? "<color=red>[Debuff]</color>" : "<color=green>[Buff]</color>";
            string title = $"{typeStr} {_buff.BuffName}";
            string timeText = $"<mspace=0.55em>{_buff.DurationRemaining:F1}</mspace>s";
            UISimpleTooltip.Instance.Show(title, timeText, transform.position + new Vector3(0, 50, 0));
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        if (UISimpleTooltip.Instance != null)
        {
            UISimpleTooltip.Instance.Hide();
        }
    }

    private void OnDisable()
    {
        if (_isHovering && UISimpleTooltip.Instance != null)
        {
            UISimpleTooltip.Instance.Hide();
        }
        _isHovering = false;
    }
}
