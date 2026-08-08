using UnityEngine;
using UnityEngine.AI;
using EnemyPatrol.Utilites;
using System;
using System.Collections;

public class EnemyBehaviour : MonoBehaviour
{
    [SerializeField] private State startingState;
    [SerializeField] private float roamingDistanceMax = 7f;
    [SerializeField] private float roamingDistanceMin = 3f;
    [SerializeField] private float roamingTimeMax = 2f;
    [SerializeField] private bool isChasingEnemy = false;
    [SerializeField] private bool isAttackingEnemy = false;
    // Thêm biến này lên đầu class
    [SerializeField] private int attackDamage = 10;

    // Crit của quái, nhận từ Monster table qua UpdateStatsFromAPI.
    // 0 = không bao giờ crit (giữ nguyên hành vi cũ cho prefab chưa gắn MonsterId).
    [SerializeField] private int critRate = 0;
    [SerializeField] private float critDamageMultiplier = 1.5f;
    private NavMeshAgent navMeshAgent;
    private State currentState;
    private Vector3 roamPosition;
    private Vector3 startingPosition;

    [SerializeField] private float chasingDistance = 10f;
    [SerializeField] private float chasingSpeedMultiplier = 2f;
    [SerializeField] private float attackDistance = 2f;
    [SerializeField] private float attackRate = 2f;
    [SerializeField] private float leashDistance = 15f; // Quãng đường tối đa đi xa khỏi nhà
    private bool isReturning = false; // Trạng thái đang quay về

    [Header("Aggro & Range Settings")]
    [Tooltip("Nếu tích chọn, quái có tầm đánh xa. Nếu không tích chọn, quái cận chiến có tầm đánh rất gần (~1.4m).")]
    [SerializeField] private bool isRanged = false;
    [Tooltip("Độ cao hiển thị dấu chấm cảm trên đầu quái (mét).")]
    [SerializeField] private float iconHeightOffset = 1.8f;

    [Header("Out of Combat Health Regeneration")]
    [Tooltip("Tỷ lệ % máu hồi phục mỗi giây khi rời khỏi giao tranh hoặc quay về điểm xuất phát.")]
    [SerializeField] private float healthRegenPercentPerSecond = 20f;
    private float regenAccumulator = 0f;

    [Header("Ranged Attack / Projectile Settings")]
    [Tooltip("Nếu tích chọn hoặc có gán attackProjectilePrefab, quái sẽ bắn chiêu thay vì gây sát thương trực tiếp.")]
    [SerializeField] private bool useProjectileAttack = false;
    [Tooltip("Prefab hiệu ứng chiêu đánh thường (mũi tên, cầu lửa...). Nếu để trống sẽ tự tạo hiệu ứng đạn bay.")]
    [SerializeField] private GameObject attackProjectilePrefab;
    [Tooltip("Điểm xuất phát của chiêu đánh thường (nếu trống sẽ lấy tâm quái).")]
    [SerializeField] private Transform projectileSpawnPoint;
    [Tooltip("Tốc độ bay của chiêu đánh thường (mét/giây).")]
    [SerializeField] private float projectileSpeed = 8f;
    [Tooltip("Thời gian chờ phát chiêu để khớp với Animation đánh (giây).")]
    [SerializeField] private float attackDelay = 0.2f;

    [Header("Skill Settings")]
    [SerializeField] private bool canCastSkill = false;
    [SerializeField] private GameObject skillPrefab;
    [SerializeField] private float skillCooldown = 7f;
    [SerializeField] private float skillSpawnDelay = 0.5f; // Thời gian chờ để khớp với animation
    [SerializeField] private Transform skillSpawnPoint;
    private float nextSkillTime = 0f;

    [Header("Skill 2 Settings (Boss Only)")]
    [SerializeField] private bool canCastSkill2 = false;
    [SerializeField] private GameObject skill2Prefab;
    [SerializeField] private float skill2Cooldown = 20f;
    [SerializeField] private float skill2SpawnDelay = 0.5f;
    private float nextSkill2Time = 0f;

    private EnemyEntity _enemyEntity;
    private GameObject aggroIcon;
    private TextMesh aggroTextMesh;

    private float chasingSpeed;
    private float nextAttackTime = 1f;
    private float roamingSpeed;
    private float roamingTime;

    public event EventHandler OnEnemyAttack;
    public event EventHandler OnEnemyCastSkill;

    private float nextCheckDirectionTime = 0f;
    private float checkDirectionDuration = 0.1f;
    private Vector3 lastPosition;

    // Resolve một lần mỗi frame trong Update, dùng chung cho mọi bước xử lý của frame đó.
    private Transform currentTarget;

    private enum State
    {
        Idle,
        Roaming,
        Chasing,
        Attack,
        Death
    }

    private void Start()
    {
        startingPosition = transform.position;
        nextSkillTime = Time.time + skillCooldown;
        nextSkill2Time = Time.time + skill2Cooldown;

        // Bật mặc định cơ chế tự động đuổi và đánh người chơi
        isChasingEnemy = true;
        isAttackingEnemy = true;

        // CHỈ các quái trong ảnh chụp (SkeletonArcher, BlueDragonFrost, Dragon, GreenDragonForest, Ice_Dragon)
        // hoặc khi dev cố tình tích chọn isRanged / useProjectileAttack / gán attackProjectilePrefab trên Inspector mới là quái đánh xa.
        bool rangedMonster = IsSpecificRangedMonster();

        if (rangedMonster)
        {
            isRanged = true;
            useProjectileAttack = true;
            if (attackDistance <= 2.2f)
            {
                attackDistance = 6.0f; // Gán tầm đánh xa 6m cho đúng quái đánh xa
            }
        }
        else
        {
            // Tất cả các quái khác (Golem, Slime, Demon, Ghost, Imp, Orc, SkeletonMelee, v.v.) ĐỀU LÀ CẬN CHIẾN
            isRanged = false;
            useProjectileAttack = false;
            attackDistance = 1.4f; // Tầm đánh rất gần cho quái cận chiến
        }

        CreateAggroIcon();
    }

    private bool IsSpecificRangedMonster()
    {
        if (isRanged || useProjectileAttack || attackProjectilePrefab != null)
            return true;

        string nameClean = gameObject.name.Replace("(Clone)", "").Replace(" ", "").Trim();

        // Danh sách chính xác các quái đánh xa theo ảnh người dùng cung cấp:
        if (nameClean.Equals("SkeletonArcher", StringComparison.OrdinalIgnoreCase) ||
            nameClean.Equals("BlueDragonFrost", StringComparison.OrdinalIgnoreCase) ||
            nameClean.Equals("Dragon", StringComparison.OrdinalIgnoreCase) ||
            nameClean.Equals("GreenDragonForest", StringComparison.OrdinalIgnoreCase) ||
            nameClean.Equals("Ice_Dragon", StringComparison.OrdinalIgnoreCase) ||
            nameClean.Equals("IceDragon", StringComparison.OrdinalIgnoreCase) ||
            nameClean.Equals("OrcSkeletonArcher", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        _enemyEntity = GetComponent<EnemyEntity>();

        if (navMeshAgent == null)
        {
            enabled = false;
            Debug.LogWarning($"[EnemyBehaviour] Missing NavMeshAgent on {name}.");
            return;
        }

        navMeshAgent.updateRotation = false;
        navMeshAgent.updateUpAxis = false;
        currentState = startingState;
        roamingSpeed = navMeshAgent.speed;
        chasingSpeed = navMeshAgent.speed * chasingSpeedMultiplier;
    }

    private void OnEnable()
    {
        if (_enemyEntity != null)
        {
            _enemyEntity.OnTakeHit -= HandleTakeHit;
            _enemyEntity.OnTakeHit += HandleTakeHit;
        }
    }

    private void OnDisable()
    {
        if (_enemyEntity != null)
        {
            _enemyEntity.OnTakeHit -= HandleTakeHit;
        }
    }

    private void OnDestroy()
    {
        if (_enemyEntity != null)
        {
            _enemyEntity.OnTakeHit -= HandleTakeHit;
        }
    }

    /// <summary>
    /// Xử lý khi quái bị người chơi/kẻ địch tấn công -> Tự động quay sang truy đuổi người tấn công
    /// </summary>
    private void HandleTakeHit(object sender, EventArgs e)
    {
        if (currentState == State.Death) return;

        isChasingEnemy = true;
        isReturning = false;

        Transform nearestPlayer = FindNearestPlayer();
        if (nearestPlayer != null)
        {
            currentTarget = nearestPlayer;
        }

        if (currentState != State.Attack)
        {
            currentState = State.Chasing;
            if (CanUseNavMeshAgent())
            {
                navMeshAgent.ResetPath();
                navMeshAgent.speed = chasingSpeed;
                if (currentTarget != null)
                {
                    navMeshAgent.SetDestination(currentTarget.position);
                }
            }
        }

        UpdateAggroIcon();
    }

    private void CreateAggroIcon()
    {
        if (aggroIcon != null) return;

        aggroIcon = new GameObject("AggroExclamationIcon");
        aggroIcon.transform.SetParent(transform, false);
        aggroIcon.transform.localPosition = new Vector3(0f, iconHeightOffset, 0f);

        aggroTextMesh = aggroIcon.AddComponent<TextMesh>();
        aggroTextMesh.text = "!";
        aggroTextMesh.fontSize = 54;
        aggroTextMesh.characterSize = 0.12f;
        aggroTextMesh.fontStyle = FontStyle.Bold;
        aggroTextMesh.alignment = TextAlignment.Center;
        aggroTextMesh.anchor = TextAnchor.MiddleCenter;
        aggroTextMesh.color = new Color(1.0f, 0.15f, 0.15f); // Đỏ rực

        MeshRenderer mr = aggroIcon.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 30000; // Đảm bảo luôn hiển thị trên cùng (đè lên sprite quái)
        }

        aggroIcon.SetActive(false);
    }

    private void UpdateAggroIcon()
    {
        if (aggroIcon == null) CreateAggroIcon();

        bool showIcon = false;
        if (currentState != State.Death && currentTarget != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, currentTarget.position);
            // Hiện dấu ! khi người chơi tiến vào vùng đuổi / tấn công hoặc quái đang ở trạng thái Chasing / Attack
            bool inRange = distanceToPlayer <= chasingDistance || distanceToPlayer <= attackDistance;
            bool inCombatState = currentState == State.Chasing || currentState == State.Attack;
            showIcon = inRange || inCombatState;
        }

        if (aggroIcon != null && aggroIcon.activeSelf != showIcon)
        {
            aggroIcon.SetActive(showIcon);
        }
    }

    public void UpdateStatsFromAPI(int apiAttack, float apiMoveSpeed, int apiCritRate = 0, int apiCritDamage = 0)
    {
        attackDamage = apiAttack;

        critRate = Mathf.Clamp(apiCritRate, 0, 100);
        critDamageMultiplier = Mathf.Max(100, apiCritDamage) / 100f;

        if (navMeshAgent != null && apiMoveSpeed > 0)
        {
            float calculatedSpeed = (apiMoveSpeed / 100f) * 3.5f;
            navMeshAgent.speed = calculatedSpeed;
            roamingSpeed = calculatedSpeed;
            chasingSpeed = calculatedSpeed * chasingSpeedMultiplier;
        }
    }

    private void Update()
    {
        currentTarget = FindNearestPlayer();

        StateController();
        MovementDirection();
        CheckSkillCasting();
        UpdateAggroIcon();
        RegenerateHealthOutOfCombat();
    }

    private void RegenerateHealthOutOfCombat()
    {
        if (_enemyEntity == null || _enemyEntity.IsDead || currentState == State.Death)
        {
            regenAccumulator = 0f;
            return;
        }

        // Nếu quái đã đầy máu thì không cần hồi
        if (_enemyEntity.CurrentHealth >= _enemyEntity.MaxHealth)
        {
            regenAccumulator = 0f;
            return;
        }

        // Quái thoát giao tranh khi: đang đi về (isReturning), không có player, hoặc player nằm ngoài bán kính đuổi (chasingDistance)
        bool isOutOfCombat = isReturning || currentTarget == null;
        if (!isOutOfCombat && currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            isOutOfCombat = dist > chasingDistance || (currentState != State.Chasing && currentState != State.Attack);
        }

        if (isOutOfCombat)
        {
            float healPerSec = _enemyEntity.MaxHealth * (healthRegenPercentPerSecond / 100f);
            regenAccumulator += healPerSec * Time.deltaTime;
            if (regenAccumulator >= 1f)
            {
                int healAmount = Mathf.FloorToInt(regenAccumulator);
                regenAccumulator -= healAmount;
                _enemyEntity.Heal(healAmount);
            }
        }
        else
        {
            regenAccumulator = 0f;
        }
    }

    public void SetDeathState()
    {
        if (CanUseNavMeshAgent())
            navMeshAgent.ResetPath();

        currentState = State.Death;
        if (aggroIcon != null) aggroIcon.SetActive(false);
    }

    private void StateController()
    {
        switch (currentState)
        {
            default:
            case State.Idle:
                CheckCurrentState();
                break;
            case State.Roaming:
                roamingTime -= Time.deltaTime;
                if (roamingTime < 0)
                {
                    Roaming();
                    roamingTime = roamingTimeMax;
                }
                CheckCurrentState();
                break;
            case State.Chasing:
                ChasingTarget();
                CheckCurrentState();
                break;
            case State.Attack:
                AttackingTarget();
                CheckCurrentState();
                break;
            case State.Death:
                break;
        }
    }

    private void ChasingTarget()
    {
        if (currentTarget == null || !CanUseNavMeshAgent())
            return;

        navMeshAgent.SetDestination(currentTarget.position);
    }

    public float GetRoamingAnimationSpeed()
    {
        if (roamingSpeed <= 0f || navMeshAgent == null)
            return 0f;

        return navMeshAgent.speed / roamingSpeed;
    }

    private void CheckCurrentState()
    {
        // 1. KIỂM TRA PHẠM VI (LEASH)
        float distanceFromStart = Vector3.Distance(transform.position, startingPosition);

        if (isReturning)
        {
            // Nếu đã về tới gần điểm xuất phát -> Hủy trạng thái đi về, sinh hoạt bình thường
            if (distanceFromStart <= roamingDistanceMax + 1f)
            {
                isReturning = false;
            }
            else
            {
                // Bắt buộc ở trạng thái Roaming để đi về nhà
                if (currentState != State.Roaming)
                {
                    currentState = State.Roaming;
                    roamingTime = 0f; // Kích hoạt đi dạo ngay lập tức để tìm đường về
                    if (CanUseNavMeshAgent()) navMeshAgent.speed = roamingSpeed;
                }
                return; // Ngăn chặn code đuổi theo người chơi bên dưới
            }
        }
        else if (leashDistance > 0 && distanceFromStart > leashDistance)
        {
            // Đi quá xa -> Bật trạng thái quay về
            isReturning = true;
            if (currentState != State.Roaming)
            {
                currentState = State.Roaming;
                roamingTime = 0f;
                if (CanUseNavMeshAgent()) navMeshAgent.speed = roamingSpeed;
            }
            return;
        }

        // 2. CHECK ĐUỔI VÀ ĐÁNH (Như cũ)
        if (currentTarget == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, currentTarget.position);
        State newState = startingState;

        if (isChasingEnemy)
        {
            if (distanceToPlayer <= chasingDistance)
            {
                newState = State.Chasing;
            }
        }

        if (isAttackingEnemy)
        {
            if (distanceToPlayer <= attackDistance)
            {
                newState = State.Attack;
            }
        }

        if (newState != currentState)
        {
            if (newState == State.Chasing)
            {
                if (CanUseNavMeshAgent())
                {
                    navMeshAgent.ResetPath();
                    navMeshAgent.speed = chasingSpeed;
                }
            }
            else if (newState == State.Roaming)
            {
                roamingTime = 0f;
                if (navMeshAgent != null)
                    navMeshAgent.speed = roamingSpeed;
            }
            else if (newState == State.Attack)
            {
                if (CanUseNavMeshAgent())
                    navMeshAgent.ResetPath();
            }
            else if (newState == State.Idle)
            {
                if (CanUseNavMeshAgent())
                    navMeshAgent.ResetPath();
            }

            currentState = newState;
        }
    }

    public bool IsRunning
    {
        get
        {
            if (navMeshAgent == null) return false;
            
            bool isMoving = navMeshAgent.velocity.magnitude > 0.05f;
            bool hasPath = (navMeshAgent.hasPath && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance) || navMeshAgent.pathPending;
            
            return isMoving || hasPath;
        }
    }

    private void AttackingTarget()
    {
        if (Time.time > nextAttackTime)
        {
            // Báo hiệu quái đang tấn công (để bật Animation)
            OnEnemyAttack?.Invoke(this, EventArgs.Empty);

            bool isCrit = critRate > 0 && UnityEngine.Random.Range(0f, 100f) <= critRate;

            // Chỉ bắn chiêu đạn bay nếu là quái thuộc danh sách đánh xa (SkeletonArcher, Dragon, v.v.)
            bool shouldShoot = isRanged && useProjectileAttack;

            if (shouldShoot && currentTarget != null)
            {
                // Quái đánh xa -> Sinh ra chiêu đạn bay về phía người chơi.
                // Sát thương KHÔNG gây ra ngay lập tức trên quái, mà tính khi chiêu đạn chạm vào người chơi!
                StartCoroutine(SpawnAttackProjectileRoutine(currentTarget, isCrit));
            }
            else
            {
                // Quái cận chiến -> Gây sát thương trực tiếp khi áp sát
                DirectMeleeDamage(isCrit);
            }

            nextAttackTime = Time.time + attackRate;
        }
    }

    private IEnumerator SpawnAttackProjectileRoutine(Transform target, bool isCrit)
    {
        if (attackDelay > 0f)
        {
            yield return new WaitForSeconds(attackDelay);
        }

        if (target == null || currentState == State.Death) yield break;

        Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : (transform.position + new Vector3(0f, 0.5f, 0f));
        Vector3 dir = (target.position - spawnPos).normalized;

        GameObject projObj;
        if (attackProjectilePrefab != null)
        {
            projObj = Instantiate(attackProjectilePrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Tự tạo hiệu ứng đạn bay nếu chưa gán prefab riêng trên Inspector
            projObj = new GameObject($"{gameObject.name}_Projectile");
            projObj.transform.position = spawnPos;

            var sr = projObj.AddComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0.6f, 0.1f);
            var col = projObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.25f;
        }

        var projLogic = projObj.GetComponent<EnemyNormalAttackProjectile>();
        if (projLogic == null)
        {
            projLogic = projObj.AddComponent<EnemyNormalAttackProjectile>();
        }

        projLogic.Setup(dir, projectileSpeed, attackDamage, isCrit, critDamageMultiplier);
    }

    private void DirectMeleeDamage(bool isCrit)
    {
        if (currentTarget != null)
        {
            var networkPlayer = currentTarget.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                int netDamage = isCrit ? Mathf.RoundToInt(attackDamage * critDamageMultiplier) : attackDamage;
                networkPlayer.RequestDamage(netDamage);
            }
            else
            {
                var playerEntity = currentTarget.GetComponent<PlayerEntity>();
                if (playerEntity != null)
                {
                    playerEntity.TakeDamage(attackDamage, isCrit, critDamageMultiplier);
                }
                else if (PlayerEntity.Instance != null)
                {
                    PlayerEntity.Instance.TakeDamage(attackDamage, isCrit, critDamageMultiplier);
                }
            }
        }
        else if (PlayerEntity.Instance != null)
        {
            PlayerEntity.Instance.TakeDamage(attackDamage, isCrit, critDamageMultiplier);
        }
    }

    private void MovementDirection()
    {
        if (Time.time > nextCheckDirectionTime)
        {
            if (IsRunning)
            {
                ChangeFaceDir(lastPosition, transform.position);
            }
            else if (currentState == State.Attack && currentTarget != null)
            {
                ChangeFaceDir(transform.position, currentTarget.position);
            }

            lastPosition = transform.position;
            nextCheckDirectionTime = Time.time + checkDirectionDuration;
        }
    }

    private void CheckSkillCasting()
    {
        // Chỉ thi triển skill khi Quái/Boss đang ở trạng thái Chasing (truy đuổi) hoặc Attack (tấn công)
        if (currentState != State.Chasing && currentState != State.Attack) return;
        if (currentTarget == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, currentTarget.position);

        if (distanceToPlayer <= chasingDistance)
        {
            // Kiểm tra Skill 2 trước (ưu tiên)
            if (canCastSkill2 && skill2Prefab != null && Time.time >= nextSkill2Time)
            {
                CastSkill(currentTarget, skill2Prefab, skill2SpawnDelay);
                nextSkill2Time = Time.time + skill2Cooldown;
                return; // Cast xong skill 2 thì dừng, không cast skill 1 cùng lúc
            }

            // Nếu không dùng skill 2, kiểm tra skill 1
            if (canCastSkill && skillPrefab != null && Time.time >= nextSkillTime)
            {
                CastSkill(currentTarget, skillPrefab, skillSpawnDelay);
                nextSkillTime = Time.time + skillCooldown;
            }
        }
    }

    private void CastSkill(Transform target, GameObject prefabToCast, float delay)
    {
        OnEnemyCastSkill?.Invoke(this, EventArgs.Empty);

        StartCoroutine(SpawnSkillWithDelay(target, prefabToCast, delay));
    }

    private IEnumerator SpawnSkillWithDelay(Transform target, GameObject prefabToCast, float delay)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }

        // Cần check lại xem target có bị null hoặc boss có bị tiêu diệt trong lúc delay không
        if (target == null || currentState == State.Death) yield break;

        Vector3 spawnPosition = target.position;
        if (skillSpawnPoint != null)
        {
            spawnPosition = skillSpawnPoint.position;
        }

        Instantiate(prefabToCast, spawnPosition, Quaternion.identity);
    }

    private void Roaming()
    {
        if (!CanUseNavMeshAgent())
            return;

        roamPosition = GetRoamingPosition();
        navMeshAgent.SetDestination(roamPosition);
    }

    private Vector3 GetRoamingPosition()
    {
        return startingPosition + Utilites.GetRandomDir() * UnityEngine.Random.Range(roamingDistanceMin, roamingDistanceMax);
    }

    private void ChangeFaceDir(Vector3 sourcePosition, Vector3 targetPosition)
    {
        if (sourcePosition.x > targetPosition.x)
        {
            transform.rotation = Quaternion.Euler(0, -180, 0);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }

    private Transform FindNearestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players == null || players.Length == 0)
        {
            if (PlayerMovement.Instance != null && PlayerMovement.Instance.gameObject.activeInHierarchy)
            {
                return PlayerMovement.Instance.transform;
            }
            if (PlayerEntity.Instance != null && PlayerEntity.Instance.gameObject.activeInHierarchy)
            {
                return PlayerEntity.Instance.transform;
            }
            return null;
        }

        Vector3 from = transform.position;
        Transform best = null;
        float bestSqr = float.MaxValue;
        
        foreach (var p in players)
        {
            if (p == null || !p.activeInHierarchy) continue;
            
            var networkPlayer = p.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                if (networkPlayer.Object == null || !networkPlayer.Object.IsValid) continue;
                if (!networkPlayer.IsAlive) continue;
            }

            float sqr = (p.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = p.transform;
            }
        }
        return best;
    }

    private bool CanUseNavMeshAgent()
    {
        return navMeshAgent != null &&
               navMeshAgent.enabled &&
               navMeshAgent.isActiveAndEnabled &&
               navMeshAgent.isOnNavMesh;
    }
}
