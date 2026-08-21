using UnityEngine;

namespace MysticJourney.UI.Responsive
{
    /// <summary>
    /// Fits a UI content panel inside the device safe area (notches, rounded corners and system bars).
    /// Attach this to a full-stretch child of a screen-space Canvas; keep full-bleed backgrounds outside it.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            Rect safeArea = UnityEngine.Screen.safeArea;
            var screenSize = new Vector2Int(UnityEngine.Screen.width, UnityEngine.Screen.height);
            if (safeArea != lastSafeArea || screenSize != lastScreenSize)
                Refresh();
        }

        private void Refresh()
        {
            if (rectTransform == null && !TryGetComponent(out rectTransform))
                return;

            int width = Mathf.Max(1, UnityEngine.Screen.width);
            int height = Mathf.Max(1, UnityEngine.Screen.height);
            Rect safeArea = UnityEngine.Screen.safeArea;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= width;
            anchorMin.y /= height;
            anchorMax.x /= width;
            anchorMax.y /= height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(width, height);
        }
    }
}
