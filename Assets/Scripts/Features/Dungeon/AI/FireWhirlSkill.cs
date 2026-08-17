using UnityEngine;
using System.Collections.Generic;

// Executes mono behaviour operation.
public class FireWhirlSkill : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Sát thương tức thì khi va chạm vào người chơi")]
    [SerializeField] private int instantDamage = 40;

    [Header("Burn Debuff Settings")]
    [Tooltip("Thời gian hiệu ứng thiêu đốt (giây)")]
    [SerializeField] private float burnDuration = 3f;

    [Tooltip("Tỷ lệ % máu tối đa bị thiêu đốt mỗi giây")]
    [SerializeField] private float burnPercentPerTick = 3f;

    [Tooltip("Khoảng thời gian mỗi lần trừ máu thiêu đốt (giây)")]
    [SerializeField] private float burnTickInterval = 1f;

    [Header("Lifetime Settings")]
    [Tooltip("Thời gian tồn tại của vòng lửa trước khi tự hủy (giây)")]
    [SerializeField] private float lifeTime = 3f;

    [Tooltip("Có hủy vòng lửa ngay sau khi chạm vào người chơi không")]
    [SerializeField] private bool destroyOnHit = false;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip castSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private HashSet<Collider2D> _hitPlayers = new HashSet<Collider2D>();

    // Performs startup initialization for FireWhirlSkill on the first active frame.
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
            if (!_hitPlayers.Contains(col))
            {
                _hitPlayers.Add(col);
                DealDamageAndApplyBurn(col.gameObject);

                if (destroyOnHit)
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    // Executes deal damage and apply burn operation.
    private void DealDamageAndApplyBurn(GameObject target)
    {
        if (EnemySkillVisualReplica.IsReplica(this)) return;
        if (hitSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(hitSound, soundVolume);
        }

        var networkPlayer = target.GetComponent<NetworkPlayer>();
        if (networkPlayer != null && networkPlayer.Object != null && networkPlayer.Object.IsValid)
        {
            networkPlayer.RequestDamage(instantDamage);
        }
        else
        {
            var playerEntity = target.GetComponent<PlayerEntity>();
            if (playerEntity != null)
            {
                playerEntity.TakeDamage(instantDamage);
            }
            else if (PlayerEntity.Instance != null)
            {
                PlayerEntity.Instance.TakeDamage(instantDamage);
            }
        }

        BurnDebuff.ApplyPercentTo(target, burnPercentPerTick, burnTickInterval, burnDuration);
    }
}
