using UnityEngine;

// Executes pointer hover scale effect on UI elements.
public class UIHoverScaleEffect : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    [SerializeField] private float hoverScaleFactor = 1.15f;
    [SerializeField] private float lerpSpeed = 15f;
    public Transform targetTransform; // Optional target to scale, defaults to this transform
    
    private Vector3 originalScale;
    private Vector3 targetScale;
    private bool _initialized;

    // Initializes internal component caches and dependencies.
    private void Awake()
    {
        InitScale();
    }

    // Performs startup initialization.
    private void Start()
    {
        InitScale();
    }

    // Executes init scale operation.
    private void InitScale()
    {
        if (targetTransform == null) targetTransform = transform;
        
        if (!_initialized || originalScale == Vector3.zero)
        {
            originalScale = targetTransform.localScale != Vector3.zero ? targetTransform.localScale : Vector3.one;
            targetScale = originalScale;
            _initialized = true;
        }
    }

    // Per-frame update loop for smooth scale interpolation.
    private void Update()
    {
        if (targetTransform != null && targetTransform.localScale != targetScale)
        {
            targetTransform.localScale = Vector3.Lerp(targetTransform.localScale, targetScale, Time.unscaledDeltaTime * lerpSpeed);
        }
    }

    // Executes on pointer enter operation.
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        InitScale();
        targetScale = originalScale * hoverScaleFactor;
    }

    // Executes on pointer exit operation.
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        InitScale();
        targetScale = originalScale;
    }

    // Restores original scale when disabled.
    private void OnDisable()
    {
        if (_initialized && originalScale != Vector3.zero && targetTransform != null)
        {
            targetTransform.localScale = originalScale;
            targetScale = originalScale;
        }
    }
}
