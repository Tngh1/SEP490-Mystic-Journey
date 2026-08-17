using UnityEngine;

// Executes mono behaviour operation.
public class PlayerFlip3 : MonoBehaviour
{
    private bool FacingRight = true;

    void Update()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        if (mousePos.x < transform.position.x && FacingRight)
        {
            Flip();
        }
        else if (mousePos.x > transform.position.x && !FacingRight)
        {
            Flip();
        }
    }

    void Flip()
    {
        Vector3 tmpScale = transform.localScale;
        tmpScale.x = -tmpScale.x;
        transform.localScale = tmpScale;
        FacingRight = !FacingRight;
    }
}
