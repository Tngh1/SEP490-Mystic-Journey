using System.Collections;
using UnityEngine;

/// <summary>
/// Script AI thông minh hỗ trợ riêng cho Boss IceFairy.
/// Các tính năng chính:
/// 1. Bay ngẫu nhiên quanh tâm thân người GolemBoss khi bình thường.
/// 2. Khi Player tiến lại gần (tầm 8m): IceFairy chủ động bay nấp đằng sau GolemBoss 
///    (lấy GolemBoss làm bức tường che chắn các đòn đánh của Player).
/// 3. Tự động điều chỉnh Sorting Order (Order in Layer): Khi nấp phía sau GolemBoss, GolemBoss sẽ đè lên che mất IceFairy.
/// 4. ĐỨNG YÊN HOÀN TOÀN khi đang thi triển skill (Tấn công Player hoặc Hồi máu cho Boss).
/// 5. Cứ 5s/lần tấn công trực tiếp Player bằng chiêu Bụi Tiên (FairyDust Prefab) trong phạm vi 8m.
/// 6. Định kỳ hồi máu cho GolemBoss khi GolemBoss bị mất HP.
/// 7. Khi bị hạ gục -> Chuyển ngay sang Animation "Die" và tắt collider.
/// </summary>
public class IceFairySupportAI : MonoBehaviour
{
    [Header("Leader Settings (GolemBoss)")]
    [Tooltip("Tên Boss cần đi theo hỗ trợ")]
    [SerializeField] private string targetBossName = "GolemBoss";

    [Tooltip("Khoảng cách nấp phía sau GolemBoss (mét)")]
    [SerializeField] private float coverDistance = 2.5f;

    [Tooltip("Bán kính bay lượn ngẫu nhiên quanh GolemBoss (mét)")]
    [SerializeField] private float wanderRadius = 4.0f;

    [Tooltip("Tốc độ di chuyển của IceFairy")]
    [SerializeField] private float moveSpeed = 3.5f;

    [Tooltip("Độ lệch chiều cao tâm thân người GolemBoss so với vị trí dưới chân (mặc định Y = 1.5m)")]
    [SerializeField] private Vector3 bossCenterOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Attack Skill Settings (FairyDust)")]
    [Tooltip("Có bật kỹ năng tấn công Player không")]
    [SerializeField] private bool canAttackPlayer = true;

    [Tooltip("Tầm tấn công Player (mét)")]
    [SerializeField] private float attackRange = 8.0f;

    [Tooltip("Thời gian hồi chiêu tấn công (giây)")]
    [SerializeField] private float fairyDustCooldown = 5.0f;

    [Tooltip("Prefab kỹ năng tấn công (kéo Prefab FairyDust vào đây)")]
    [SerializeField] private GameObject fairyDustPrefab;

    [Header("Heal Support Settings (HealBoss)")]
    [Tooltip("Có bật kỹ năng hồi máu cho GolemBoss không")]
    [SerializeField] private bool canHealLeader = true;

    [Tooltip("Khoảng thời gian hồi chiêu hồi máu (giây)")]
    [SerializeField] private float healCooldown = 8.0f;

    [Tooltip("Prefab cột sáng hồi máu (kéo Prefab HealBoss vào đây)")]
    [SerializeField] private GameObject healBossPrefab;

    [Header("Animation Settings")]
    [Tooltip("Thời gian phát animation Attack trước khi quay về Move (giây)")]
    [SerializeField] private float attackAnimDuration = 0.5f;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip healSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private EnemyEntity _myEntity;
    private EnemyEntity _golemEntity;
    private Transform _golemTransform;
    private SpriteRenderer _golemSpriteRenderer;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;

    private float _nextAttackTime = 0f;
    private float _nextHealTime = 0f;
    private float _nextWanderTime = 0f;
    private Vector3 _wanderTargetPos;
    private bool _isCastingSkill = false;
    private bool _hasDied = false;

    private void Awake()
    {
        _myEntity = GetComponent<EnemyEntity>();
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (_myEntity != null)
        {
            _myEntity.OnDeath += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (_myEntity != null)
        {
            _myEntity.OnDeath -= HandleDeath;
        }
    }

    private void Start()
    {
        if (_myEntity != null)
        {
            _myEntity.OnDeath -= HandleDeath;
            _myEntity.OnDeath += HandleDeath;
        }

        FindGolemBoss();
        _nextAttackTime = Time.time + 2f;
        _nextHealTime = Time.time + 4f;
    }

    private void Update()
    {
        // Nếu IceFairy đã chết thì lập tức ngưng xử lý
        if (_hasDied || (_myEntity != null && _myEntity.IsDead))
        {
            if (!_hasDied) HandleDeath(this, System.EventArgs.Empty);
            return;
        }

        if (_golemTransform == null)
        {
            FindGolemBoss();
        }

        // Tự động điều chỉnh Sorting Order (Đứng sau GolemBoss sẽ bị GolemBoss che đè lên)
        UpdateSortingOrder();

        Transform playerTransform = FindPlayerTarget();

        // 1. Quản lý di chuyển (Bay ngẫu nhiên hoặc Nấp sau GolemBoss)
        HandleSmartMovement(playerTransform);

        // 2. Tấn công Player bằng FairyDust (mỗi 5s trong tầm 8m)
        HandleAttackSupport(playerTransform);

        // 3. Hồi máu cho GolemBoss nếu GolemBoss bị mất máu
        HandleHealSupport();
    }

    private void UpdateSortingOrder()
    {
        if (_spriteRenderer == null || _golemTransform == null) return;

        if (_golemSpriteRenderer == null && _golemEntity != null)
        {
            _golemSpriteRenderer = _golemEntity.GetComponentInChildren<SpriteRenderer>();
        }

        int golemOrder = _golemSpriteRenderer != null ? _golemSpriteRenderer.sortingOrder : 10;

        // Nếu IceFairy đứng phía sau GolemBoss (Y cao hơn GolemBoss) -> Sorting Order nhỏ hơn GolemBoss để GolemBoss che khuất IceFairy
        if (transform.position.y > _golemTransform.position.y + 0.1f)
        {
            _spriteRenderer.sortingOrder = golemOrder - 1;
        }
        else
        {
            _spriteRenderer.sortingOrder = golemOrder + 1;
        }
    }

    private void HandleDeath(object sender, System.EventArgs e)
    {
        if (_hasDied) return;
        _hasDied = true;
        _isCastingSkill = false;

        if (_animator != null)
        {
            _animator.Play("Die", 0, 0f);
            if (_animator.HasParameter("Died"))
            {
                _animator.SetBool("Died", true);
            }
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        this.enabled = false;
    }

    private Vector3 GetGolemCenterPos()
    {
        if (_golemTransform == null) return transform.position;
        Transform found = _golemTransform.Find("SpawnPoint") ?? _golemTransform.Find("SkillSpawn");
        if (found != null) return found.position;
        return _golemTransform.position + bossCenterOffset;
    }

    private void HandleSmartMovement(Transform playerTransform)
    {
        // Khi đang thi triển skill -> ĐỨNG YÊN HOÀN TOÀN không di chuyển!
        if (_isCastingSkill)
        {
            SetRunningAnim(false);
            return;
        }

        if (_golemTransform == null)
        {
            SetRunningAnim(false);
            return;
        }

        Vector3 golemCenterPos = GetGolemCenterPos();
        Vector3 targetPos;
        bool isPlayerInCombatRange = false;

        if (playerTransform != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            float golemDistToPlayer = Vector3.Distance(golemCenterPos, playerTransform.position);

            if (distToPlayer <= attackRange || golemDistToPlayer <= attackRange)
            {
                isPlayerInCombatRange = true;
            }
        }

        if (isPlayerInCombatRange && playerTransform != null)
        {
            // === CHẾ ĐỘ THÔNG MINH: Lấy GolemBoss làm bức tường che chắn ===
            Vector3 playerToGolemDir = (golemCenterPos - playerTransform.position).normalized;
            targetPos = golemCenterPos + playerToGolemDir * coverDistance;
        }
        else
        {
            // === CHẾ ĐỘ BÌNH THƯỜNG: Bay lượn ngẫu nhiên quanh tâm GolemBoss ===
            if (Time.time >= _nextWanderTime || Vector3.Distance(transform.position, _wanderTargetPos) < 0.3f)
            {
                _nextWanderTime = Time.time + Random.Range(2.5f, 4.0f);
                Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
                _wanderTargetPos = golemCenterPos + new Vector3(randomCircle.x, randomCircle.y, 0f);
            }
            targetPos = _wanderTargetPos;
        }

        // Di chuyển mượt mà tới targetPos
        Vector3 moveDir = (targetPos - transform.position);
        if (moveDir.magnitude > 0.15f)
        {
            Vector3 normDir = moveDir.normalized;
            transform.position += normDir * moveSpeed * Time.deltaTime;

            if (_spriteRenderer != null && Mathf.Abs(normDir.x) > 0.05f)
            {
                _spriteRenderer.flipX = normDir.x < 0;
            }

            SetRunningAnim(true);
        }
        else
        {
            SetRunningAnim(false);
        }
    }

    private void HandleAttackSupport(Transform playerTransform)
    {
        if (!canAttackPlayer || _isCastingSkill || playerTransform == null || fairyDustPrefab == null) return;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distToPlayer <= attackRange && Time.time >= _nextAttackTime)
        {
            _nextAttackTime = Time.time + fairyDustCooldown;
            StartCoroutine(PerformFairyDustAttackRoutine(playerTransform));
        }
    }

    private IEnumerator PerformFairyDustAttackRoutine(Transform playerTransform)
    {
        _isCastingSkill = true;
        SetRunningAnim(false);

        PlayAttackAnimation();

        yield return new WaitForSeconds(0.25f);

        if (playerTransform != null)
        {
            if (attackSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
            {
                MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(attackSound, soundVolume);
            }

            Instantiate(fairyDustPrefab, playerTransform.position, Quaternion.identity);
        }

        float remainingAnimTime = Mathf.Max(0.1f, attackAnimDuration - 0.25f);
        yield return new WaitForSeconds(remainingAnimTime);

        _isCastingSkill = false;
        ReturnToMoveAnimation();
    }

    private void HandleHealSupport()
    {
        if (!canHealLeader || _isCastingSkill || _golemEntity == null || _golemEntity.IsDead) return;

        if (_golemEntity.CurrentHealth < _golemEntity.MaxHealth && Time.time >= _nextHealTime)
        {
            _nextHealTime = Time.time + healCooldown;
            StartCoroutine(PerformHealRoutine());
        }
    }

    private IEnumerator PerformHealRoutine()
    {
        _isCastingSkill = true;
        SetRunningAnim(false);

        PlayAttackAnimation();

        yield return new WaitForSeconds(0.25f);

        if (_golemEntity != null && !_golemEntity.IsDead)
        {
            if (healSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
            {
                MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(healSound, soundVolume);
            }

            if (healBossPrefab != null)
            {
                Instantiate(healBossPrefab, GetGolemCenterPos(), Quaternion.identity);
            }
        }

        float remainingAnimTime = Mathf.Max(0.1f, attackAnimDuration - 0.25f);
        yield return new WaitForSeconds(remainingAnimTime);

        _isCastingSkill = false;
        ReturnToMoveAnimation();
    }

    private void PlayAttackAnimation()
    {
        if (_animator == null) return;

        _animator.Play("Attack", 0, 0f);

        if (_animator.HasParameter("EnemyAttack")) _animator.SetTrigger("EnemyAttack");
        if (_animator.HasParameter("Attack")) _animator.SetTrigger("Attack");
        if (_animator.HasParameter("CastSkill")) _animator.SetTrigger("CastSkill");
    }

    private void ReturnToMoveAnimation()
    {
        if (_animator != null)
        {
            _animator.Play("Move", 0, 0f);
            SetRunningAnim(true);
        }
    }

    private void SetRunningAnim(bool isRunning)
    {
        if (_animator != null && !_isCastingSkill)
        {
            if (_animator.HasParameter("Move"))
            {
                _animator.SetBool("Move", isRunning);
            }
            else if (_animator.HasParameter("IsRunning"))
            {
                _animator.SetBool("IsRunning", isRunning);
            }
        }
    }

    private Transform FindPlayerTarget()
    {
        if (PlayerMovement.Instance != null)
        {
            return PlayerMovement.Instance.transform;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private void FindGolemBoss()
    {
        EnemyEntity[] enemies = FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.gameObject != this.gameObject && enemy.gameObject.name.Contains(targetBossName))
            {
                _golemEntity = enemy;
                _golemTransform = enemy.transform;
                _golemSpriteRenderer = enemy.GetComponentInChildren<SpriteRenderer>();
                break;
            }
        }
    }
}
