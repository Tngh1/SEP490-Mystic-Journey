using System;
using UnityEngine;


public class EnemyAnimations : MonoBehaviour
{
    [SerializeField] private EnemyBehaviour enemyBehaviour;
    [SerializeField] private EnemyEntity enemyEntity;

    public event EventHandler OnTakeHit;


    private Animator anim;
    private const string IS_RUNNING = "Move";
    private const string CHASING_SPEED_MULTIPLIIER = "ChasingSpeedMultiplier";
    private const string ATTACK = "EnemyAttack";
    private const string TAKEHIT = "TakeDamage";
    private const string DIED = "Died";

    SpriteRenderer spriteRenderer;

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    private void Start()
    {
        enemyBehaviour.OnEnemyAttack += enemyBehaviour_OnEnemyAttack;
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
        enemyBehaviour.OnEnemyAttack -= enemyBehaviour_OnEnemyAttack;
    }

    void Update()
    {
        anim.SetBool(IS_RUNNING, enemyBehaviour.IsRunning);
        anim.SetFloat(CHASING_SPEED_MULTIPLIIER, enemyBehaviour.GetRoamingAnimationSpeed());
    }

    private void enemyBehaviour_OnEnemyAttack(object sender, System.EventArgs e)
    {
        anim.SetTrigger(ATTACK);
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

