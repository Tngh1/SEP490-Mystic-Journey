using UnityEngine;
using System.Collections.Generic;

public class FrozenSashSkill : MonoBehaviour
{
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private AudioClip castSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private float _damage;
    private HashSet<Collider2D> _damagedEnemies = new HashSet<Collider2D>();

    public void Setup(float damage)
    {
        _damage = damage;
        Destroy(gameObject, duration);

        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        EnemyEntity enemy = collision.GetComponentInParent<EnemyEntity>();
        if (enemy != null || collision.CompareTag("Monster"))
        {
            if (!_damagedEnemies.Contains(collision))
            {
                if (enemy != null)
                {
                    bool isCrit = Random.Range(0f, 100f) <= 20f;
                    float finalDamage = isCrit ? _damage * 1.5f : _damage;
                    int damageInt = Mathf.RoundToInt(finalDamage);

                    enemy.TakeDamage(damageInt);
                    _damagedEnemies.Add(collision);

                    if (hitSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
                    {
                        MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(hitSound, soundVolume);
                    }

                    if (DamagePopupManager.Instance != null)
                    {
                        DamagePopupManager.Instance.Create(enemy.transform.position, damageInt, isCrit, false);
                    }
                }
            }
        }
    }
}
