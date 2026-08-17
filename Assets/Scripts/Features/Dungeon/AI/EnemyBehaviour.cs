using UnityEngine;
using UnityEngine.AI;
using EnemyPatrol.Utilites;
using System;
using System.Collections;

// Executes mono behaviour operation.
public class EnemyBehaviour : MonoBehaviour
{
    [SerializeField] private State startingState;
    [SerializeField] private float roamingDistanceMax = 7f;
    [SerializeField] private float roamingDistanceMin = 3f;
    [SerializeField] private float roamingTimeMax = 2f;
    [SerializeField] private bool isChasingEnemy = false;
    [SerializeField] private bool isAttackingEnemy = false;
    [SerializeField] private int attackDamage = 10;

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
    [SerializeField] private float leashDistance = 15f;
    private bool isReturning = false;

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
    [SerializeField] private float skillSpawnDelay = 0.5f;
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

    private Transform currentTarget;

    // Executes state operation.
    private enum State
    {
        Idle,
        Roaming,
        Chasing,
        Attack,
        Death
    }

    // Performs startup initialization for EnemyBehaviour on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        startingPosition = transform.position;
        nextSkillTime = Time.time + skillCooldown;
        nextSkill2Time = Time.time + skill2Cooldown;

        isChasingEnemy = true;
        isAttackingEnemy = true;

        bool rangedMonster = IsSpecificRangedMonster();

        if (rangedMonster)
        {
            isRanged = true;
            useProjectileAttack = true;
            if (attackDistance <= 2.2f)
            {
                attackDistance = 6.0f;
            }
        }
        else
        {
            isRanged = false;
            useProjectileAttack = false;
            attackDistance = 1.4f;
        }

        CreateAggroIcon();
    }

    // Executes is specific ranged monster operation.
    private bool IsSpecificRangedMonster()
    {
        if (isRanged || useProjectileAttack || attackProjectilePrefab != null)
            return true;

        string nameClean = gameObject.name.Replace("(Clone)", "").Replace(" ", "").Trim();

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

    // Initializes internal component caches and dependencies for EnemyBehaviour upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        _enemyEntity = GetComponent<EnemyEntity>();
        var networkEnemy = GetComponent<NetworkEnemy>();
        networkEnemy?.RegisterSkillPrefab(skillPrefab);
        networkEnemy?.RegisterSkillPrefab(skill2Prefab);
        networkEnemy?.RegisterSkillPrefab(attackProjectilePrefab);

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

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        if (_enemyEntity != null)
        {
            _enemyEntity.OnTakeHit -= HandleTakeHit;
            _enemyEntity.OnTakeHit += HandleTakeHit;
        }
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        if (_enemyEntity != null)
        {
            _enemyEntity.OnTakeHit -= HandleTakeHit;
        }
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (_enemyEntity != null)
        {
            _enemyEntity.OnTakeHit -= HandleTakeHit;
        }
    }

    // Executes handle take hit operation.
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

    // Executes create aggro icon operation.
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
        aggroTextMesh.color = new Color(1.0f, 0.15f, 0.15f);

        MeshRenderer mr = aggroIcon.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 30000;
        }

        aggroIcon.SetActive(false);
    }

    // Executes update aggro icon operation.
    private void UpdateAggroIcon()
    {
        if (aggroIcon == null) CreateAggroIcon();

        bool showIcon = false;
        if (currentState != State.Death && currentTarget != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, currentTarget.position);
            bool inRange = distanceToPlayer <= chasingDistance || distanceToPlayer <= attackDistance;
            bool inCombatState = currentState == State.Chasing || currentState == State.Attack;
            showIcon = inRange || inCombatState;
        }

        if (aggroIcon != null && aggroIcon.activeSelf != showIcon)
        {
            aggroIcon.SetActive(showIcon);
        }
    }

    // Executes update stats from api operation.
    public void UpdateStatsFromAPI(int apiAttack, float apiMoveSpeed, int apiCritRate = 0, int apiCritDamage = 0)
    {
        attackDamage = apiAttack;

        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
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

    // Per-frame update loop for EnemyBehaviour.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        currentTarget = FindNearestPlayer();

        StateController();
        MovementDirection();
        CheckSkillCasting();
        UpdateAggroIcon();
        RegenerateHealthOutOfCombat();
    }

    // Executes regenerate health out of combat operation.
    private void RegenerateHealthOutOfCombat()
    {
        if (_enemyEntity == null || _enemyEntity.IsDead || currentState == State.Death)
        {
            regenAccumulator = 0f;
            return;
        }

        if (_enemyEntity.CurrentHealth >= _enemyEntity.MaxHealth)
        {
            regenAccumulator = 0f;
            return;
        }

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

    // Executes set death state operation.
    public void SetDeathState()
    {
        if (CanUseNavMeshAgent())
            navMeshAgent.ResetPath();

        currentState = State.Death;
        if (aggroIcon != null) aggroIcon.SetActive(false);
    }

    // Executes state controller operation.
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

    // Executes chasing target operation.
    private void ChasingTarget()
    {
        if (currentTarget == null || !CanUseNavMeshAgent())
            return;

        navMeshAgent.SetDestination(currentTarget.position);
    }

    // Executes get roaming animation speed operation.
    public float GetRoamingAnimationSpeed()
    {
        if (roamingSpeed <= 0f || navMeshAgent == null)
            return 0f;

        return navMeshAgent.speed / roamingSpeed;
    }

    // Executes check current state operation.
    private void CheckCurrentState()
    {
        float distanceFromStart = Vector3.Distance(transform.position, startingPosition);

        if (isReturning)
        {
            if (distanceFromStart <= roamingDistanceMax + 1f)
            {
                isReturning = false;
            }
            else
            {
                if (currentState != State.Roaming)
                {
                    currentState = State.Roaming;
                    roamingTime = 0f;
                    if (CanUseNavMeshAgent()) navMeshAgent.speed = roamingSpeed;
                }
                return;
            }
        }
        else if (leashDistance > 0 && distanceFromStart > leashDistance)
        {
            isReturning = true;
            if (currentState != State.Roaming)
            {
                currentState = State.Roaming;
                roamingTime = 0f;
                if (CanUseNavMeshAgent()) navMeshAgent.speed = roamingSpeed;
            }
            return;
        }

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

    // Executes is running operation.
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

    // Executes attacking target operation.
    private void AttackingTarget()
    {
        if (Time.time > nextAttackTime)
        {
            OnEnemyAttack?.Invoke(this, EventArgs.Empty);
            GetComponent<NetworkEnemy>()?.NotifyAttackAnimation();

            // Randomize the eligible candidates before selecting this gameplay result.
            bool isCrit = critRate > 0 && UnityEngine.Random.Range(0f, 100f) <= critRate;

            bool shouldShoot = isRanged && useProjectileAttack;

            if (shouldShoot && currentTarget != null)
            {
                // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
                StartCoroutine(SpawnAttackProjectileRoutine(currentTarget, isCrit));
            }
            else
            {
                DirectMeleeDamage(isCrit);
            }

            nextAttackTime = Time.time + attackRate;
        }
    }

    // Executes spawn attack projectile routine operation.
    private IEnumerator SpawnAttackProjectileRoutine(Transform target, bool isCrit)
    {
        if (attackDelay > 0f)
        {
            yield return new WaitForSeconds(attackDelay);
        }

        if (target == null || currentState == State.Death) yield break;

        Vector3 spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : (transform.position + new Vector3(0f, 0.5f, 0f));
        Vector3 dir = (target.position - spawnPos).normalized;

        var networkEnemy = GetComponent<NetworkEnemy>();
        if (networkEnemy != null && networkEnemy.IsNetworkActive)
        {
            networkEnemy.SpawnEnemyProjectile(
                attackProjectilePrefab,
                spawnPos,
                dir,
                projectileSpeed,
                attackDamage,
                isCrit,
                critDamageMultiplier);
            yield break;
        }

        GameObject projObj;
        if (attackProjectilePrefab != null)
        {
            projObj = Instantiate(attackProjectilePrefab, spawnPos, Quaternion.identity);
        }
        else
        {
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

    // Executes direct melee damage operation.
    private void DirectMeleeDamage(bool isCrit)
    {
        if (currentTarget != null)
        {
            var networkPlayer = currentTarget.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                int netDamage = isCrit ? Mathf.RoundToInt(attackDamage * critDamageMultiplier) : attackDamage;
                networkPlayer.RequestDamage(netDamage, isCrit);
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

    // Executes movement direction operation.
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

    // Executes check skill casting operation.
    private void CheckSkillCasting()
    {
        if (currentState != State.Chasing && currentState != State.Attack) return;
        if (currentTarget == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, currentTarget.position);

        if (distanceToPlayer <= chasingDistance)
        {
            if (canCastSkill2 && skill2Prefab != null && Time.time >= nextSkill2Time)
            {
                CastSkill(currentTarget, skill2Prefab, skill2SpawnDelay);
                nextSkill2Time = Time.time + skill2Cooldown;
                return;
            }

            if (canCastSkill && skillPrefab != null && Time.time >= nextSkillTime)
            {
                CastSkill(currentTarget, skillPrefab, skillSpawnDelay);
                nextSkillTime = Time.time + skillCooldown;
            }
        }
    }

    // Executes cast skill operation.
    private void CastSkill(Transform target, GameObject prefabToCast, float delay)
    {
        OnEnemyCastSkill?.Invoke(this, EventArgs.Empty);
        GetComponent<NetworkEnemy>()?.NotifySkillAnimation();

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(SpawnSkillWithDelay(target, prefabToCast, delay));
    }

    // Executes spawn skill with delay operation.
    private IEnumerator SpawnSkillWithDelay(Transform target, GameObject prefabToCast, float delay)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }

        if (target == null || currentState == State.Death) yield break;

        Vector3 spawnPosition = target.position;
        if (skillSpawnPoint != null)
        {
            spawnPosition = skillSpawnPoint.position;
        }

        var networkEnemy = GetComponent<NetworkEnemy>();
        if (networkEnemy != null)
            networkEnemy.SpawnEnemySkill(prefabToCast, spawnPosition);
        else
            Instantiate(prefabToCast, spawnPosition, Quaternion.identity);
    }

    // Executes roaming operation.
    private void Roaming()
    {
        if (!CanUseNavMeshAgent())
            return;

        roamPosition = GetRoamingPosition();
        navMeshAgent.SetDestination(roamPosition);
    }

    // Executes get roaming position operation.
    private Vector3 GetRoamingPosition()
    {
        // Randomize the eligible candidates before selecting this gameplay result.
        return startingPosition + Utilites.GetRandomDir() * UnityEngine.Random.Range(roamingDistanceMin, roamingDistanceMax);
    }

    // Executes change face dir operation.
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

    private static GameObject[] _playersThisFrame = Array.Empty<GameObject>();
    private static int _playersFrame = -1;

    // Executes get players cached operation.
    private static GameObject[] GetPlayersCached()
    {
        if (_playersFrame != Time.frameCount)
        {
            _playersFrame = Time.frameCount;
            _playersThisFrame = GameObject.FindGameObjectsWithTag("Player");
        }
        return _playersThisFrame;
    }

    // Executes find nearest player operation.
    private Transform FindNearestPlayer()
    {
        GameObject[] players = GetPlayersCached();
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

    // Executes can use nav mesh agent operation.
    private bool CanUseNavMeshAgent()
    {
        return navMeshAgent != null &&
               navMeshAgent.enabled &&
               navMeshAgent.isActiveAndEnabled &&
               navMeshAgent.isOnNavMesh;
    }
}
