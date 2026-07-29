using UnityEngine;

/// <summary>
/// Skill Tấn Công Bụi Tiên (Fairy Dust) do IceFairy thi triển lên Player (mặc định 5s/lần trong tầm 8m).
/// Xuất hiện tại vị trí Player, gây sát thương và biến mất sau khi chạy xong hiệu ứng.
/// </summary>
public class FairyDustSkill : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Sát thương gây ra cho Player")]
    [SerializeField] private int damage = 20;

    [Tooltip("Thời gian tồn tại trước khi tự hủy (giây)")]
    [SerializeField] private float lifeTime = 1.0f;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private bool _hasDealtDamage = false;

    private void Start()
    {
        // Phát âm thanh va chạm/tấn công
        if (hitSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(hitSound, soundVolume);
        }

        // Gây sát thương cho Player nếu xuất hiện ngay trên Player
        Collider2D col = Physics2D.OverlapCircle(transform.position, 1.2f, LayerMask.GetMask("Player", "Default"));
        if (col != null && col.CompareTag("Player"))
        {
            DealDamage(col.gameObject);
        }

        // Tự động xóa Prefab sau khi hiệu ứng hoàn tất
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (_hasDealtDamage) return;

        if (col.CompareTag("Player"))
        {
            DealDamage(col.gameObject);
        }
    }

    private void DealDamage(GameObject target)
    {
        _hasDealtDamage = true;

        var networkPlayer = target.GetComponent<NetworkPlayer>();
        if (networkPlayer != null && networkPlayer.Object != null && networkPlayer.Object.IsValid)
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
