using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;

    private Rigidbody2D rb;
    private Animator animator;

    private Vector2 input;
    private Vector2 lastMove;

    public Vector2 MoveInput => input;
    public bool IsMoving => input.sqrMagnitude > 0.01f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        ReadInput();

        // Lưu hướng cuối cùng
        if (input != Vector2.zero)
        {
            lastMove = input;
        }

        // Update Animator
        animator.SetFloat("MoveX", lastMove.x);
        animator.SetFloat("MoveY", lastMove.y);
        animator.SetFloat("Speed", IsMoving ? 1f : 0f);
        Debug.Log(IsMoving ? 1f : 0f);
    }

    private void FixedUpdate()
    {
        rb.MovePosition(rb.position + input * moveSpeed * Time.fixedDeltaTime);
    }

    private void ReadInput()
    {
        input = Vector2.zero;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            input.x = -1f;
        else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            input.x = 1f;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            input.y = 1f;
        else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            input.y = -1f;

        input.Normalize();
    }
}