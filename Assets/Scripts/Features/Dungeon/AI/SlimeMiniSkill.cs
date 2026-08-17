using UnityEngine;

// Executes mono behaviour operation.
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

    // Initializes internal component caches and dependencies for SlimeMiniSkill upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    // Performs startup initialization for SlimeMiniSkill on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        Destroy(gameObject, maxLifeTime);
    }

    // Executes on trigger enter2 d operation.
    // Validates input parameters against null or empty values.
    private void OnTriggerEnter2D(Collider2D col)
    {
        if (EnemySkillVisualReplica.IsReplica(this)) return;
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

            PlayerMovement playerMovement = col.GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                playerMovement.ApplyRoot(rootDuration);
            }

            if (playerCombat != null)
            {
                playerCombat.ApplySilence(silenceDuration);
            }

            SlimeDebuff.ApplyTo(col.gameObject, 0f, damageOnStep, 1f, rootDuration);

            if (damageOnStep > 0)
            {
                PlayerEntity playerEntity = col.GetComponent<PlayerEntity>();
                if (playerEntity != null)
                {
                    playerEntity.TakeDamage(damageOnStep);
                }
            }

            if (triggerSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
            {
                MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(triggerSound, soundVolume);
            }

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

            Destroy(gameObject, destroyDelay);
        }
    }
}
