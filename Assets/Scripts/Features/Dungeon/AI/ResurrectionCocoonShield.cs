using UnityEngine;

// Executes mono behaviour operation.
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

    // Executes remaining block hits operation.
    public int RemainingBlockHits => remainingBlockHits;

    // Executes initialize operation.
    // Evaluates conditions and returns a boolean result.
    public void Initialize(int blockHits, GameObject visual)
    {
        remainingBlockHits = blockHits;
        visualObject = visual;
    }

    // Executes try block hit operation.
    public bool TryBlockHit()
    {
        if (remainingBlockHits <= 0) return false;

        remainingBlockHits--;

        if (blockSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(blockSound, soundVolume);
        }

        if (DamagePopupManager.Instance != null)
        {
            DamagePopupManager.Instance.Create(transform.position, 0, false, false, false);
        }

        if (remainingBlockHits <= 0)
        {
            BreakShield();
        }

        return true;
    }

    // Executes break shield operation.
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
