using System;
using UnityEngine;

// Initializes a new default instance of the AnimatorExtensions class.
public static class AnimatorExtensions
{
    // Executes has parameter operation.
    // Validates input parameters against null or empty values.
    // Evaluates conditions and returns a boolean result.
    public static bool HasParameter(this Animator animator, string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName))
            return false;

        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }
}

// Executes mono behaviour operation.
public class EnemyAnimations : MonoBehaviour
{
    [SerializeField] private EnemyBehaviour enemyBehaviour;
    [SerializeField] private EnemyEntity enemyEntity;

#pragma warning disable CS0067
    public event EventHandler OnTakeHit;
#pragma warning restore CS0067

    private Animator anim;
    private const string IS_RUNNING = "Move";
    private const string CHASING_SPEED_MULTIPLIIER = "ChasingSpeedMultiplier";
    private const string ATTACK = "EnemyAttack";
    private const string TAKEHIT = "TakeDamage";
    private const string DIED = "Died";
    private const string CAST_SKILL = "CastSkill";

    SpriteRenderer spriteRenderer;

    private float stopDelay = 0.1f;
    private float stopTimer = 0f;

    // Initializes internal component caches and dependencies for AnimatorExtensions upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

        if (enemyBehaviour == null)
            enemyBehaviour = GetComponent<EnemyBehaviour>() ?? GetComponentInParent<EnemyBehaviour>() ?? GetComponentInChildren<EnemyBehaviour>();

        if (enemyEntity == null)
            enemyEntity = GetComponent<EnemyEntity>() ?? GetComponentInParent<EnemyEntity>() ?? GetComponentInChildren<EnemyEntity>();
    }

    // Performs startup initialization for AnimatorExtensions on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (enemyBehaviour == null)
            enemyBehaviour = GetComponent<EnemyBehaviour>() ?? GetComponentInParent<EnemyBehaviour>() ?? GetComponentInChildren<EnemyBehaviour>();

        if (enemyEntity == null)
            enemyEntity = GetComponent<EnemyEntity>() ?? GetComponentInParent<EnemyEntity>() ?? GetComponentInChildren<EnemyEntity>();

        if (enemyBehaviour != null)
        {
            enemyBehaviour.OnEnemyAttack += enemyBehaviour_OnEnemyAttack;
            enemyBehaviour.OnEnemyCastSkill += enemyBehaviour_OnEnemyCastSkill;
        }

        if (enemyEntity != null)
        {
            enemyEntity.OnTakeHit += enemyEntity_OnTakeHit;
            enemyEntity.OnDeath += enemyEntity_OnDeath;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Executes trigger att anim turn off operation.
    public void TriggerAttAnimTurnOff()
    {
        if (enemyEntity != null) enemyEntity.PolyCollTurnOff();
    }

    // Executes trigger att anim turn on operation.
    public void TriggerAttAnimTurnOn()
    {
        if (enemyEntity != null) enemyEntity.PolyCollTurnOn();
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (enemyBehaviour != null)
        {
            enemyBehaviour.OnEnemyAttack -= enemyBehaviour_OnEnemyAttack;
            enemyBehaviour.OnEnemyCastSkill -= enemyBehaviour_OnEnemyCastSkill;
        }

        if (enemyEntity != null)
        {
            enemyEntity.OnTakeHit -= enemyEntity_OnTakeHit;
            enemyEntity.OnDeath -= enemyEntity_OnDeath;
        }
    }

    void Update()
    {
        if (enemyBehaviour == null || anim == null) return;

        if (enemyBehaviour.IsRunning)
        {
            stopTimer = stopDelay;
            anim.SetBool(IS_RUNNING, true);
        }
        else
        {
            stopTimer -= Time.deltaTime;
            if (stopTimer <= 0f)
                anim.SetBool(IS_RUNNING, false);
        }

        float roamingSpeed = enemyBehaviour.GetRoamingAnimationSpeed();
        if (roamingSpeed > 0f)
        {
            if (anim.HasParameter(CHASING_SPEED_MULTIPLIIER))
            {
                anim.SetFloat(CHASING_SPEED_MULTIPLIIER, roamingSpeed);
            }
        }
    }

    // Executes enemy behaviour_on enemy attack operation.
    private void enemyBehaviour_OnEnemyAttack(object sender, System.EventArgs e)
    {
        PlayAttackAnimation();
    }

    // Executes enemy behaviour_on enemy cast skill operation.
    private void enemyBehaviour_OnEnemyCastSkill(object sender, System.EventArgs e)
    {
        PlaySkillAnimation();
    }

    // Executes play attack animation operation.
    public void PlayAttackAnimation()
    {
        if (anim == null) return;
        if (anim.HasParameter(ATTACK)) anim.SetTrigger(ATTACK);
        else if (anim.HasParameter("Attack")) anim.SetTrigger("Attack");
    }

    // Executes play skill animation operation.
    public void PlaySkillAnimation()
    {
        if (anim == null) return;
        if (anim.HasParameter(CAST_SKILL)) anim.SetTrigger(CAST_SKILL);
        else if (anim.HasParameter(ATTACK)) anim.SetTrigger(ATTACK);
        else if (anim.HasParameter("Attack")) anim.SetTrigger("Attack");
    }

    // Executes enemy entity_on take hit operation.
    private void enemyEntity_OnTakeHit(object sender, EventArgs e)
    {
        if (anim != null) anim.SetTrigger(TAKEHIT);
    }

    // Executes enemy entity_on death operation.
    private void enemyEntity_OnDeath(object sender, EventArgs e)
    {
        if (anim != null) anim.SetBool(DIED, true);
    }
}
