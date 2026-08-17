using UnityEngine;

// Executes mono behaviour operation.
public class SkillProjectile : MonoBehaviour
{
    [SerializeField] protected float speed = 10f;
    [SerializeField] protected AudioClip castSound;
    [SerializeField] protected AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] protected float soundVolume = 1f;
    protected float _damage;

    // Executes setup operation.
    public virtual void Setup(float damage)
    {
        _damage = damage;
        Destroy(gameObject, 2f);

        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }
    }

    // Per-frame update loop for SkillProjectile.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    protected virtual void Update()
    {
        transform.Translate(Vector3.right * speed * Time.deltaTime, Space.Self);
    }

    // Executes on trigger enter2 d operation.
    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player")) return;

        EnemyEntity enemy = collision.GetComponentInParent<EnemyEntity>();

        if (collision.isTrigger && enemy == null && !collision.CompareTag("Monster")) return;

        if (enemy != null || collision.CompareTag("Monster"))
        {
            if (enemy != null && !PlayerSkillVisualReplica.IsReplica(this))
            {
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
            OnHitTarget();
            return;
        }

        OnHitTarget();
    }

    // Executes on collision enter2 d operation.
    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        OnTriggerEnter2D(collision.collider);
    }

    // Executes on hit target operation.
    protected virtual void OnHitTarget()
    {
        if (hitSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(hitSound, soundVolume);
        }
        Destroy(gameObject);
    }
}
