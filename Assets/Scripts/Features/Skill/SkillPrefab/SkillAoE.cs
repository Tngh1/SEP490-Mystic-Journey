using UnityEngine;
using System.Collections.Generic;

// Executes mono behaviour operation.
public class SkillAoE : MonoBehaviour
{
    [SerializeField] private float duration = 3f;
    [SerializeField] private AudioClip castSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;
    private float _damage;

    private HashSet<Collider2D> _damagedEnemies = new HashSet<Collider2D>();

    // Executes setup operation.
    public void Setup(float damage)
    {
        _damage = damage;
        Destroy(gameObject, duration);
        PlayCastAudio();
    }

    // Plays the configured cast clip for both legacy and network-spawned AoE effects.
    public void PlayCastAudio()
    {
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D collision)
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
