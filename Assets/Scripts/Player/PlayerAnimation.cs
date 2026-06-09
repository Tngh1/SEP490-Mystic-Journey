using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (movement == null)
            movement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        Vector2 direction = movement.LastMove;

        float moveX = Mathf.Abs(direction.x);
        float moveY = direction.y;

        animator.SetFloat("MoveX", moveX);
        animator.SetFloat("MoveY", moveY);
        animator.SetFloat("Speed",
            movement.IsMoving ? 1f : 0f);

        if (direction.x != 0)
        {
            spriteRenderer.flipX = direction.x < 0;
        }
    }
}