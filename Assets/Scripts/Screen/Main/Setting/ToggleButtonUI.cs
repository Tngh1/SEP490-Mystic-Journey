using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class ToggleButtonUI : MonoBehaviour
{
    [Header("UI")]
    public Image toggleImage;

    public Sprite offSprite;
    public Sprite onSprite;

    [Header("State")]
    public bool isOn = false;

    [Header("Events")]
    public UnityEvent onTurnOn;
    public UnityEvent onTurnOff;

    private void Start()
    {
        RefreshUI();
    }

    public void Toggle()
    {
        isOn = !isOn;

        RefreshUI();

        if (isOn)
            onTurnOn?.Invoke();
        else
            onTurnOff?.Invoke();
    }

    public void SetState(bool value)
    {
        isOn = value;
        RefreshUI();
    }

    private void RefreshUI()
    {
        toggleImage.sprite = isOn ? onSprite : offSprite;
    }
}