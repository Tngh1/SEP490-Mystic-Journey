using UnityEngine;

// Executes mono behaviour operation.
public class EnemySkillDamage : MonoBehaviour
{
    [Tooltip("Sát thương của kỹ năng này")]
    [SerializeField] private int damage = 10;

    [Tooltip("Thời gian tồn tại của kỹ năng (tính bằng giây) trước khi tự động biến mất")]
    [SerializeField] private float lifeTime = 3f;

    [Tooltip("Có huỷ kỹ năng ngay sau khi chạm vào người chơi không?")]
    [SerializeField] private bool destroyOnHit = true;

    [Header("Audio Settings")]
    [Tooltip("Âm thanh xuất hiện của kỹ năng quái")]
    [SerializeField] private AudioClip castSound;

    [Tooltip("Âm thanh khi kỹ năng quái trúng người chơi")]
    [SerializeField] private AudioClip hitSound;

    [Tooltip("Âm lượng hiệu ứng âm thanh (0.0 đến 1.0)")]
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    // Performs startup initialization for EnemySkillDamage on the first active frame.
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
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null) return;
        HandleHit(collision.gameObject, collision.isTrigger);
    }

    // Executes on collision enter2 d operation.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null) return;
        HandleHit(collision.gameObject, false);
    }

    // Executes handle hit operation.
    private void HandleHit(GameObject hitObj, bool isTrigger)
    {
        if (hitObj.GetComponent<EnemyEntity>() != null || hitObj.GetComponent<EnemyBehaviour>() != null || hitObj.layer == LayerMask.NameToLayer("Ignore Raycast")) return;

        if (hitObj.CompareTag("Player"))
        {
            DealDamage(hitObj);
            if (destroyOnHit)
            {
                Destroy(gameObject);
            }
            return;
        }

        if (!isTrigger && destroyOnHit)
        {
            Destroy(gameObject);
        }
    }

    // Executes deal damage operation.
    private void DealDamage(GameObject target)
    {
        if (EnemySkillVisualReplica.IsReplica(this)) return;
        if (hitSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(hitSound, soundVolume);
        }

        var networkPlayer = target.GetComponent<NetworkPlayer>();
        if (networkPlayer != null)
        {
            networkPlayer.RequestDamage(damage);
        }
        else
        {
            var playerEntity = target.GetComponent<PlayerEntity>();
            if (playerEntity != null)
            {
                playerEntity.TakeDamage(damage);
            }
            else if (PlayerEntity.Instance != null)
            {
                PlayerEntity.Instance.TakeDamage(damage);
            }
        }
    }
}
