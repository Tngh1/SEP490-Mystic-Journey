using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Skill Nhát Chém Bí Ngô (Pumpkin Slash) dành cho Đấu sĩ (Knight).
/// Logic tương tự BloodySlash: Nhát chém cận chiến bám theo người chơi,
/// chém xuyên tất cả quái vật trong vùng chém (mỗi quái nhận sát thương 1 lần)
/// và tự biến mất sau thời gian duration.
/// </summary>
public class PumpkinSlashSkill : SkillProjectile
{
    [SerializeField] private float duration = 0.5f; // Thời gian tồn tại của nhát chém
    
    // Danh sách quái đã nhận sát thương từ nhát chém này
    private HashSet<Collider2D> _damagedEnemies = new HashSet<Collider2D>();

    private Transform _casterTransform;
    private Vector3 _offsetFromCaster;

    public override void Setup(float damage)
    {
        base.Setup(damage);
        // Nhát chém tự hủy sau thời gian duration
        Destroy(gameObject, duration);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Transform fp = player.transform.Find("FirePoint");
            _casterTransform = fp != null ? fp : player.transform;
            _offsetFromCaster = transform.position - _casterTransform.position;
        }
    }

    protected override void Update()
    {
        // Nhát chém bám theo vị trí người chơi
        if (_casterTransform != null)
        {
            transform.position = _casterTransform.position + _offsetFromCaster;
        }
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Monster"))
        {
            if (!_damagedEnemies.Contains(collision))
            {
                EnemyEntity enemy = collision.GetComponent<EnemyEntity>();
                if (enemy != null)
                {
                    bool isCrit = Random.Range(0f, 100f) <= 20f;
                    float finalDamage = isCrit ? _damage * 1.5f : _damage;
                    int damageInt = Mathf.RoundToInt(finalDamage);

                    enemy.TakeDamage(damageInt);
                    _damagedEnemies.Add(collision);

                    if (DamagePopupManager.Instance != null)
                    {
                        DamagePopupManager.Instance.Create(enemy.transform.position, damageInt, isCrit, false);
                    }
                }
            }
        }
    }
}
