using UnityEngine;
using System.Collections.Generic;

// Executes mono behaviour operation.
public class PumpkinThrowSkill : MonoBehaviour
{
    [Header("Flight & Arc Settings")]
    [Tooltip("Khoảng cách ném tới trước nếu không có target chỉ định")]
    [SerializeField] private float throwDistance = 5f;

    [Tooltip("Độ cao đỉnh vòng cung (vòng bay cao bao nhiêu)")]
    [SerializeField] private float arcHeight = 2.5f;

    [Tooltip("Thời gian bay từ lúc ném đến khi chạm đất (giây)")]
    [SerializeField] private float flightDuration = 0.8f;

    [Header("Explosion Settings")]
    [Tooltip("Bán kính nổ gây sát thương AoE khi chạm đất")]
    [SerializeField] private float explosionRadius = 2.0f;

    [Tooltip("Thời gian chờ animation nổ chạy xong trước khi Destroy (giây)")]
    [SerializeField] private float explodeDuration = 0.5f;

    [Header("Animator States / Triggers")]
    [Tooltip("Tên state/trigger animation lúc ném/bay")]
    [SerializeField] private string flyAnimState = "PumpkinFly";

    [Tooltip("Tên state/trigger animation lúc rơi xuống")]
    [SerializeField] private string fallAnimState = "PumpkinFall";

    [Tooltip("Tên state/trigger animation lúc nổ")]
    [SerializeField] private string boomAnimState = "PumpkinBoom";

    [Header("Audio Settings")]
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private AudioClip explodeSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Header("Layer Settings")]
    [SerializeField] private LayerMask monsterLayer;

    private float _damage;
    private Vector3 _startPos;
    private Vector3 _targetPos;
    private float _elapsedTime = 0f;
    private bool _isFalling = false;
    private bool _isExploding = false;
    private Animator _animator;

    // Initializes internal component caches and dependencies for PumpkinThrowSkill upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    // Executes setup operation.
    // Validates input parameters against null or empty values.
    public void Setup(float damage)
    {
        Vector3 target = transform.position + transform.right * throwDistance;
        Setup(damage, target);
    }

    // Executes setup operation.
    public void Setup(float damage, Vector3 targetPosition)
    {
        _damage = damage;
        _startPos = transform.position;
        _targetPos = targetPosition;
        _elapsedTime = 0f;
        _isFalling = false;
        _isExploding = false;

        if (throwSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(throwSound, soundVolume);
        }

        if (_animator != null && !string.IsNullOrEmpty(flyAnimState))
        {
            _animator.Play(flyAnimState);
        }
    }

    // Per-frame update loop for PumpkinThrowSkill.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (_isExploding) return;

        _elapsedTime += Time.deltaTime;
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        float progress = Mathf.Clamp01(_elapsedTime / flightDuration);

        Vector3 currentPos = Vector3.Lerp(_startPos, _targetPos, progress);

        float heightOffset = Mathf.Sin(progress * Mathf.PI) * arcHeight;
        currentPos.y += heightOffset;

        transform.position = currentPos;

        if (!_isFalling && progress >= 0.5f)
        {
            _isFalling = true;
            if (_animator != null && !string.IsNullOrEmpty(fallAnimState))
            {
                _animator.Play(fallAnimState);
            }
        }

        if (progress >= 1f)
        {
            Explode();
        }
    }

    // Executes on trigger enter2 d operation.
    // Validates input parameters against null or empty values.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_isExploding) return;

        if (collision.CompareTag("Player")) return;

        Explode();
    }

    // Executes explode operation.
    public void Explode()
    {
        if (_isExploding) return;
        _isExploding = true;

        if (explodeSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(explodeSound, soundVolume);
        }

        if (_animator != null && !string.IsNullOrEmpty(boomAnimState))
        {
            _animator.Play(boomAnimState);
        }

        DealAoEDamage();

        Destroy(gameObject, explodeDuration);
    }

    // Executes deal ao e damage operation.
    private void DealAoEDamage()
    {
        if (PlayerSkillVisualReplica.IsReplica(this)) return;

        LayerMask targetMask = (monsterLayer != 0) ? monsterLayer : LayerMask.GetMask("Monster");
        if (targetMask == 0) targetMask = ~0;

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius, targetMask);
        HashSet<EnemyEntity> damagedEnemies = new HashSet<EnemyEntity>();

        foreach (var col in hitColliders)
        {
            EnemyEntity enemy = col.GetComponentInParent<EnemyEntity>();
            if (enemy != null || col.CompareTag("Monster"))
            {
                if (enemy != null && !damagedEnemies.Contains(enemy))
                {
                    damagedEnemies.Add(enemy);

                    // Randomize the eligible candidates before selecting this gameplay result.
                    bool isCrit = Random.Range(0f, 100f) <= 20f;
                    float finalDamage = isCrit ? _damage * 1.5f : _damage;
                    int damageInt = Mathf.RoundToInt(finalDamage);

                    enemy.TakeDamage(damageInt);

                    if (DamagePopupManager.Instance != null)
                    {
                        DamagePopupManager.Instance.Create(enemy.transform.position, damageInt, isCrit, false);
                    }
                }
            }
        }
    }

    // Executes on draw gizmos selected operation.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
