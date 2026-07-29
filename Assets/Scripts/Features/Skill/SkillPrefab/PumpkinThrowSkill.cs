using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Skill Quăng Quả Bí Vòng Cung (Pumpkin Throw Skill) dành cho Đấu sĩ (Knight).
/// Quả bí sẽ bay theo quỹ đạo vòng cung (parabol). Khi lên đến đỉnh và bắt đầu rơi xuống,
/// script sẽ tự động chuyển sang animation rơi (PumpkinFall).
/// Khi chạm đất hoặc va chạm bất kỳ vật thể nào, quả bí sẽ nổ và chỉ gây sát thương lên Quái (`Monster`).
/// </summary>
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

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Hàm khởi tạo mặc định khi ném theo hướng nhìn
    /// </summary>
    public void Setup(float damage)
    {
        Vector3 target = transform.position + transform.right * throwDistance;
        Setup(damage, target);
    }

    /// <summary>
    /// Hàm khởi tạo có vị trí đích đến cụ thể (khi dùng chỉ báo vị trí con trỏ chuột/AoE target)
    /// </summary>
    public void Setup(float damage, Vector3 targetPosition)
    {
        _damage = damage;
        _startPos = transform.position;
        _targetPos = targetPosition;
        _elapsedTime = 0f;
        _isFalling = false;
        _isExploding = false;

        // Phát âm thanh quăng
        if (throwSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(throwSound, soundVolume);
        }

        // Chạy animation bay ban đầu
        if (_animator != null && !string.IsNullOrEmpty(flyAnimState))
        {
            _animator.Play(flyAnimState);
        }
    }

    private void Update()
    {
        if (_isExploding) return;

        _elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(_elapsedTime / flightDuration);

        // Tính vị trí Lerp ngang
        Vector3 currentPos = Vector3.Lerp(_startPos, _targetPos, progress);

        // Cộng độ cao vòng cung Parabol: Sin(progress * PI) sẽ bằng 0 ở 2 đầu và = 1 ở giữa (progress = 0.5)
        float heightOffset = Mathf.Sin(progress * Mathf.PI) * arcHeight;
        currentPos.y += heightOffset;

        transform.position = currentPos;

        // Chuyển sang Animation rơi khi đã qua đỉnh vòng cung (progress > 0.5)
        if (!_isFalling && progress >= 0.5f)
        {
            _isFalling = true;
            if (_animator != null && !string.IsNullOrEmpty(fallAnimState))
            {
                _animator.Play(fallAnimState);
            }
        }

        // Nếu bay hết thời gian mà chưa chạm va chạm -> Tự phát nổ tại điểm đích
        if (progress >= 1f)
        {
            Explode();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_isExploding) return;

        // Bỏ qua va chạm với Player (người chơi ném)
        if (collision.CompareTag("Player")) return;

        // Chạm vào bất kỳ object nào (đất, tường, chướng ngại vật hoặc quái) đều phát nổ
        Explode();
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

        // Chạy animation nổ chạm đất
        if (_animator != null && !string.IsNullOrEmpty(boomAnimState))
        {
            _animator.Play(boomAnimState);
        }

        // Gây sát thương AoE xung quanh (chỉ gây sát thương cho quái vật Monster)
        DealAoEDamage();

        // Xóa GameObject sau khi nổ xong
        Destroy(gameObject, explodeDuration);
    }

    private void DealAoEDamage()
    {
        LayerMask targetMask = (monsterLayer != 0) ? monsterLayer : LayerMask.GetMask("Monster");
        if (targetMask == 0) targetMask = ~0; // Fallback

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius, targetMask);
        HashSet<EnemyEntity> damagedEnemies = new HashSet<EnemyEntity>();

        foreach (var col in hitColliders)
        {
            // Chỉ lọc những đối tượng có Tag "Monster"
            if (col.CompareTag("Monster"))
            {
                EnemyEntity enemy = col.GetComponent<EnemyEntity>();
                if (enemy != null && !damagedEnemies.Contains(enemy))
                {
                    damagedEnemies.Add(enemy);

                    // Logic Chí mạng (20% crit, x1.5 sát thương)
                    bool isCrit = Random.Range(0f, 100f) <= 20f;
                    float finalDamage = isCrit ? _damage * 1.5f : _damage;
                    int damageInt = Mathf.RoundToInt(finalDamage);

                    enemy.TakeDamage(damageInt);

                    // Hiển thị popup số sát thương
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
        // Vẽ bán kính nổ trong Scene view
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
