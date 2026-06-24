using UnityEngine;

public class SkillProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    private float _damage;

    public void Setup(float damage)
    {
        _damage = damage;
        Destroy(gameObject, 2f);
    }

    void Update()
    {
        transform.Translate(Vector3.right * speed * Time.deltaTime, Space.Self);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Monster"))
        {
            EnemyEntity enemy = collision.GetComponent<EnemyEntity>();
            if (enemy != null)
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

            Destroy(gameObject); // Chạm quái thì đạn nổ biến mất
        }
    }
}