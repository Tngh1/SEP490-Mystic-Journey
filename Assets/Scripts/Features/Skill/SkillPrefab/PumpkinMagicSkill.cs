using UnityEngine;
using System.Collections.Generic;

// Executes mono behaviour operation.
public class PumpkinMagicSkill : MonoBehaviour
{
    [Header("Skill Settings")]
    [Tooltip("Thời gian tồn tại của quả bí trên mặt đất trước khi tự nổ (giây)")]
    [SerializeField] private float duration = 5f;

    [Tooltip("Bán kính nổ gây sát thương AoE")]
    [SerializeField] private float explosionRadius = 2.5f;

    [Tooltip("Thời gian chờ animation nổ chạy xong trước khi hủy GameObject (giây)")]
    [SerializeField] private float explodeDuration = 0.5f;

    [Tooltip("Tên State hoặc Trigger nổ trong Animator")]
    [SerializeField] private string boomAnimState = "PumpkinmagicBoom";

    [Header("Audio Settings")]
    [SerializeField] private AudioClip castSound;
    [SerializeField] private AudioClip explodeSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Header("Layer Settings")]
    [SerializeField] private LayerMask monsterLayer;

    private float _damage;
    private bool _isExploding = false;
    private float _timer = 0f;
    private Animator _animator;

    // Initializes internal component caches and dependencies for PumpkinMagicSkill upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    // Executes setup operation.
    public void Setup(float damage)
    {
        _damage = damage;
        _timer = duration;
        _isExploding = false;

        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }
    }

    // Per-frame update loop for PumpkinMagicSkill.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (_isExploding) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            Explode();
        }
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_isExploding) return;

        if (collision.CompareTag("Monster"))
        {
            Explode();
        }
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

        if (_animator != null)
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
