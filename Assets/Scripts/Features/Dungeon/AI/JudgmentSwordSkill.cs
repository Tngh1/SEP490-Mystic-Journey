using System.Collections;
using UnityEngine;
using MysticJourney.Core.Services;

// Executes mono behaviour operation.
public class JudgmentSwordSkill : MonoBehaviour
{
    [Header("Judgment Settings")]
    [Tooltip("Thời gian trễ từ lúc gọi skill đến lúc chém xuống (để người chơi lướt né)")]
    [SerializeField] private float impactDelay = 0.5f;

    [Tooltip("Sát thương gốc")]
    [SerializeField] private int baseDamage = 100;

    [Tooltip("Hệ số nhân sát thương từ Hắc Hoá (ví dụ 1 Hắc Hoá = +10 sát thương)")]
    [SerializeField] private float darknessDamageMultiplier = 10f;

    [Tooltip("Bán kính sát thương khi gươm cắm xuống")]
    [SerializeField] private float hitRadius = 1.5f;

    [Tooltip("Layer của người chơi để check va chạm")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip castSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    // Performs startup initialization for JudgmentSwordSkill on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (castSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(JudgmentRoutine());
    }

    // Executes judgment routine operation.
    private IEnumerator JudgmentRoutine()
    {
        yield return new WaitForSeconds(impactDelay);

        Collider2D[] hits = EnemySkillVisualReplica.IsReplica(this)
            ? System.Array.Empty<Collider2D>()
            : Physics2D.OverlapCircleAll(transform.position, hitRadius, playerLayer);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                if (hitSound != null && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySfx(hitSound, soundVolume);
                }

                float currentDarkness = 0f;
                if (GameStateService.Instance != null)
                {
                    currentDarkness = GameStateService.Instance.CorruptionLevel;
                }

                int totalDamage = baseDamage + Mathf.RoundToInt(currentDarkness * darknessDamageMultiplier);

                PlayerEntity entity = hit.GetComponent<PlayerEntity>();
                if (entity != null)
                {
                    entity.TakeDamage(totalDamage);
                    Debug.Log($"[JudgmentSword] Chém trúng! Sát thương gốc {baseDamage} + Hắc Hoá {Mathf.RoundToInt(currentDarkness * darknessDamageMultiplier)} = {totalDamage} Damage");
                }
            }
        }

        yield return new WaitForSeconds(1f);
        Destroy(gameObject);
    }

    // Executes on draw gizmos selected operation.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
