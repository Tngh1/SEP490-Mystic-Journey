using UnityEngine;
using System.Collections.Generic;

// Executes skill projectile operation.
public class BloodySlashSkill : SkillProjectile
{
    [SerializeField] private float duration = 0.5f;

    private HashSet<Collider2D> _damagedEnemies = new HashSet<Collider2D>();

    private Transform _casterTransform;
    private Vector3 _offsetFromCaster;

    // Executes setup operation.
    public override void Setup(float damage)
    {
        base.Setup(damage);
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

    // Per-frame update loop for BloodySlashSkill.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    protected override void Update()
    {
        if (_casterTransform != null)
        {
            transform.position = _casterTransform.position + _offsetFromCaster;
        }
    }

    // Executes on trigger enter2 d operation.
    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (PlayerSkillVisualReplica.IsReplica(this)) return;

        EnemyEntity enemy = collision.GetComponentInParent<EnemyEntity>();
        if (enemy != null || collision.CompareTag("Monster"))
        {
            if (!_damagedEnemies.Contains(collision))
            {
                if (enemy != null)
                {
                    // Randomize the eligible candidates before selecting this gameplay result.
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
