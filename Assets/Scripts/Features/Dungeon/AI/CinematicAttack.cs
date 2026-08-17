using UnityEngine;
using UnityEngine.AI;

// Executes mono behaviour operation.
public class CinematicAttack : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float arrivalDistance = 1.5f;
    [SerializeField] private float attackRate = 2f;

    private NavMeshAgent navMeshAgent;
    private EnemyBehaviour enemyBehaviour;
    private Animator anim;
    private bool isAttacking = false;
    private float nextAttackTime = 0f;

    private const string ATTACK = "EnemyAttack";

    // Initializes internal component caches and dependencies for CinematicAttack upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        enemyBehaviour = GetComponent<EnemyBehaviour>();
        anim = GetComponentInChildren<Animator>();
    }

    // Per-frame update loop for CinematicAttack.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (target == null) return;

        if (!isAttacking)
        {
            navMeshAgent.SetDestination(target.position);

            if (!navMeshAgent.pathPending &&
                navMeshAgent.remainingDistance <= arrivalDistance)
            {
                StartCinematicAttack();
            }
        }
        else
        {
            if (Time.time >= nextAttackTime)
            {
                anim.SetTrigger(ATTACK);
                nextAttackTime = Time.time + attackRate;
            }
        }
    }

    // Executes start cinematic attack operation.
    private void StartCinematicAttack()
    {
        isAttacking = true;
        navMeshAgent.ResetPath();
        enemyBehaviour.SetDeathState();
    }
}
