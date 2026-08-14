using UnityEngine;

public class LightsaberSkill : MonoBehaviour
{
    [SerializeField] private float damageRadius = 1.5f;
    [SerializeField] private float duration = 2f;
    [SerializeField] private AudioClip castSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;
    
    private float _damage;

    public void Setup(float damage)
    {
        _damage = damage;
        Destroy(gameObject, duration);
        
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        if (PlayerSkillVisualReplica.IsReplica(this)) return;
        
        // Deal damage immediately at spawn position to targets in small radius
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, damageRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Monster"))
            {
                var enemy = hit.GetComponent<EnemyEntity>();
                if (enemy != null)
                {
                    bool isCrit = Random.Range(0f, 100f) <= 20f; // Could be from PlayerCombat
                    float finalDamage = isCrit ? _damage * 1.5f : _damage;
                    int damageInt = Mathf.RoundToInt(finalDamage);

                    enemy.TakeDamage(damageInt);
                    
                    if (DamagePopupManager.Instance != null)
                    {
                        DamagePopupManager.Instance.Create(enemy.transform.position, damageInt, isCrit, false);
                    }
                }
            }
        }
    }
}
