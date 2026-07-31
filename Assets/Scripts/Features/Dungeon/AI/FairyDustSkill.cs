using UnityEngine;

/// <summary>
/// Skill Tấn Công Bụi Tiên (Fairy Dust) do IceFairy thi triển lên Player (mặc định 5s/lần trong tầm 8m).
/// Xuất hiện tại vị trí Player, đi theo di chuyển của Player, gây sát thương và tự biến mất.
/// </summary>
public class FairyDustSkill : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Sát thương gây ra cho Player")]
    [SerializeField] private int damage = 20;

    [Tooltip("Thời gian tồn tại trước khi tự hủy (giây)")]
    [SerializeField] private float lifeTime = 1.0f;

    [Header("Tracking Settings")]
    [Tooltip("Có liên tục đi theo di chuyển của Player khi đang phát hiệu ứng không")]
    [SerializeField] private bool followPlayer = true;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private bool _hasDealtDamage = false;
    private Transform _targetPlayer;

    public void SetTarget(Transform target)
    {
        _targetPlayer = target;
    }

    private void Start()
    {
        if (_targetPlayer == null)
        {
            FindPlayerTarget();
        }

        if (_targetPlayer != null)
        {
            transform.position = new Vector3(_targetPlayer.position.x, _targetPlayer.position.y, 0f);
        }

        // Phát âm thanh va chạm/tấn công
        if (hitSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(hitSound, soundVolume);
        }

        // Gây sát thương cho Player nếu xuất hiện ngay trên Player
        if (_targetPlayer != null)
        {
            DealDamage(_targetPlayer.gameObject);
        }
        else
        {
            Collider2D col = Physics2D.OverlapCircle(transform.position, 1.5f, LayerMask.GetMask("Player", "Default"));
            if (col != null && col.CompareTag("Player"))
            {
                DealDamage(col.gameObject);
            }
        }

        // Tự động xóa Prefab sau khi hiệu ứng hoàn tất
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        if (followPlayer)
        {
            if (_targetPlayer == null)
            {
                FindPlayerTarget();
            }

            if (_targetPlayer != null)
            {
                transform.position = new Vector3(_targetPlayer.position.x, _targetPlayer.position.y, 0f);
            }
        }
    }

    private void FindPlayerTarget()
    {
        if (PlayerMovement.Instance != null)
        {
            _targetPlayer = PlayerMovement.Instance.transform;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _targetPlayer = player.transform;
        }
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
