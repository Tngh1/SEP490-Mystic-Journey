using UnityEngine;

// Executes i pointer exit handler operation.
public class UIHoverScaleEffect : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    private Vector3 originalScale;
    private Vector3 targetScale;
    private bool _initialized;

    // Initializes internal component caches and dependencies for UIHoverScaleEffect upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        InitScale();
    }

    // Performs startup initialization for UIHoverScaleEffect on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        InitScale();
    }

    // Executes init scale operation.
    private void InitScale()
    {
        if (!_initialized || originalScale == Vector3.zero)
        {
            originalScale = transform.localScale != Vector3.zero ? transform.localScale : Vector3.one;
            targetScale = originalScale;
            _initialized = true;
        }
    }

    // Per-frame update loop for UIHoverScaleEffect.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 15f);
        }
    }

    // Executes on pointer enter operation.
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        InitScale();
        targetScale = originalScale * 1.08f;
    }

    // Executes on pointer exit operation.
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        InitScale();
        targetScale = originalScale;
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        if (_initialized && originalScale != Vector3.zero)
        {
            transform.localScale = originalScale;
            targetScale = originalScale;
        }
    }
}
