using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private PlayerMovement movement;

    [Header("Animation")]
    [SerializeField] private float smoothTime = 0.03f;

    private Vector2 lastMove = Vector2.down;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (movement == null)
            movement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        Vector2 moveInput = movement.MoveInput;

        if (moveInput != Vector2.zero)
        {
            if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
                lastMove = new Vector2(Mathf.Sign(moveInput.x), 0f);
            else
                lastMove = new Vector2(0f, Mathf.Sign(moveInput.y));
        }

        float animX = Mathf.Abs(lastMove.x);
        float animY = lastMove.y;
        float speed = movement.IsMoving ? 1f : 0f;

        animator.SetFloat("MoveX", animX, smoothTime, Time.deltaTime);
        animator.SetFloat("MoveY", animY, smoothTime, Time.deltaTime);
        animator.SetFloat("Speed", speed, smoothTime, Time.deltaTime);

        if (lastMove.x != 0)
            spriteRenderer.flipX = lastMove.x < 0;
    }
}