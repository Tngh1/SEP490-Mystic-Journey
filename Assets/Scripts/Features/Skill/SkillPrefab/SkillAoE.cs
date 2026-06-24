using UnityEngine;
using System.Collections.Generic;

public class SkillAoE : MonoBehaviour
{
    [SerializeField] private float duration = 3f;
    private float _damage;

    private HashSet<Collider2D> _damagedEnemies = new HashSet<Collider2D>();

    public void Setup(float damage)
    {
        _damage = damage;
        Destroy(gameObject, duration);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Monster"))
        {
            if (!_damagedEnemies.Contains(collision))
            {
                EnemyEntity enemy = collision.GetComponent<EnemyEntity>();
                if (enemy != null)
                {
                    // 👇 Thêm logic Chí mạng cho Vòng Phép
                    bool isCrit = Random.Range(0f, 100f) <= 20f;
                    float finalDamage = isCrit ? _damage * 1.5f : _damage;
                    int damageInt = Mathf.RoundToInt(finalDamage);

                    enemy.TakeDamage(damageInt);
                    _damagedEnemies.Add(collision);

                    // 👇 Hiện số sát thương bay lên
                    if (DamagePopupManager.Instance != null)
                    {
                        DamagePopupManager.Instance.Create(enemy.transform.position, damageInt, isCrit, false);
                    }
                }
            }
        }
    }
}