using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;

    private Rigidbody2D rb;

    private Vector2 moveInput;
    private Vector2 lastMove = Vector2.down;

    public Vector2 MoveInput => moveInput;
    public Vector2 LastMove => lastMove;
    public bool IsMoving => moveInput.sqrMagnitude > 0.01f;
    public static PlayerMovement Instance { get; private set; }
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        Instance = this;
    }

    private void FixedUpdate()
    {
        rb.MovePosition(
            rb.position +
            moveInput * moveSpeed * Time.fixedDeltaTime
        );
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();

        if (moveInput != Vector2.zero)
        {
            lastMove = moveInput.normalized;
        }
    }

}