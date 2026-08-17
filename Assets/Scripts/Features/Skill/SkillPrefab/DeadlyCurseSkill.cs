using UnityEngine;

// Executes skill projectile operation.
public class DeadlyCurseSkill : SkillProjectile
{
    private Animator anim;
    private bool isExploding = false;

    [SerializeField] private float explodeDuration = 0.5f;

    // Initializes internal component caches and dependencies for DeadlyCurseSkill upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    // Per-frame update loop for DeadlyCurseSkill.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    protected override void Update()
    {
        if (!isExploding)
        {
            base.Update();
        }
    }

    // Executes on hit target operation.
    protected override void OnHitTarget()
    {
        isExploding = true;

        if (anim != null)
        {
            anim.SetTrigger("Hit");
        }

        Destroy(gameObject, explodeDuration);
    }
}
