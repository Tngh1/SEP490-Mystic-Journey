using UnityEngine;
using System.Collections.Generic;

public class SkillAoE : MonoBehaviour
{
    [SerializeField] private float duration = 3f; // Thời gian vòng phép tồn tại
    private float _damage;

    // Danh sách lưu những quái đã nhận sát thương để tránh việc 1 con quái bị trừ máu liên tục mỗi khung hình
    private HashSet<Collider2D> _damagedEnemies = new HashSet<Collider2D>();

    public void Setup(float damage)
    {
        _damage = damage;
        Destroy(gameObject, duration); // Tự biến mất sau khi hết hạn
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Monster"))
        {
            // Kiểm tra xem quái này đã bị dính đòn từ vòng phép này chưa
            if (!_damagedEnemies.Contains(collision))
            {
                EnemyEntity enemy = collision.GetComponent<EnemyEntity>();
                if (enemy != null)
                {
                    enemy.TakeDamage((int)_damage);
                    _damagedEnemies.Add(collision); // Đánh dấu là đã nhận sát thương
                }
            }
        }
    }
}