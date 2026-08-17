using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// Executes mono behaviour operation.
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

    // Performs startup initialization for ToggleButtonUI on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        RefreshUI();
    }

    // Executes toggle operation.
    public void Toggle()
    {
        isOn = !isOn;

        RefreshUI();

        if (isOn)
            onTurnOn?.Invoke();
        else
            onTurnOff?.Invoke();
    }

    // Executes set state operation.
    public void SetState(bool value)
    {
        isOn = value;
        RefreshUI();
    }

    // Executes refresh ui operation.
    private void RefreshUI()
    {
        toggleImage.sprite = isOn ? onSprite : offSprite;
    }
}
