namespace Fusion.Statistics {
  using System;
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.Events;
  using UnityEngine.EventSystems;
  using UnityEngine.UI;
  
  /// <summary>
  /// The side to attach the statistics panel anchor.
  /// </summary>
  public enum CanvasAnchor {TopLeft, TopRight}

  
  // Executes i begin drag handler operation.
  public class FusionStatsCanvas : MonoBehaviour, IDragHandler, IEndDragHandler, IBeginDragHandler {
    [Header("General References")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private CanvasScaler _canvasScaler;
    [SerializeField] private RectTransform _canvasPanel;

    [Space] [Header("Panel References")] 
    [SerializeField] private RectTransform _contentPanel;
    [SerializeField] private RectTransform _contentContainer;
    [SerializeField] private RectTransform _bottomPanel;
    [SerializeField] private FusionStatsPanelHeader _header;
    
    [Space] [Header("Misc")] 
    [SerializeField] private Button _hideButton;
    [SerializeField] private Button _closeButton;

    [Space] [Header("World Anchor Panel Settings")] 
    [SerializeField] private FusionStatsConfig _config;
    
    // Executes _is colapsed operation.
    private bool _isColapsed => !_contentPanel.gameObject.activeSelf;
    
    private CanvasAnchor _anchor;
    
    // Executes drag mode operation.
    private enum DragMode {None, DragCanvas, ResizeContent}

    private DragMode _dragMode;
    private static int _statsCanvasActiveCount = 0;

    // Executes setup stats canvas operation.
    internal void SetupStatsCanvas(FusionStatistics fusionStatistics, CanvasAnchor canvasAnchor, UnityAction closeButtonAction) {
      _anchor = canvasAnchor;
      _canvasPanel.anchoredPosition = GetDefinedAnchorPosition();
      var maxOffsetMultiplier = Mathf.Min(_statsCanvasActiveCount, 3);
      _canvasPanel.anchoredPosition += Vector2.down * (FusionStatisticsHelper.DEFAULT_HEADER_HEIGHT * maxOffsetMultiplier);
      
      //Setup buttons
      _closeButton.onClick.RemoveAllListeners();
      _closeButton.onClick.AddListener(closeButtonAction);
      
      _hideButton.onClick.RemoveAllListeners();
      _hideButton.onClick.AddListener(ToggleHide);
      
      // Setup runner statistics ref
      _config.SetupStatisticReference(fusionStatistics);
    }

    // Executes on begin drag operation.
    public void OnBeginDrag(PointerEventData eventData) {
      if (_config.IsWorldAnchored) return;
      
      if (_dragMode != DragMode.None) return; // Already dragging.
      var dragBeginPos = eventData.pressPosition;
      var rectT = _bottomPanel;
      dragBeginPos = rectT.InverseTransformPoint(dragBeginPos);
      var resize = rectT.rect.Contains(dragBeginPos) && eventData.button == PointerEventData.InputButton.Right;
      _dragMode = resize ? DragMode.ResizeContent : DragMode.DragCanvas;
    }

    // Executes on drag operation.
    public void OnDrag(PointerEventData eventData) {
      if (_config.IsWorldAnchored) return;
      
      switch (_dragMode) {
        case DragMode.DragCanvas:
          _canvasPanel.anchoredPosition += eventData.delta / _canvas.scaleFactor;
          break;
        case DragMode.ResizeContent:
          UpdateContentContainerHeight(eventData.delta.y / _canvas.scaleFactor);
          break;
      }
    }

    // Executes on end drag operation.
    public void OnEndDrag(PointerEventData eventData) {
      if (_config.IsWorldAnchored) return;
      
      if (CheckDraggableRectVisibility(_canvasPanel) == false)
        SnapPanelBackToOriginPos();

      if (_dragMode == DragMode.ResizeContent) {
        var currentSize = _contentPanel.sizeDelta.y;
        var visibleGraphHeight = 0f;
        var remaining = 0f;
        for (int i = 0; i < _contentContainer.childCount; i++) {
          var prevHeight = visibleGraphHeight;
          visibleGraphHeight += ((RectTransform)_contentContainer.GetChild(i)).sizeDelta.y + 10;
          
          if (visibleGraphHeight >= currentSize) {
            if (currentSize - prevHeight < visibleGraphHeight - currentSize) {
              remaining = currentSize - prevHeight;
            } else {
             remaining = -(visibleGraphHeight - currentSize);
            }
            
            break;
          }
        }
        UpdateContentContainerHeight(remaining);
      }

      _dragMode = DragMode.None;
    }
    
    // Executes snap panel back to origin pos operation.
    public void SnapPanelBackToOriginPos() {
      _canvasPanel.anchoredPosition = GetDefinedAnchorPosition();
    }

    // Executes update content container height operation.
    private void UpdateContentContainerHeight(float yDelta) {
      var height = _contentPanel.sizeDelta.y;
      var targetHeight = height - yDelta;
      SetContentPanelHeight(targetHeight);
    }
    
    // Executes toggle hide operation.
    // Evaluates conditions and returns a boolean result.
    internal void ToggleHide() {
      var active = _contentPanel.gameObject.activeSelf;
      _hideButton.transform.rotation = active ? Quaternion.Euler(0, 0, 90) : Quaternion.identity;
      _contentPanel.gameObject.SetActive(!active);
      _bottomPanel.gameObject.SetActive(!active);
    }
    
    // Better offscreen check for later.
    private bool CheckDraggableRectVisibility(RectTransform rectTransform) {
      var anchoredPos = rectTransform.anchoredPosition;
      var size = rectTransform.rect.size;
      
      if (Mathf.Abs(anchoredPos.x) >= _canvasScaler.referenceResolution.x * .5f + size.x * .5f)
        return false;
      
      // anchor is on top.
      if (anchoredPos.y >= _canvasScaler.referenceResolution.y * .5f + size.y || anchoredPos.y <= -_canvasScaler.referenceResolution.y * .5f)
        return false;

      return true;
    }

    // Executes set content panel height operation.
    private void SetContentPanelHeight(float value) {
      if (value < FusionStatisticsHelper.DEFAULT_GRAPH_HEIGHT) {
        value = FusionStatisticsHelper.DEFAULT_GRAPH_HEIGHT;
      }else {
        var maxHeight = Screen.height / _canvas.scaleFactor - 2 * FusionStatisticsHelper.DEFAULT_HEADER_HEIGHT;
        if (value > maxHeight) {
          value = maxHeight;
        }
      }
      
      _contentPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, value);
      _contentPanel.gameObject.SetActive(false);
      _contentPanel.gameObject.SetActive(true);
    }

    // Executes adapt content height to graphs operation.
    private void AdaptContentHeightToGraphs() {
      var neededHeight = 0f;
      for (int i = 0; i < _contentContainer.childCount; i++) {
        neededHeight += ((RectTransform)_contentContainer.GetChild(i)).sizeDelta.y + 10; // +10 spacing
      }
      
      float maxHeight = Screen.height / _canvas.scaleFactor - 2 * FusionStatisticsHelper.DEFAULT_HEADER_HEIGHT;

      if (neededHeight > maxHeight) {
        neededHeight = maxHeight;
      }
      if (neededHeight < FusionStatisticsHelper.DEFAULT_GRAPH_HEIGHT) {
        neededHeight = FusionStatisticsHelper.DEFAULT_GRAPH_HEIGHT;
      }

      SetContentPanelHeight(neededHeight);
    }

    // Callback invoked when FusionStatsCanvas becomes enabled and active in the scene hierarchy.
    // Subscribes to global game events and refreshes visible UI displays.
    private void OnEnable() {
      _statsCanvasActiveCount++;
      _header.OnRenderStatsUpdate += AdaptContentHeightToGraphs;
    }

    // Callback invoked when FusionStatsCanvas becomes disabled in the scene hierarchy.
    // Unregisters event listeners to prevent unintended callbacks while inactive.
    private void OnDisable() {
      _statsCanvasActiveCount--;
      _header.OnRenderStatsUpdate -= AdaptContentHeightToGraphs;
    }

    // Executes set canvas anchor operation.
    public void SetCanvasAnchor(CanvasAnchor anchor) {
      _anchor = anchor;
      SnapPanelBackToOriginPos();
    }
    
    // Executes get defined anchor position operation.
    private Vector2 GetDefinedAnchorPosition() {
      var refRes = _canvasScaler.referenceResolution;
      switch (_anchor) {
        case CanvasAnchor.TopRight:
          return refRes * .5f - Vector2.right * (_canvasPanel.sizeDelta.x * .5f);
        case CanvasAnchor.TopLeft:
          refRes.x *= -1;
          return refRes * .5f + Vector2.right * (_canvasPanel.sizeDelta.x * .5f);
        default:
          return Vector2.zero;
      }
    }
  }
}