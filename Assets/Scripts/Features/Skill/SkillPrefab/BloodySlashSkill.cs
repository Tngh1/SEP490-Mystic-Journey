using UnityEngine;
using System.Collections.Generic;

public class BloodySlashSkill : SkillProjectile
{
    [SerializeField] private float duration = 0.5f; // Thời gian tồn tại của nhát chém (chỉnh cho khớp độ dài animation)
    
    // Lưu danh sách quái đã chém trúng để không bị trừ máu nhiều lần trong 1 nhát chém
    private HashSet<Collider2D> _damagedEnemies = new HashSet<Collider2D>();

    private Transform _casterTransform;
    private Vector3 _offsetFromCaster;

    public override void Setup(float damage)
    {
        base.Setup(damage);
        // Thay vì 2s như đạn bay, nhát chém sẽ tự huỷ rất nhanh (tùy theo animation duration)
        Destroy(gameObject, duration);

        Transform replicaOwner = PlayerSkillVisualReplica.GetOwner(this);
        GameObject player = replicaOwner == null ? GameObject.FindGameObjectWithTag("Player") : null;
        Transform playerTransform = replicaOwner != null ? replicaOwner : player?.transform;
        if (playerTransform != null)
        {
            Transform fp = playerTransform.Find("FirePoint");
            _casterTransform = fp != null ? fp : playerTransform;
            _offsetFromCaster = transform.position - _casterTransform.position;
        }
    }

    protected override void Update()
    {
        // Di chuyển nhát chém bám theo người chơi khi người chơi di chuyển
        if (_casterTransform != null)
        {
            transform.position = _casterTransform.position + _offsetFromCaster;
        }
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (PlayerSkillVisualReplica.IsReplica(this)) return;

        EnemyEntity enemy = collision.GetComponentInParent<EnemyEntity>();
        if (enemy != null || collision.CompareTag("Monster"))
        {
            // Nếu con quái này chưa nhận sát thương từ nhát chém này
            if (!_damagedEnemies.Contains(collision))
            {
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
