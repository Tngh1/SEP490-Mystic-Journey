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
        if (collision.CompareTag("Enemy"))
        {
            // collision.GetComponent<EnemyHealth>().TakeDamage(_damage);
            Destroy(gameObject); // Chạm quái thì nổ hoặc biến mất
        }
    }
}