using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Executes mono behaviour operation.
public class Door3 : MonoBehaviour
{
    [SerializeField] BoxCollider2D boxCollider3;
    [SerializeField] CapsuleCollider2D capsuleCollider3;
    [SerializeField] private Animator anim;
    [SerializeField] private GameObject door;


    void Start()
    {
         boxCollider3 = GetComponent<BoxCollider2D>();
         capsuleCollider3 = GetComponent<CapsuleCollider2D>();
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D collision)
 {
     if(collision.tag == "Player")
     {
        anim.SetTrigger("OpenD");
        capsuleCollider3.enabled = false;
     }
 }



}
