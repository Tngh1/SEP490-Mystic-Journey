using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giữ kích thước Handle của một Scrollbar CỐ ĐỊNH theo PIXEL, không đổi theo tỷ lệ
/// viewport/content.
///
/// ScrollRect mỗi LateUpdate tự set <see cref="Scrollbar.size"/> = viewport/content và
/// Scrollbar resize handle theo size. Component chạy LateUpdate SAU ScrollRect
/// (DefaultExecutionOrder cao hơn), ép lại:
///   • Trục TRƯỢT: size = handleLength / areaLength -> handle dài đúng handleLength pixel.
///   • Trục VUÔNG GÓC (cross): SetSizeWithCurrentAnchors = handleThickness pixel (giữ tỷ lệ
///     art gốc, có thể to hơn bề dày khung). handleThickness<=0 -> khớp đúng bề dày khung.
/// Vị trí (value) vẫn do ScrollRect điều khiển; scroll bình thường.
/// </summary>
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Scrollbar))]
public class FixedScrollbarHandleSize : MonoBehaviour
{
    [Tooltip("Chiều dài handle theo trục trượt (pixel).")]
    [SerializeField] private float handleLength = 41.2f;

    [Tooltip("Bề dày handle theo trục vuông góc (pixel). 0 = khớp đúng bề dày khung.")]
    [SerializeField] private float handleThickness = 0f;

    private Scrollbar _scrollbar;

    private void LateUpdate()
    {
        // Lazy-resolve: component có thể được gắn lúc GameObject đang inactive nên
        // không dựa vào Awake timing.
        if (_scrollbar == null) _scrollbar = GetComponent<Scrollbar>();
        if (_scrollbar == null || _scrollbar.handleRect == null) return;

        var slidingArea = _scrollbar.handleRect.parent as RectTransform;
        if (slidingArea == null) return;

        var handle = _scrollbar.handleRect;
        bool vertical = _scrollbar.direction == Scrollbar.Direction.BottomToTop
                     || _scrollbar.direction == Scrollbar.Direction.TopToBottom;

        // TRỤC TRƯỢT: zero sizeDelta rồi khóa size. Handle rect = size*areaLength + sizeDelta;
        // sizeDelta thừa làm handle thò ra 2 đầu Sliding Area nên phải zero.
        float areaLength = vertical ? slidingArea.rect.height : slidingArea.rect.width;
        if (areaLength <= 0f) return;

        var sd = handle.sizeDelta;
        if (vertical) sd.y = 0f; else sd.x = 0f;
        if (handle.sizeDelta != sd) handle.sizeDelta = sd;

        float target = Mathf.Clamp01(handleLength / areaLength);
        if (!Mathf.Approximately(_scrollbar.size, target))
            _scrollbar.size = target;

        // TRỤC VUÔNG GÓC: ép bề dày cố định (giữ tỷ lệ art). SetSizeWithCurrentAnchors tự tính
        // sizeDelta bù theo anchor hiện tại -> rect cross đúng handleThickness bất kể anchor.
        var crossAxis = vertical ? RectTransform.Axis.Horizontal : RectTransform.Axis.Vertical;
        if (handleThickness > 0f)
        {
            float cross = vertical ? handle.rect.width : handle.rect.height;
            if (!Mathf.Approximately(cross, handleThickness))
                handle.SetSizeWithCurrentAnchors(crossAxis, handleThickness);
        }
        else
        {
            // Khớp bề dày khung: zero sizeDelta cross (chỉ khi khung cross-size > 0).
            var sd2 = handle.sizeDelta;
            if (vertical) { if (slidingArea.rect.width > 0f) sd2.x = 0f; }
            else { if (slidingArea.rect.height > 0f) sd2.y = 0f; }
            if (handle.sizeDelta != sd2) handle.sizeDelta = sd2;
        }
    }
}
