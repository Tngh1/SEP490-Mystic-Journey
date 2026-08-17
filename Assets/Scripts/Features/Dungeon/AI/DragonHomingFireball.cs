using UnityEngine;

// Executes mono behaviour operation.
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

    // Initializes internal component caches and dependencies for DragonHomingFireball upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    // Performs startup initialization for DragonHomingFireball on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        if (_animator != null && !string.IsNullOrEmpty(flyAnimState))
        {
            _animator.Play(flyAnimState);
        }

        if (_target == null)
        {
            FindTargetPlayer();
        }

        Destroy(gameObject, lifeTime);
    }

    // Executes set target operation.
    public void SetTarget(Transform targetPlayer)
    {
        _target = targetPlayer;
    }

    // Per-frame update loop for DragonHomingFireball.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (_isHit) return;

        if (_target == null)
        {
            FindTargetPlayer();
        }

        if (_target != null)
        {
            Vector3 direction = (_target.position - transform.position).normalized;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);

            transform.position += transform.right * speed * Time.deltaTime;
        }
        else
        {
            transform.position += transform.right * speed * Time.deltaTime;
        }
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D col)
    {
        if (_isHit || col == null) return;

        if (col.GetComponent<EnemyEntity>() != null || col.GetComponent<EnemyBehaviour>() != null || col.gameObject.layer == LayerMask.NameToLayer("Ignore Raycast")) return;

        if (col.CompareTag("Player"))
        {
            _isHit = true;
            DealDamage(col.gameObject);
            return;
        }

        if (!col.isTrigger)
        {
            _isHit = true;
            TriggerExplosion();
        }
    }

    // Executes deal damage operation.
    private void DealDamage(GameObject playerObj)
    {
        if (EnemySkillVisualReplica.IsReplica(this))
        {
            TriggerExplosion();
            return;
        }
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

    // Executes trigger explosion operation.
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

    // Executes find target player operation.
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
