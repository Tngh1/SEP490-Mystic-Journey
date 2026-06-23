using UnityEngine;

public class SkillProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    private float _damage;

    public void Setup(float damage)
    {
        _damage = damage;
        // Tự hủy sau 2 giây để tránh làm nặng bộ nhớ
        Destroy(gameObject, 2f);
    }

    void Update()
    {
        // Bay theo hướng trục X của Object (đã được xoay hoặc lật theo nhân vật)
        transform.Translate(Vector3.right * speed * Time.deltaTime, Space.Self);
    }

    // Khi chạm quái, bạn gọi hàm gây sát thương ở đây
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Monster"))
        {
            // Lấy component EnemyEntity của quái vật bị va chạm
            EnemyEntity enemy = collision.GetComponent<EnemyEntity>();
            if (enemy != null)
            {
                // Ép kiểu (int) vì _damage đang là float, còn TakeDamage nhận int
                enemy.TakeDamage((int)_damage);
            }

            Destroy(gameObject); // Chạm quái thì kỹ năng nổ/biến mất
        }
    }
}