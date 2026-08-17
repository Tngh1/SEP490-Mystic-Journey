using UnityEngine;

// Executes mono behaviour operation.
public class SlimePillarSkill : MonoBehaviour
{
    [Header("Debuff Settings")]
    [Tooltip("Tỷ lệ làm chậm (0.5 nghĩa là giảm 50% tốc độ gốc)")]
    [SerializeField] private float slowMultiplier = 0.5f;

    [Tooltip("Sát thương mỗi lần trừ máu (Damage over Time)")]
    [SerializeField] private int damagePerTick = 5;

    [Tooltip("Khoảng cách mỗi lần trừ máu (tính bằng giây)")]
    [SerializeField] private float tickInterval = 1f;

    [Tooltip("Thời gian tồn tại của hiệu ứng chất nhầy trên người chơi (giây)")]
    [SerializeField] private float debuffDuration = 5f;

    [Header("Skill Settings")]
    [Tooltip("Thời gian tự huỷ của chính cái cọc slime này (để nó không tồn tại mãi trên bản đồ)")]
    [SerializeField] private float lifeTime = 2f;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip castSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    // Performs startup initialization for SlimePillarSkill on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        Destroy(gameObject, lifeTime);
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
        {
            if (hitSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
            {
                MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(hitSound, soundVolume);
            }

            SlimeDebuff.ApplyTo(col.gameObject, slowMultiplier, damagePerTick, tickInterval, debuffDuration);
        }
    }
}
