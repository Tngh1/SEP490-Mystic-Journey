using System.Collections;
using UnityEngine;
using MysticJourney.Core.Services;

// Executes mono behaviour operation.
public class DarknessCurseSkill : MonoBehaviour
{
    [Header("Curse Settings")]
    [Tooltip("Thời gian tồn tại của Lời nguyền (giây)")]
    [SerializeField] private float duration = 10f;

    [Tooltip("Sát thương mỗi giây")]
    [SerializeField] private int damagePerSecond = 5;

    [Tooltip("Tỷ lệ làm chậm (0.5 = giảm 50% tốc độ)")]
    [SerializeField] private float slowMultiplier = 0.5f;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip castSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private PlayerEntity targetEntity;
    private PlayerMovement targetMovement;

    // Performs startup initialization for DarknessCurseSkill on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (EnemySkillVisualReplica.IsReplica(this))
        {
            Destroy(gameObject, duration);
            return;
        }

        if (castSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var combat = player.GetComponent<PlayerCombat>();
            var buffMgr = player.GetComponent<BuffManager>();
            if ((combat != null && combat.IsDebuffImmune) || (buffMgr != null && buffMgr.IsStatusImmune))
            {
                if (DamagePopupManager.Instance != null)
                {
                    DamagePopupManager.Instance.CreateText(player.transform.position, "Immunity", Color.cyan);
                }
                Destroy(gameObject);
                return;
            }

            transform.SetParent(player.transform);
            transform.localPosition = Vector3.zero;

            targetEntity = player.GetComponent<PlayerEntity>();
            targetMovement = player.GetComponent<PlayerMovement>();

            if (targetMovement != null)
            {
                targetMovement.SetMoveSpeedOverride(targetMovement.CurrentMoveSpeed * slowMultiplier);
            }

            if (buffMgr != null) buffMgr.AddBuff("Darkness Curse", "curse_icon", duration, true);
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(CurseRoutine());

            Destroy(gameObject, duration);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Executes curse routine operation.
    private IEnumerator CurseRoutine()
    {
        float timer = 0f;
        while (timer < duration)
        {
            yield return new WaitForSeconds(1f);
            timer += 1f;

            if (targetEntity != null)
            {
                var combat = targetEntity.GetComponent<PlayerCombat>();
                if (combat != null && combat.IsDebuffImmune)
                {
                    Destroy(gameObject);
                    yield break;
                }
                targetEntity.TakeDamage(damagePerSecond);
            }

            var playerCombat = targetEntity != null ? targetEntity.GetComponent<PlayerCombat>() : null;
            if (playerCombat != null)
            {
                playerCombat.ApplyCorruptionDelta(1f);
                Debug.Log($"[DarknessCurse] Chỉ số Hắc Hoá bị tăng lên: {GameStateService.Instance.CorruptionLevel}");
            }
        }
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (targetMovement != null)
        {
            targetMovement.SetMoveSpeedOverride(0f);
        }
    }
}
