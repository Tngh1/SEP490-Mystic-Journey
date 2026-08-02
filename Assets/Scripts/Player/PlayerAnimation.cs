using UnityEngine;

/// <summary>
/// Animator parameter driver for the player. Reads movement direction and life
/// state from <see cref="NetworkPlayer"/> and writes them to the Animator and
/// SpriteRenderer.
///
/// Scope: pure presentation. Runs on every client (each client animates every
/// visible player independently). Driven by the NetworkPlayer.Render() callback
/// once per Unity Update after the simulation has settled.
///
/// Trigger forwarding:
///   - Attack / Skill1 / Skill2 / Skill3 triggers are forwarded from
///     PlayerCombat via TriggerAttack / TriggerSkill. PlayerCombat owns the
///     decision of when to trigger (Phase 7 will make these replicated via RPC).
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional. If null, fetched via GetComponent.")]
    [SerializeField] private Animator animator;
    [Tooltip("Optional. If null, fetched via GetComponentInChildren.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    // Cached parameter hashes (faster than string lookups).
    private static readonly int HashMoveX = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY = Animator.StringToHash("MoveY");
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashIsDead = Animator.StringToHash("IsDead");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashSkill1 = Animator.StringToHash("Skill1");
    private static readonly int HashSkill2 = Animator.StringToHash("Skill2");
    private static readonly int HashSkill3 = Animator.StringToHash("Skill3");

    private bool _lastIsDead;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Per-frame driver — called by NetworkPlayer.Render()
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Update movement / facing / death animator parameters.
    /// Called from <see cref="NetworkPlayer.Render"/>.
    /// </summary>
    /// <param name="move">Latest movement vector from the network (already normalized; can be zero when idle).</param>
    /// <param name="isAlive">Whether the player is currently alive.</param>
    public void SetMovement(Vector2 move, bool isAlive)
    {
        if (animator == null) return;

        float moveX = Mathf.Abs(move.x);
        float moveY = move.y;
        float sqrMag = move.sqrMagnitude;

        if (sqrMag > 0.01f)
        {
            // Prioritize animations based on diagonal direction:
            if (moveX > 0.1f && Mathf.Abs(moveY) > 0.1f)
            {
                if (moveY < -0.1f)
                {
                    // Diagonal Down -> prioritize horizontal slide
                    moveX = 1f;
                    moveY = 0f;
                }
                else if (moveY > 0.1f)
                {
                    // Diagonal Up -> prioritize vertical up (North)
                    moveX = 0f;
                    moveY = 1f;
                }
            }

            animator.SetFloat(HashMoveX, moveX);
            animator.SetFloat(HashMoveY, moveY);
        }
        animator.SetFloat(HashSpeed, sqrMag > 0.01f ? 1f : 0f);

        bool isDead = !isAlive;
        if (isDead != _lastIsDead)
        {
            _lastIsDead = isDead;
            animator.SetBool(HashIsDead, isDead);
        }

        if (spriteRenderer != null && move.x != 0f)
        {
            spriteRenderer.flipX = move.x < 0f;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Trigger forwarding — called by PlayerCombat (single-player path) and
    // eventually by replicated RPCs (multiplayer path, Phase 7).
    // ─────────────────────────────────────────────────────────────────────────

    public void TriggerAttack()
    {
        if (animator != null) animator.SetTrigger(HashAttack);
    }

    public void TriggerSkill(int slotIndex)
    {
        if (animator == null) return;
        switch (slotIndex)
        {
            case 0: animator.SetTrigger(HashSkill1); break;
            case 1: animator.SetTrigger(HashSkill2); break;
            case 2: animator.SetTrigger(HashSkill3); break;
        }
    }
}