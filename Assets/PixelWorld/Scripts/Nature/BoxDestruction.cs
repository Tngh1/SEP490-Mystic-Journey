using System;
using System.Collections;
using UnityEngine;

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



    // Start is called before the first frame update
    void Start()
    {
        boxCollider3 = GetComponent<BoxCollider2D>();
        currentBoxHealth = maxBoxHealth;
        boxDestruction3 = GetComponent<Box3>();
        boxDestruction3.OnDestruction += boxDestruction3_OnDestruction;
    }

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

    private void DetectDeath()
    {
        if (currentBoxHealth <= 0)
        {
            OnDestruction?.Invoke(this, EventArgs.Empty);
            boxCollider3.enabled = false;
            StartCoroutine(Wait());
        }
    }
    private void boxDestruction3_OnDestruction(object sender, EventArgs e)
    {
        anim.SetTrigger(DESTRUCTION);
    }

    private IEnumerator Wait()
    {
        yield return new WaitForSeconds(0.4f);
        Destroy(box);
    }
}
