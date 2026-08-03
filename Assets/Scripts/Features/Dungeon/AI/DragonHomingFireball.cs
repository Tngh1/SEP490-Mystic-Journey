using UnityEngine;

/// <summary>
/// Quả cầu lửa truy đuổi (Homing Fireball) của Boss Rồng.
/// Tự động chuyển animation giữa bay (fireballFly) và nổ (fireboom).
/// Bay truy đuổi theo người chơi (Player). Khi va chạm gây 15 sát thương.
/// </summary>
public class DragonHomingFireball : MonoBehaviour
{
    [Header("Fireball Settings")]
    [Tooltip("Sát thương gây ra khi va chạm người chơi")]
    [SerializeField] private int damage = 15;

    [Tooltip("Tốc độ bay của quả cầu lửa")]
    [SerializeField] private float speed = 6.0f;

    [Tooltip("Tốc độ bẻ hướng/truy đuổi mục tiêu")]
    [SerializeField] private float rotateSpeed = 10.0f;

    [Tooltip("Thời gian tồn tại tối đa nếu không trúng mục tiêu (giây)")]
    [SerializeField] private float lifeTime = 5.0f;

    [Tooltip("Thời gian chờ animation nổ (fireboom) chạy xong trước khi Destroy (giây)")]
    [SerializeField] private float destroyDelay = 0.4f;

    [Header("Animation State Names")]
    [Tooltip("Tên state animation lúc bay truy đuổi")]
    [SerializeField] private string flyAnimState = "fireballFly";

    [Tooltip("Tên state animation lúc nổ khi trúng Player")]
    [SerializeField] private string boomAnimState = "fireboom";

    [Header("Audio Settings")]
    [SerializeField] private AudioClip castSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private Transform _target;
    private bool _isHit = false;
    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // Phát âm thanh xuất hiện
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        // Chạy animation bay
        if (_animator != null && !string.IsNullOrEmpty(flyAnimState))
        {
            _animator.Play(flyAnimState);
        }

        // Tự động tìm mục tiêu Player nếu chưa gán
        if (_target == null)
        {
            FindTargetPlayer();
        }

        // Hủy sau lifeTime nếu không trúng ai
        Destroy(gameObject, lifeTime);
    }

    public void SetTarget(Transform targetPlayer)
    {
        _target = targetPlayer;
    }

    private void Update()
    {
        if (_isHit) return;

        // Nếu mất mục tiêu thì cố gắng tìm lại Player gần nhất
        if (_target == null)
        {
            FindTargetPlayer();
        }

        if (_target != null)
        {
            // Tính hướng bay tới vị trí Player
            Vector3 direction = (_target.position - transform.position).normalized;

            // Xoay góc quả cầu lửa hướng về phía Player
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);

            // Di chuyển tới trước theo hướng đã xoay
            transform.position += transform.right * speed * Time.deltaTime;
        }
        else
        {
            // Nếu vẫn không có target, bay thẳng tới trước
            transform.position += transform.right * speed * Time.deltaTime;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (_isHit || col == null) return;

        if (col.GetComponent<EnemyEntity>() != null || col.GetComponent<EnemyBehaviour>() != null || col.CompareTag("IgnoreRaycast")) return;

        if (col.CompareTag("Player"))
        {
            _isHit = true;
            DealDamage(col.gameObject);
            return;
        }

        // Đâm vào tường / vật thể cản môi trường (không phải trigger) -> Kích hoạt nổ và huỷ cầu lửa
        if (!col.isTrigger)
        {
            _isHit = true;
            TriggerExplosion();
        }
    }

    private void DealDamage(GameObject playerObj)
    {
        var networkPlayer = playerObj.GetComponent<NetworkPlayer>();
        if (networkPlayer != null && networkPlayer.Object != null && networkPlayer.Object.IsValid)
        {
            networkPlayer.RequestDamage(damage);
        }
        else
        {
            var playerEntity = playerObj.GetComponent<PlayerEntity>();
            if (playerEntity != null)
            {
                playerEntity.TakeDamage(damage);
            }
            else if (PlayerEntity.Instance != null)
            {
                PlayerEntity.Instance.TakeDamage(damage);
            }
        }

        TriggerExplosion();
    }

    private void TriggerExplosion()
    {
        if (hitSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(hitSound, soundVolume);
        }

        if (_animator != null && !string.IsNullOrEmpty(boomAnimState))
        {
            _animator.Play(boomAnimState);
        }

        Destroy(gameObject, destroyDelay);
    }

    private void FindTargetPlayer()
    {
        if (PlayerMovement.Instance != null)
        {
            _target = PlayerMovement.Instance.transform;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _target = player.transform;
        }
    }
}
