using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Executes mono behaviour operation.
public class GateOpens : MonoBehaviour
{
    private BoxCollider2D boxCollider;
    [SerializeField] private Animator anim;


    void Start()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        anim.SetBool("Idle", true);
    }

    void Update()
    {

    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.tag == "Player")
        {
            Debug.Log("open");
            anim.SetBool("Idle", false);
            anim.SetTrigger("Open");
            anim.SetBool("Opened", true);
            boxCollider.isTrigger = true;
        }

    }

    // Executes on trigger exit2 d operation.
    private void OnTriggerExit2D(Collider2D collision)
    {
            Debug.Log("closed");
            anim.SetBool("Opened", false);
            anim.SetTrigger("Close");
            anim.SetBool("Idle", true);

    }


}
