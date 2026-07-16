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

    [Header("Skill Settings")]
    [SerializeField] private bool canCastSkill = false;
    [SerializeField] private GameObject skillPrefab;
    [SerializeField] private float skillCooldown = 7f;
    [SerializeField] private float skillSpawnDelay = 0.5f; // Thời gian chờ để khớp với animation
    [SerializeField] private Transform skillSpawnPoint;
    private float nextSkillTime = 0f;

    private float chasingSpeed;
    private float nextAttackTime = 1f;
    private float roamingSpeed;
    private float roamingTime;

    public event EventHandler OnEnemyAttack;
    public event EventHandler OnEnemyCastSkill;

    private float nextCheckDirectionTime = 0f;
    private float checkDirectionDuration = 0.1f;
    private Vector3 lastPosition;

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
        var target = GetPlayerTarget();
        if (target == null || !CanUseNavMeshAgent())
            return;

        navMeshAgent.SetDestination(target.position);
    }

    public float GetRoamingAnimationSpeed()
    {
        if (roamingSpeed <= 0f || navMeshAgent == null)
            return 0f;

        return navMeshAgent.speed / roamingSpeed;
    }

    private void CheckCurrentState()
    {
        var target = GetPlayerTarget();
        if (target == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, target.position);
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

            var target = GetPlayerTarget();
            if (target != null)
            {
                var networkPlayer = target.GetComponent<NetworkPlayer>();
                if (networkPlayer != null)
                {
                    networkPlayer.RequestDamage(attackDamage);
                }
                else
                {
                    var playerEntity = target.GetComponent<PlayerEntity>();
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
            var target = GetPlayerTarget();

            if (IsRunning)
            {
                ChangeFaceDir(lastPosition, transform.position);
            }
            else if (currentState == State.Attack && target != null)
            {
                ChangeFaceDir(transform.position, target.position);
            }

            lastPosition = transform.position;
            nextCheckDirectionTime = Time.time + checkDirectionDuration;
        }
    }

    private void CheckSkillCasting()
    {
        if (!canCastSkill || skillPrefab == null) return;

        var target = GetPlayerTarget();
        if (target == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, target.position);
        
        if (distanceToPlayer <= chasingDistance * 2f) 
        {
            if (Time.time >= nextSkillTime)
            {
                CastSkill(target);
                nextSkillTime = Time.time + skillCooldown;
            }
        }
    }

    private void CastSkill(Transform target)
    {
        OnEnemyCastSkill?.Invoke(this, EventArgs.Empty);

        StartCoroutine(SpawnSkillWithDelay(target, skillSpawnDelay));
    }

    private IEnumerator SpawnSkillWithDelay(Transform target, float delay)
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

        Instantiate(skillPrefab, spawnPosition, Quaternion.identity);
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

    private Transform GetPlayerTarget()
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
            if (networkPlayer != null && !networkPlayer.IsAlive) continue;

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
