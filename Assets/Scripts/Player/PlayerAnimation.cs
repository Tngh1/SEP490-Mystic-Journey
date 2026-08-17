using UnityEngine;

// Executes mono behaviour operation.
[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional. If null, fetched via GetComponent.")]
    [SerializeField] private Animator animator;
    [Tooltip("Optional. If null, fetched via GetComponentInChildren.")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("Check this if the character's original sprite is drawn facing left (e.g. Southwest) instead of right.")]
    [SerializeField] private bool invertFlipX = false;

    private static readonly int HashMoveX = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY = Animator.StringToHash("MoveY");
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashIsDead = Animator.StringToHash("IsDead");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashSkill1 = Animator.StringToHash("Skill1");
    private static readonly int HashSkill2 = Animator.StringToHash("Skill2");
    private static readonly int HashSkill3 = Animator.StringToHash("Skill3");

    private bool _lastIsDead;


    private Transform _firePoint;
    private float _firePointAbsX;

    // Initializes internal component caches and dependencies for PlayerAnimation upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        _firePoint = transform.Find("FirePoint");
        if (_firePoint != null)
        {
            _firePointAbsX = Mathf.Abs(_firePoint.localPosition.x);
        }
    }


    // Executes set movement operation.
    public void SetMovement(Vector2 move, bool isAlive)
    {
        if (animator == null) return;

        float moveX = Mathf.Abs(move.x);
        float moveY = move.y;
        float sqrMag = move.sqrMagnitude;

        if (sqrMag > 0.01f)
        {
            if (moveX > 0.1f && Mathf.Abs(moveY) > 0.1f)
            {
                if (moveY < -0.1f)
                {
                    moveX = 1f;
                    moveY = 0f;
                }
                else if (moveY > 0.1f)
                {
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
            bool shouldFlip = move.x < 0f;
            spriteRenderer.flipX = invertFlipX ? !shouldFlip : shouldFlip;

            if (_firePoint != null && _firePointAbsX > 0.001f)
            {
                Vector3 fPos = _firePoint.localPosition;
                fPos.x = move.x < 0f ? -_firePointAbsX : _firePointAbsX;
                _firePoint.localPosition = fPos;
            }
        }
    }


    // Executes trigger attack operation.
    public void TriggerAttack()
    {
        if (animator != null) animator.SetTrigger(HashAttack);
    }

    // Executes trigger skill operation.
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
