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
    }

    private void Update()
    {
        if (_isHovering && UISimpleTooltip.Instance != null && _buff != null)
        {
            string typeStr = _buff.IsDebuff ? "<color=red>[Bất Lợi]</color>" : "<color=green>[Có Lợi]</color>";
            string info = $"{typeStr} {_buff.BuffName}\nCòn lại: {_buff.DurationRemaining:F1}s";
            UISimpleTooltip.Instance.Show(info, transform.position + new Vector3(0, 50, 0)); // Hiển thị phía trên icon 50px
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
