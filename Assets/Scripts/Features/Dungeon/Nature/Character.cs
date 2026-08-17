using UnityEngine;

// Executes mono behaviour operation.
public class Character3 : MonoBehaviour
{
    private SpriteRenderer m_SpriteRenderer;

    // Executes sprite renderer operation.
    public SpriteRenderer SpriteRenderer => m_SpriteRenderer;

    void Start()
    {
        m_SpriteRenderer = GetComponent<SpriteRenderer>();
    }
}
