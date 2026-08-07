using UnityEngine;

/// <summary>
/// Slime Mini Trap Skill - Bẫy Slime Mini do Boss SwampDemon triệu hồi.
/// Đứng yên tại vị trí triệu hồi. Khi Player giẫm phải:
/// 1. Kích hoạt hiệu ứng trói/khóa chân Player (ngăn không cho di chuyển).
/// 2. Chuyển animation Attack/Die và tự hủy ngay lập tức.
/// </summary>
public class SlimeMiniSkill : MonoBehaviour
{
    [Header("Trap Settings")]
    [Tooltip("Thời gian trói/khóa chân người chơi khi giẫm phải (giây)")]
    [SerializeField] private float rootDuration = 0.5f;

    [Tooltip("Thời gian khóa kỹ năng / cấm đánh thường khi giẫm phải (giây)")]
    [SerializeField] private float silenceDuration = 1.0f;

    [Tooltip("Sát thương gây ra cho người chơi khi giẫm phải (0 nếu chỉ muốn trói chân)")]
    [SerializeField] private int damageOnStep = 10;

    [Tooltip("Thời gian tồn tại tối đa của bẫy nếu người chơi không giẫm phải (giây)")]
    [SerializeField] private float maxLifeTime = 12f;

    [Tooltip("Thời gian chờ animation Die/Attack chạy xong trước khi Destroy (giây)")]
    [SerializeField] private float destroyDelay = 0.3f;

    [Header("Animation States")]
    [SerializeField] private string attackAnimState = "Attack";
    [SerializeField] private string dieAnimState = "Die";

    [Header("Audio Settings")]
    [SerializeField] private AudioClip triggerSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private bool _isTriggered = false;
    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // Tự động biến mất sau thời gian tối đa nếu không ai giẫm phải
        Destroy(gameObject, maxLifeTime);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (_isTriggered) return;

        if (col.CompareTag("Player"))
        {
            _isTriggered = true;

            PlayerCombat playerCombat = col.GetComponent<PlayerCombat>();
            BuffManager buffMgr = col.GetComponent<BuffManager>();
            bool isImmune = (playerCombat != null && playerCombat.IsDebuffImmune) || (buffMgr != null && buffMgr.IsStatusImmune);

            if (isImmune)
            {
                if (DamagePopupManager.Instance != null)
                {
                    DamagePopupManager.Instance.CreateText(col.transform.position, "Immunity", Color.cyan);
                }
                return;
            }

            // 1. Áp dụng hiệu ứng Khóa chân (Root) cho Player
            PlayerMovement playerMovement = col.GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                playerMovement.ApplyRoot(rootDuration);
            }

            // 2. Áp dụng hiệu ứng Cấm đánh & Khóa kỹ năng (Silence) cho Player
            if (playerCombat != null)
            {
                playerCombat.ApplySilence(silenceDuration);
            }

            // Đồng thời áp dụng SlimeDebuff để tạo icon/DoT nếu cần
            SlimeDebuff.ApplyTo(col.gameObject, 0f, damageOnStep, 1f, rootDuration);

            // Gây sát thương tức thì nếu có
            if (damageOnStep > 0)
            {
                PlayerEntity playerEntity = col.GetComponent<PlayerEntity>();
                if (playerEntity != null)
                {
                    playerEntity.TakeDamage(damageOnStep);
                }
            }

            // 2. Phát âm thanh
            if (triggerSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
            {
                MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(triggerSound, soundVolume);
            }

            // 3. Chạy animation Attack / Die
            if (_animator != null)
            {
                if (!string.IsNullOrEmpty(dieAnimState))
                {
                    _animator.Play(dieAnimState);
                }
                else if (!string.IsNullOrEmpty(attackAnimState))
                {
                    _animator.Play(attackAnimState);
                }
            }

            // 4. Hủy bẫy SlimeMini ngay lập tức sau delay ngắn
            Destroy(gameObject, destroyDelay);
        }
    }
}
