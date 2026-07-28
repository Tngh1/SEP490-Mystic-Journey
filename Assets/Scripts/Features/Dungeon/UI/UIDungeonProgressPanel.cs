using System;
using UnityEngine;
using TMPro;

/// <summary>
/// Displays dungeon progress including elapsed time and monster kill count.
/// Automatically updates its UI by polling DungeonManager.
/// </summary>
public class UIDungeonProgressPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text progressText;
    
    [SerializeField] private RectTransform backgroundRect;
    
    private float _elapsedTime;
    private bool _isRunning;

    private void OnEnable()
    {
        _elapsedTime = 0f;
        _isRunning = true;
    }

    private void OnDisable()
    {
        _isRunning = false;
    }

    private void Update()
    {
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
            else
            {
                int bosses = DungeonManager.Instance.BossCount;
                if (bosses > 0)
                {
                    progressText.text = "Boss Spawned!";
                }
                else
                {
                    progressText.text = "Cleared!";
                    _isRunning = false;
                }
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
}
