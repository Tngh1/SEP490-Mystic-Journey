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
    private NavMeshAgent navMeshAgent;
    private State currentState;
    private Vector3 roamPosition;
    private Vector3 startingPosition;

    [SerializeField] private float chasingDistance = 4f;
    [SerializeField] private float chasingSpeedMultiplier = 2f;
    [SerializeField] private float attackDistance = 2f;
    [SerializeField] private float attackRate = 2f;
    [SerializeField] private float leashDistance = 15f; // Quãng đường tối đa đi xa khỏi nhà
    private bool isReturning = false; // Trạng thái đang quay về

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
    }

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
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

    public void UpdateStatsFromAPI(int apiAttack, float apiMoveSpeed)
    {
        attackDamage = apiAttack;
        if (navMeshAgent != null && apiMoveSpeed > 0)
        {
            // Base API speed 100 ~ 3.5f Unity speed
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
    }

    public void SetDeathState()
    {
        if (CanUseNavMeshAgent())
            navMeshAgent.ResetPath();

        currentState = State.Death;
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

            if (currentTarget != null)
            {
                var networkPlayer = currentTarget.GetComponent<NetworkPlayer>();
                if (networkPlayer != null)
                {
                    networkPlayer.RequestDamage(attackDamage);
                }
                else
                {
                    var playerEntity = currentTarget.GetComponent<PlayerEntity>();
                    if (playerEntity != null)
                    {
                        playerEntity.TakeDamage(attackDamage);
                    }
                    else if (PlayerEntity.Instance != null)
                    {
                        PlayerEntity.Instance.TakeDamage(attackDamage);
                    }
                }
            }
            else if (PlayerEntity.Instance != null)
            {
                PlayerEntity.Instance.TakeDamage(attackDamage);
            }

            nextAttackTime = Time.time + attackRate;
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
        if (currentTarget == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, currentTarget.position);

        if (distanceToPlayer <= chasingDistance * 2f)
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
        if (players == null || players.Length == 0) return null;

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
