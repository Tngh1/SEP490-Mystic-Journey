using UnityEngine;
using UnityEngine.AI;
using EnemyPatrol.Utilites;
using System;

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

    private float chasingSpeed;
    private float nextAttackTime = 1f;
    private float roamingSpeed;
    private float roamingTime;

    public event EventHandler OnEnemyAttack;

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
        State newState = State.Roaming;

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

            currentState = newState;
        }
    }

    public bool IsRunning
    {
        get
        {
            return navMeshAgent != null && navMeshAgent.velocity.magnitude > 0.1f;
        }
    }

    private void AttackingTarget()
    {
        if (Time.time > nextAttackTime)
        {
            // Báo hiệu quái đang tấn công (để bật Animation)
            OnEnemyAttack?.Invoke(this, EventArgs.Empty);

            // Gây sát thương thẳng lên PlayerEntity
            if (PlayerEntity.Instance != null)
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

    private static Transform GetPlayerTarget()
    {
        if (PlayerBehaviour.Instance != null)
            return PlayerBehaviour.Instance.transform;

        if (PlayerMovement.Instance != null)
            return PlayerMovement.Instance.transform;

        return null;
    }

    private bool CanUseNavMeshAgent()
    {
        return navMeshAgent != null &&
               navMeshAgent.enabled &&
               navMeshAgent.isActiveAndEnabled &&
               navMeshAgent.isOnNavMesh;
    }
}
