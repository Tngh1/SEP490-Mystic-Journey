using System;
using UnityEngine;

public class PlayerBehaviour : MonoBehaviour
{
    public static PlayerBehaviour Instance { get; private set; }
    [SerializeField] private float movingSpeed = 4f;
    public event EventHandler OnPlayerAttack;
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

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        attackPoint.SetActive(false);
        transform.position = startingPosition3.initialValue3;
        polyCollider = GetComponent<PolygonCollider2D>();
    }

    private void Start()
    {
        PolyCollTurnOff();
    }

    
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

    
    private void FixedUpdate()
    {
        rb.MovePosition(rb.position + inputVector.normalized * (movingSpeed * Time.fixedDeltaTime));
    }

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

    public void PolyCollTurnOff() { polyCollider.enabled = false; }
    public void PolyCollTurnOn()
    {
        polyCollider.enabled = true;
        Debug.Log("PolyCollider");
    }

    public bool IsRunning() { return isRunning; }
}
