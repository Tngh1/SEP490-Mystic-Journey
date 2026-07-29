using System.Collections;
using UnityEngine;
using MysticJourney.Core.Services;

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

    private void Start()
    {
        if (castSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        // Khi vừa sinh ra, bắt đầu quá trình giáng đòn
        StartCoroutine(JudgmentRoutine());
    }

    private IEnumerator JudgmentRoutine()
    {
        // 1. Chờ hoạt ảnh kiếm rơi xuống (0.5s)
        yield return new WaitForSeconds(impactDelay);

        // 2. Chém xuống, quét xem người chơi có còn đứng trong vùng ảnh hưởng không
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hitRadius, playerLayer);
        
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                if (hitSound != null && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySfx(hitSound, soundVolume);
                }

                // Lấy chỉ số Hắc hoá từ API/GameState toàn cục
                float currentDarkness = 0f;
                if (GameStateService.Instance != null)
                {
                    currentDarkness = GameStateService.Instance.CorruptionLevel;
                }

                // Tính toán sát thương
                int totalDamage = baseDamage + Mathf.RoundToInt(currentDarkness * darknessDamageMultiplier);

                // Gây sát thương
                PlayerEntity entity = hit.GetComponent<PlayerEntity>();
                if (entity != null)
                {
                    entity.TakeDamage(totalDamage);
                    Debug.Log($"[JudgmentSword] Chém trúng! Sát thương gốc {baseDamage} + Hắc Hoá {Mathf.RoundToInt(currentDarkness * darknessDamageMultiplier)} = {totalDamage} Damage");
                }
            }
        }

        // 3. Chờ thêm 1 chút để hoạt ảnh biến mất rồi huỷ object
        yield return new WaitForSeconds(1f);
        Destroy(gameObject);
    }

    // Vẽ vòng tròn phạm vi chém trong Editor để dễ căn chỉnh
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
