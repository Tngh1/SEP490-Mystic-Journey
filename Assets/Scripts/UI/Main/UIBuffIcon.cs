using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Executes i pointer exit handler operation.
// Validates input parameters against null or empty values.
public class UIBuffIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image borderImage;

    private ActiveBuff _buff;
    private bool _isHovering;

    // Executes setup operation.
    public void Setup(ActiveBuff buff)
    {
        _buff = buff;
        if (borderImage != null)
        {
            borderImage.color = _buff.IsDebuff ? Color.red : Color.green;
        }

        if (iconImage != null && !string.IsNullOrEmpty(_buff.IconName))
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>("Icons/Effects/" + _buff.IconName);
            if (sprites != null && sprites.Length > 0)
            {
                iconImage.sprite = sprites[0];
                iconImage.color = Color.white;
            }
        }
    }

    // Per-frame update loop for UIBuffIcon.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
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

    // Executes on pointer enter operation.
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
    }

    // Executes on pointer exit operation.
    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        if (UISimpleTooltip.Instance != null)
        {
            UISimpleTooltip.Instance.Hide();
        }
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        if (_isHovering && UISimpleTooltip.Instance != null)
        {
            UISimpleTooltip.Instance.Hide();
        }
        _isHovering = false;
    }
}
