using System;
using System.Collections;
using UnityEngine;

// Executes mono behaviour operation.
public class Box3 : MonoBehaviour
{
    private BoxCollider2D boxCollider3;
    [SerializeField] private Animator anim;
    [SerializeField] private GameObject box;
    private int currentBoxHealth;
    [SerializeField] private int maxBoxHealth;
    private const string DESTRUCTION = "Broke";
    private Box3 boxDestruction3;
    public event EventHandler OnDestruction;



    void Start()
    {
        boxCollider3 = GetComponent<BoxCollider2D>();
        currentBoxHealth = maxBoxHealth;
        boxDestruction3 = GetComponent<Box3>();
        boxDestruction3.OnDestruction += boxDestruction3_OnDestruction;
    }

    // Executes take damage box operation.
    public void TakeDamageBox(int boxdamage)
    {
        currentBoxHealth -= boxdamage;
        Debug.Log("Damage");

         if (currentBoxHealth <= 0)
        {
            anim.SetTrigger("Broke");
            boxCollider3.enabled = false;
            DetectDeath();
        }
    }

    // Executes detect death operation.
    private void DetectDeath()
    {
        if (currentBoxHealth <= 0)
        {
            OnDestruction?.Invoke(this, EventArgs.Empty);
            boxCollider3.enabled = false;
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(Wait());
        }
    }
    // Executes box destruction3_on destruction operation.
    private void boxDestruction3_OnDestruction(object sender, EventArgs e)
    {
        anim.SetTrigger(DESTRUCTION);
    }

    // Executes wait operation.
    private IEnumerator Wait()
    {
        yield return new WaitForSeconds(0.4f);
        Destroy(box);
    }
}
