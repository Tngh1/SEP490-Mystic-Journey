using UnityEngine;
using System.Collections.Generic;

public class FrozenSashSkill : SkillProjectile
{
    [SerializeField] private float duration = 0.5f; // Thời gian tồn tại của nhát chém (chỉnh cho khớp độ dài animation)
    
    // Lưu danh sách quái đã chém trúng để không bị trừ máu nhiều lần trong 1 nhát chém
    private HashSet<Collider2D> _damagedEnemies = new HashSet<Collider2D>();

    public override void Setup(float damage)
    {
        _damage = damage;
        // Thay vì 2s như đạn bay, nhát chém sẽ tự huỷ rất nhanh (tùy theo animation duration)
        Destroy(gameObject, duration);
    }

    protected override void Update()
    {
        // Cố tình bỏ trống (KHÔNG gọi base.Update()) để skill không bị di chuyển về phía trước như viên đạn.
        // Nó sẽ nằm cố định ngay trước mặt nhân vật giống một nhát chém cận chiến.
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Monster"))
        {
            // Nếu con quái này chưa nhận sát thương từ nhát chém này
            if (!_damagedEnemies.Contains(collision))
            {
                EnemyEntity enemy = collision.GetComponent<EnemyEntity>();
                if (enemy != null)
                {
                    bool isCrit = Random.Range(0f, 100f) <= 20f;
                    float finalDamage = isCrit ? _damage * 1.5f : _damage;
                    int damageInt = Mathf.RoundToInt(finalDamage);

                    enemy.TakeDamage(damageInt);
                    
                    // Thêm vào danh sách để không chém 1 con quái 2 lần
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
