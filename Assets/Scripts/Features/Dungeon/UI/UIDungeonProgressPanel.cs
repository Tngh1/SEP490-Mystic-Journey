using System;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// Executes i pointer exit handler operation.
public class UIDungeonProgressPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private RectTransform backgroundRect;

    [Header("Hover Slide")]
    [SerializeField, Min(0f)] private float collapsedVisibleWidth = 10f;
    [SerializeField, Min(0.01f)] private float slideDuration = 0.25f;

    private float _elapsedTime;
    private bool _isRunning;
    private RectTransform _panelRect;
    private Vector2 _expandedPosition;
    private float _targetX;
    private float _slideVelocity;

    // Initializes internal component caches and dependencies for UIDungeonProgressPanel upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _panelRect = transform as RectTransform;
        if (_panelRect == null) return;

        _expandedPosition = _panelRect.anchoredPosition;
        _targetX = GetCollapsedX();
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        ResetProgress();

        if (_panelRect != null)
        {
            Vector2 position = _expandedPosition;
            position.x = GetCollapsedX();
            _panelRect.anchoredPosition = position;
            _targetX = position.x;
            _slideVelocity = 0f;
        }
    }

    // Executes reset progress operation.
    public void ResetProgress()
    {
        _elapsedTime = 0f;
        _isRunning = true;
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        _isRunning = false;
        _slideVelocity = 0f;
    }

    // Per-frame update loop for UIDungeonProgressPanel.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        UpdateSlidePosition();

        if (!_isRunning || DungeonManager.Instance == null) return;

        _elapsedTime += Time.deltaTime;
        if (timeText != null)
        {
            TimeSpan time = TimeSpan.FromSeconds(_elapsedTime);
            timeText.text = $"Time: {time.Minutes:D2}:{time.Seconds:D2}";
        }

        if (progressText != null)
        {
            int killed = DungeonManager.Instance.EnemiesKilledCount;
            int total = DungeonManager.Instance.TotalNormalEnemies;

            if (total == 0)
            {
                progressText.text = "Loading...";
            }
            else if (killed < total)
            {
                var progressDict = DungeonManager.Instance.EnemyProgress;
                string lines = "";
                foreach (var kvp in progressDict)
                {
                    lines += $"{kvp.Key} {kvp.Value.killed}/{kvp.Value.total}\n";
                }
                progressText.text = lines.TrimEnd('\n');
            }
            else if (DungeonManager.Instance.IsDungeonCleared)
            {
                progressText.text = "Cleared!";
                _isRunning = false;
            }
            else
            {
                progressText.text = "Boss Spawned!";
            }
        }

        if (backgroundRect != null && timeText != null && progressText != null)
        {
            if (backgroundRect.pivot.y != 1f)
            {
                Vector2 size = backgroundRect.rect.size;
                float deltaY = 1f - backgroundRect.pivot.y;
                backgroundRect.pivot = new Vector2(backgroundRect.pivot.x, 1f);
                backgroundRect.localPosition += new Vector3(0, deltaY * size.y, 0);
            }

            float basePaddingAndGaps = 100f;
            float timeHeight = timeText.preferredHeight;
            float monstersHeight = progressText.preferredHeight;

            float totalHeight = basePaddingAndGaps + timeHeight + monstersHeight;

            backgroundRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
        }
    }

    // Executes on pointer enter operation.
    public void OnPointerEnter(PointerEventData eventData)
    {
        _targetX = _expandedPosition.x;
    }

    // Executes on pointer exit operation.
    public void OnPointerExit(PointerEventData eventData)
    {
        _targetX = GetCollapsedX();
    }

    // Executes get collapsed x operation.
    private float GetCollapsedX()
    {
        if (_panelRect == null) return _expandedPosition.x;

        float hiddenWidth = Mathf.Max(0f, _panelRect.rect.width - collapsedVisibleWidth);
        return _expandedPosition.x + hiddenWidth;
    }

    // Executes update slide position operation.
    private void UpdateSlidePosition()
    {
        if (_panelRect == null) return;

        Vector2 position = _panelRect.anchoredPosition;
        position.x = Mathf.SmoothDamp(
            position.x,
            _targetX,
            ref _slideVelocity,
            slideDuration,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        if (Mathf.Abs(position.x - _targetX) < 0.01f)
        {
            position.x = _targetX;
            _slideVelocity = 0f;
        }

        _panelRect.anchoredPosition = position;
    }
}
