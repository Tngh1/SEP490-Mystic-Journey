using System;
using UnityEngine;

// Executes mono behaviour operation.
public class PlayerBehaviour : MonoBehaviour
{
    // Executes instance operation.
    public static PlayerBehaviour Instance { get; private set; }
    [SerializeField] private float movingSpeed = 4f;
#pragma warning disable CS0067
    public event EventHandler OnPlayerAttack;
#pragma warning restore CS0067
    private Box3 box3;

    private Rigidbody2D rb;
    private PolygonCollider2D polyCollider;
    [SerializeField] private Animator anim;
    [SerializeField] private GameObject attackPoint;

    public VectorValue3 startingPosition3;
    private float minMovingSpeed = 0.1f;
    private bool isRunning = false;

    private float cooldownTimer = Mathf.Infinity;
    [SerializeField] private float attackCooldown;
    [SerializeField] private int damageAmount;


    private Vector2 inputVector;

    // Initializes internal component caches and dependencies for PlayerBehaviour upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        attackPoint.SetActive(false);
        transform.position = startingPosition3.initialValue3;
        polyCollider = GetComponent<PolygonCollider2D>();
    }

    // Performs startup initialization for PlayerBehaviour on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        PolyCollTurnOff();
    }


    // Per-frame update loop for PlayerBehaviour.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        inputVector = Vector2.zero;

        if (Input.GetKey(KeyCode.W)) inputVector.y = 1f;
        if (Input.GetKey(KeyCode.S)) inputVector.y = -1f;
        if (Input.GetKey(KeyCode.A)) inputVector.x = -1f;
        if (Input.GetKey(KeyCode.D)) inputVector.x = 1f;

        isRunning = inputVector.magnitude > minMovingSpeed;


        cooldownTimer += Time.deltaTime;

        if (Input.GetKey(KeyCode.Mouse0) && cooldownTimer > attackCooldown)
        {
            Debug.Log("Mouse");
            anim.SetTrigger("PlayerAttack");
            cooldownTimer = 0;
        }
    }


    // Executes fixed update operation.
    private void FixedUpdate()
    {
        rb.MovePosition(rb.position + inputVector.normalized * (movingSpeed * Time.fixedDeltaTime));
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.transform.TryGetComponent(out EnemyEntity enemyEntity) && polyCollider.enabled == true)
        {
            enemyEntity.TakeDamage(damageAmount);
        }

        if (collision.transform.TryGetComponent(out Box3 box3) && polyCollider.enabled == true)
        {
            box3.TakeDamageBox(damageAmount);
        }
    }

    // Executes poly coll turn off operation.
    public void PolyCollTurnOff() { polyCollider.enabled = false; }
    // Executes poly coll turn on operation.
    public void PolyCollTurnOn()
    {
        polyCollider.enabled = true;
        Debug.Log("PolyCollider");
    }

    // Executes is running operation.
    public bool IsRunning() { return isRunning; }
}
