using UnityEngine;

public class Character3 : MonoBehaviour
{
    private SpriteRenderer m_SpriteRenderer;

    public SpriteRenderer SpriteRenderer => m_SpriteRenderer;

    void Start()
    {
        m_SpriteRenderer = GetComponent<SpriteRenderer>();
    }
}
