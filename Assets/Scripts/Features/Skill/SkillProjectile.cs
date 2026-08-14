using UnityEngine;

public class SkillProjectile : MonoBehaviour
{
    [SerializeField] protected float speed = 10f;
    [SerializeField] protected AudioClip castSound;
    [SerializeField] protected AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] protected float soundVolume = 1f;
    protected float _damage;

    public virtual void Setup(float damage)
    {
        _damage = damage;
        Destroy(gameObject, 2f);

        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }
    }

    protected virtual void Update()
    {
        transform.Translate(Vector3.right * speed * Time.deltaTime, Space.Self);
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        // Bỏ qua va chạm với Player (người tung skill hoặc đồng đội)
        if (collision.CompareTag("Player")) return;

        EnemyEntity enemy = collision.GetComponentInParent<EnemyEntity>();

        // Bỏ qua các vùng kích hoạt ẩn (Trigger) không phải là Monster (ví dụ: fader cây/nhà, portal)
        if (collision.isTrigger && enemy == null && !collision.CompareTag("Monster")) return;

        if (enemy != null || collision.CompareTag("Monster"))
        {
            if (enemy != null && !PlayerSkillVisualReplica.IsReplica(this))
            {
                // 👇 Thêm logic Chí mạng cho Kỹ năng (Tạm để 20% crit, x1.5 sát thương)
                bool isCrit = Random.Range(0f, 100f) <= 20f;
                float finalDamage = isCrit ? _damage * 1.5f : _damage;
                int damageInt = Mathf.RoundToInt(finalDamage);

                enemy.TakeDamage(damageInt);

                // 👇 Hiện số sát thương bay lên
                if (DamagePopupManager.Instance != null)
                {
                    DamagePopupManager.Instance.Create(enemy.transform.position, damageInt, isCrit, false);
                }
            }
            OnHitTarget();
            return;
        }

        // Đạn va chạm bất kỳ vật thể nào trên bản đồ (Monster, tường, địa hình, chướng ngại vật...) đều nổ / biến mất
        OnHitTarget();
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        OnTriggerEnter2D(collision.collider);
    }

    protected virtual void OnHitTarget()
    {
        if (hitSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(hitSound, soundVolume);
        }
        Destroy(gameObject); // Chạm quái thì đạn nổ biến mất
    }
}
