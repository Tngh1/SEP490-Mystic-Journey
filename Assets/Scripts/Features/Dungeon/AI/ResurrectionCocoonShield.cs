using UnityEngine;

/// <summary>
/// Khiên Kén Phục Sinh (Resurrection Cocoon Shield) bảo vệ Boss UnderKing.
/// Chặn 2 đòn đánh hoặc kỹ năng từ Player. Đồng thời chứa visual hiển thị kén quanh Boss.
/// </summary>
public class ResurrectionCocoonShield : MonoBehaviour
{
    [Header("Shield Settings")]
    [Tooltip("Số đòn đánh/kỹ năng tối đa có thể chặn")]
    [SerializeField] private int remainingBlockHits = 2;

    [Tooltip("Visual GameObject của Kén hiển thị quanh Boss")]
    [SerializeField] private GameObject visualObject;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip blockSound;
    [SerializeField] private AudioClip breakSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    public int RemainingBlockHits => remainingBlockHits;

    public void Initialize(int blockHits, GameObject visual)
    {
        remainingBlockHits = blockHits;
        visualObject = visual;
    }

    /// <summary>
    /// Kiểm tra và xử lý việc chặn đòn đánh của Player.
    /// Trả về true nếu đòn đánh bị chặn thành công (gây 0 sát thương).
    /// </summary>
    public bool TryBlockHit()
    {
        if (remainingBlockHits <= 0) return false;

        remainingBlockHits--;

        // Phát âm thanh khi chặn thành công
        if (blockSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(blockSound, soundVolume);
        }

        // Hiển thị Popup 0 khi chặn sát thương thành công
        if (DamagePopupManager.Instance != null)
        {
            DamagePopupManager.Instance.Create(transform.position, 0, false, false, false);
        }

        // Nếu hết lượt chặn -> vỡ kén
        if (remainingBlockHits <= 0)
        {
            BreakShield();
        }

        return true;
    }

    public void BreakShield()
    {
        if (breakSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(breakSound, soundVolume);
        }

        if (visualObject != null)
        {
            Destroy(visualObject);
        }

        Destroy(this);
    }
}
