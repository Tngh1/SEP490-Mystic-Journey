using System.Collections;
using UnityEngine;
using MysticJourney.Core.Services;

public class DarknessCurseSkill : MonoBehaviour
{
    [Header("Curse Settings")]
    [Tooltip("Thời gian tồn tại của Lời nguyền (giây)")]
    [SerializeField] private float duration = 10f;
    
    [Tooltip("Sát thương mỗi giây")]
    [SerializeField] private int damagePerSecond = 5;
    
    [Tooltip("Tỷ lệ làm chậm (0.5 = giảm 50% tốc độ)")]
    [SerializeField] private float slowMultiplier = 0.5f;

    private PlayerEntity targetEntity;
    private PlayerMovement targetMovement;

    private void Start()
    {
        // 1. Tìm người chơi để bám vào
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Bám chặt vào người chơi
            transform.SetParent(player.transform);
            transform.localPosition = Vector3.zero; // Nằm ngay chính giữa

            // Lấy các component cần thiết
            targetEntity = player.GetComponent<PlayerEntity>();
            targetMovement = player.GetComponent<PlayerMovement>();

            // Gây hiệu ứng làm chậm
            if (targetMovement != null)
            {
                targetMovement.SetMoveSpeedOverride(targetMovement.CurrentMoveSpeed * slowMultiplier);
            }

            // Bắt đầu chu kỳ gây sát thương và cộng dồn hắc hoá
            StartCoroutine(CurseRoutine());

            // Tự động huỷ sau thời gian duration
            Destroy(gameObject, duration);
        }
        else
        {
            // Nếu không tìm thấy player thì tự hủy luôn
            Destroy(gameObject);
        }
    }

    private IEnumerator CurseRoutine()
    {
        float timer = 0f;
        while (timer < duration)
        {
            // Đợi 1 giây
            yield return new WaitForSeconds(1f);
            timer += 1f;

            // Trừ máu
            if (targetEntity != null)
            {
                targetEntity.TakeDamage(damagePerSecond);
            }

            // Tăng 1 chỉ số Hắc Hoá vào Profile toàn cục
            if (GameStateService.Instance != null)
            {
                GameStateService.Instance.CorruptionLevel += 1f;
                Debug.Log($"[DarknessCurse] Chỉ số Hắc Hoá bị tăng lên: {GameStateService.Instance.CorruptionLevel}");
            }
        }
    }

    private void OnDestroy()
    {
        // Khi skill kết thúc (hoặc Boss chết -> skill biến mất), trả lại tốc độ bình thường
        if (targetMovement != null)
        {
            targetMovement.SetMoveSpeedOverride(0f);
        }
    }
}
