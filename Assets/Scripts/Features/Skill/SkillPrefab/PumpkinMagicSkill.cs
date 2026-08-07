using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Skill Bẫy Quả Bí (Pumpkin Magic) dành cho Xạ thủ (Archer).
/// Skill dạng AoE đặt trên mặt đất, tồn tại tối đa 5 giây.
/// Khi quái vật chạm vào hoặc hết 5s sẽ tự động phát nổ gây sát thương diện rộng.
/// </summary>
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

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    public void Setup(float damage)
    {
        _damage = damage;
        _timer = duration;
        _isExploding = false;

        // Phát âm thanh khi đặt skill
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }
    }

    private void Update()
    {
        if (_isExploding) return;

        // Đếm ngược 5s tự phát nổ
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            Explode();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_isExploding) return;

        // Nếu quái va chạm vào quả bí thì phát nổ ngay lập tức
        if (collision.CompareTag("Monster"))
        {
            Explode();
        }
    }

    public void Explode()
    {
        if (_isExploding) return;
        _isExploding = true;

        // Phát âm thanh nổ
        if (explodeSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(explodeSound, soundVolume);
        }

        // Kích hoạt animation nổ (PumpkinmagicBoom)
        if (_animator != null)
        {
            _animator.Play(boomAnimState);
        }

        // Gây sát thương AoE xung quanh vị trí quả bí
        DealAoEDamage();

        // Hủy GameObject sau khi animation nổ chạy xong
        Destroy(gameObject, explodeDuration);
    }

    private void DealAoEDamage()
    {
        LayerMask targetMask = (monsterLayer != 0) ? monsterLayer : LayerMask.GetMask("Monster");
        if (targetMask == 0) targetMask = ~0; // Fallback nếu chưa setup layer

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

                    // Logic Chí mạng (20% crit, x1.5 sát thương)
                    bool isCrit = Random.Range(0f, 100f) <= 20f;
                    float finalDamage = isCrit ? _damage * 1.5f : _damage;
                    int damageInt = Mathf.RoundToInt(finalDamage);

                    enemy.TakeDamage(damageInt);

                    // Hiển thị số sát thương
                    if (DamagePopupManager.Instance != null)
                    {
                        DamagePopupManager.Instance.Create(enemy.transform.position, damageInt, isCrit, false);
                    }
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ bán kính nổ trong cửa sổ Scene
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
