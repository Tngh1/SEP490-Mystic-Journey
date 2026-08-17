using UnityEngine;
using UnityEngine.UI;

// Executes mono behaviour operation.
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Scrollbar))]
public class FixedScrollbarHandleSize : MonoBehaviour
{
    [Tooltip("Chiều dài handle theo trục trượt (pixel).")]
    [SerializeField] private float handleLength = 41.2f;

    [Tooltip("Bề dày handle theo trục vuông góc (pixel). 0 = khớp đúng bề dày khung.")]
    [SerializeField] private float handleThickness = 0f;

    private Scrollbar _scrollbar;

    // Executes late update operation.
    private void LateUpdate()
    {
        if (_scrollbar == null) _scrollbar = GetComponent<Scrollbar>();
        if (_scrollbar == null || _scrollbar.handleRect == null) return;

        var slidingArea = _scrollbar.handleRect.parent as RectTransform;
        if (slidingArea == null) return;

        var handle = _scrollbar.handleRect;
        bool vertical = _scrollbar.direction == Scrollbar.Direction.BottomToTop
                     || _scrollbar.direction == Scrollbar.Direction.TopToBottom;

        float areaLength = vertical ? slidingArea.rect.height : slidingArea.rect.width;
        if (areaLength <= 0f) return;

        var sd = handle.sizeDelta;
        if (vertical) sd.y = 0f; else sd.x = 0f;
        if (handle.sizeDelta != sd) handle.sizeDelta = sd;

        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        float target = Mathf.Clamp01(handleLength / areaLength);
        if (!Mathf.Approximately(_scrollbar.size, target))
            _scrollbar.size = target;

        var crossAxis = vertical ? RectTransform.Axis.Horizontal : RectTransform.Axis.Vertical;
        if (handleThickness > 0f)
        {
            float cross = vertical ? handle.rect.width : handle.rect.height;
            if (!Mathf.Approximately(cross, handleThickness))
                handle.SetSizeWithCurrentAnchors(crossAxis, handleThickness);
        }
        else
        {
            var sd2 = handle.sizeDelta;
            if (vertical) { if (slidingArea.rect.width > 0f) sd2.x = 0f; }
            else { if (slidingArea.rect.height > 0f) sd2.y = 0f; }
            if (handle.sizeDelta != sd2) handle.sizeDelta = sd2;
        }
    }
}
