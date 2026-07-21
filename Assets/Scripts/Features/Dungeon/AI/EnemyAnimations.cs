using System;
using UnityEngine;

/// <summary>
/// Extension method để kiểm tra parameter có tồn tại trong Animator Controller không
/// </summary>
public static class AnimatorExtensions
{
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

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    private void Start()
    {
        enemyBehaviour.OnEnemyAttack += enemyBehaviour_OnEnemyAttack;
        enemyBehaviour.OnEnemyCastSkill += enemyBehaviour_OnEnemyCastSkill;
        enemyEntity.OnTakeHit += enemyEntity_OnTakeHit;
        enemyEntity.OnDeath += enemyEntity_OnDeath;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void TriggerAttAnimTurnOff()
    {
        enemyEntity.PolyCollTurnOff();
    }

    public void TriggerAttAnimTurnOn()
    {
        enemyEntity.PolyCollTurnOn();
    }

    private void OnDestroy()
    {
        if (enemyBehaviour != null)
        {
            enemyBehaviour.OnEnemyAttack -= enemyBehaviour_OnEnemyAttack;
            enemyBehaviour.OnEnemyCastSkill -= enemyBehaviour_OnEnemyCastSkill;
        }
    }

    void Update()
    {
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

        // Chỉ set speed parameter nếu nó tồn tại trong Animator Controller
        float roamingSpeed = enemyBehaviour.GetRoamingAnimationSpeed();
        if (roamingSpeed > 0f)
        {
            // Kiểm tra parameter tồn tại trước khi set để tránh warning mỗi frame
            if (anim.HasParameter(CHASING_SPEED_MULTIPLIIER))
            {
                anim.SetFloat(CHASING_SPEED_MULTIPLIIER, roamingSpeed);
            }
        }
    }

    private void enemyBehaviour_OnEnemyAttack(object sender, System.EventArgs e)
    {
        anim.SetTrigger(ATTACK);
    }

    private void enemyBehaviour_OnEnemyCastSkill(object sender, System.EventArgs e)
    {
        anim.SetTrigger(CAST_SKILL);
    }

    private void enemyEntity_OnTakeHit(object sender, EventArgs e)
    {
        anim.SetTrigger(TAKEHIT);
    }

    private void enemyEntity_OnDeath(object sender, EventArgs e)
    {
        anim.SetBool(DIED, true);
    }
}