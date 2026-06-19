using System;
using UnityEngine;

public class EnemyEntity : MonoBehaviour
{
    private PolygonCollider2D polyColl;
    private CapsuleCollider2D capsuleColl;
    private BoxCollider2D boxColl;
    private EnemyBehaviour enemyBehaviour;

    private int currentHealth;
    [SerializeField] private int maxHealth;

    public event EventHandler OnTakeHit;
    public event EventHandler OnDeath;

    private void Start()
    {
        polyColl = GetComponent<PolygonCollider2D>();
        capsuleColl = GetComponent<CapsuleCollider2D>();
        boxColl = GetComponent<BoxCollider2D>();
        enemyBehaviour = GetComponent<EnemyBehaviour>();
        currentHealth = maxHealth * 2;
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("Attack");
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        OnTakeHit?.Invoke(this, EventArgs.Empty);
        Debug.Log("Damage");
        DetectDeath();
    }

    public void PolyCollTurnOff()
    {
        Debug.Log("Disabled");

        polyColl.enabled = false;
    }
    public void PolyCollTurnOn()
    {
        Debug.Log("Enabled");

        polyColl.enabled = true;
    }


    private void DetectDeath()
    {
        if (currentHealth <= 0)
        {
            boxColl.enabled = false;
            polyColl.enabled = false;
            capsuleColl.enabled = false;

            enemyBehaviour.SetDeathState();
            Debug.Log("Destroy");
            OnDeath?.Invoke(this, EventArgs.Empty);

        }
    }
}
