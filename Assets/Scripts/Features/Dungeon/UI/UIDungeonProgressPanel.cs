using System;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Displays dungeon progress including elapsed time and monster kill count.
/// Automatically updates its UI by polling DungeonManager.
/// </summary>
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

    private void Awake()
    {
        _panelRect = transform as RectTransform;
        if (_panelRect == null) return;

        _expandedPosition = _panelRect.anchoredPosition;
        _targetX = GetCollapsedX();
    }

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

    /// <summary>
    /// Restart the timer and un-latch the "Cleared!" state. The panel lives in the always
    /// loaded Main HUD, so a dungeon restart never re-enables it — without this it stayed
    /// frozen on the finished run's time and "Cleared!" for the whole second run.
    /// </summary>
    public void ResetProgress()
    {
        _elapsedTime = 0f;
        _isRunning = true;
    }

    private void OnDisable()
    {
        _isRunning = false;
        _slideVelocity = 0f;
    }

    private void Update()
    {
        UpdateSlidePosition();

        if (!_isRunning || DungeonManager.Instance == null) return;

        // Update Time
        _elapsedTime += Time.deltaTime;
        if (timeText != null)
        {
            TimeSpan time = TimeSpan.FromSeconds(_elapsedTime);
            timeText.text = $"Time: {time.Minutes:D2}:{time.Seconds:D2}";
        }

        // Update Progress
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
                // Only the boss actually dying clears the dungeon. BossCount == 0 is NOT a
                // clear signal — it is also true during the ~1.2s shake before the boss
                // object exists, and latching _isRunning=false there froze the panel on
                // "Cleared!" with the boss still at full HP.
                progressText.text = "Cleared!";
                _isRunning = false;
            }
            else
            {
                progressText.text = "Boss Spawned!";
            }
        }

        // Auto-resize background based on text heights
        if (backgroundRect != null && timeText != null && progressText != null)
        {
            // 1. Ensure Background pivot is at the Top (Y = 1) so it grows downwards.
            // We adjust localPosition simultaneously so it doesn't visually jump when pivot changes.
            if (backgroundRect.pivot.y != 1f)
            {
                Vector2 size = backgroundRect.rect.size;
                float deltaY = 1f - backgroundRect.pivot.y;
                backgroundRect.pivot = new Vector2(backgroundRect.pivot.x, 1f);
                backgroundRect.localPosition += new Vector3(0, deltaY * size.y, 0);
            }

            // 2. Calculate required height
            // We need to account for: Top border, "DUNGEON PROGRESS" title height, 
            // the manual gaps between the texts, and the bottom border.
            // The previous 65f was too small to cover the manual gaps.
            float basePaddingAndGaps = 100f; 
            float timeHeight = timeText.preferredHeight;
            float monstersHeight = progressText.preferredHeight;
            
            float totalHeight = basePaddingAndGaps + timeHeight + monstersHeight;
            
            // 3. Apply height using SetSizeWithCurrentAnchors to avoid anchor conflicts
            backgroundRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _targetX = _expandedPosition.x;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _targetX = GetCollapsedX();
    }

    private float GetCollapsedX()
    {
        if (_panelRect == null) return _expandedPosition.x;

        float hiddenWidth = Mathf.Max(0f, _panelRect.rect.width - collapsedVisibleWidth);
        return _expandedPosition.x + hiddenWidth;
    }

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
