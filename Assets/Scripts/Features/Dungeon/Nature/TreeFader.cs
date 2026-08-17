using UnityEngine;
using UnityEngine.Tilemaps;

// Executes mono behaviour operation.
public class TreeFader3 : MonoBehaviour
{
    const float VISIBLE_ALPHA = 1f;
    const float TRANSPARENT_ALPHA = 0.3f;
    const float ALPHA_EPSILON = 0.005f;

    private SpriteRenderer[] m_SpriteRenderers;
    private Tilemap[] m_Tilemaps;
    private bool m_FadeOutEnabled = false;

    private int m_InitialSortOrder;
    private SpriteRenderer m_InteractorRenderer;

    // Executes background object alpha operation.
    private float BackgroundObjectAlpha
    {
        get
        {
            if (m_SpriteRenderers != null && m_SpriteRenderers.Length > 0 && m_SpriteRenderers[0] != null)
                return m_SpriteRenderers[0].color.a;
            if (m_Tilemaps != null && m_Tilemaps.Length > 0 && m_Tilemaps[0] != null)
                return m_Tilemaps[0].color.a;
            return 1f;
        }
    }

    void Awake()
    {
        m_SpriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        m_Tilemaps = GetComponentsInChildren<Tilemap>();
        this.enabled = false;
    }

    void Update()
    {
        bool hasGraphics = (m_SpriteRenderers != null && m_SpriteRenderers.Length > 0) ||
                           (m_Tilemaps != null && m_Tilemaps.Length > 0);
        if (!hasGraphics) { this.enabled = false; return; }

        float currentAlpha = BackgroundObjectAlpha;
        float targetAlpha = m_FadeOutEnabled ? TRANSPARENT_ALPHA : VISIBLE_ALPHA;
        float diff = Mathf.Abs(currentAlpha - targetAlpha);

        if (diff <= ALPHA_EPSILON)
        {
            ApplyAlpha(targetAlpha);
            if (!m_FadeOutEnabled && m_InteractorRenderer != null)
                m_InteractorRenderer.sortingOrder = m_InitialSortOrder;
            this.enabled = false;
            return;
        }

        if (m_FadeOutEnabled)
            FadeOut();
        else
            FadeIn();
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (m_FadeOutEnabled) return;

        if (collision.CompareTag("Player"))
        {
            m_InteractorRenderer = collision.GetComponentInChildren<SpriteRenderer>();
            if (m_InteractorRenderer != null)
            {
                m_InitialSortOrder = m_InteractorRenderer.sortingOrder;
                m_FadeOutEnabled = true;
                this.enabled = true;
            }
        }
    }

    // Executes on trigger exit2 d operation.
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!m_FadeOutEnabled) return;

        if (collision.CompareTag("Player"))
        {
            m_FadeOutEnabled = false;
            this.enabled = true;
        }
    }

    // Executes fade out operation.
    private void FadeOut()
    {
        if (m_SpriteRenderers != null)
            foreach (var renderer in m_SpriteRenderers)
                ChangeSpriteOpacity(renderer, TRANSPARENT_ALPHA);
        if (m_Tilemaps != null)
            foreach (var map in m_Tilemaps)
                ChangeTilemapOpacity(map, TRANSPARENT_ALPHA);
    }

    // Executes fade in operation.
    private void FadeIn()
    {
        if (m_SpriteRenderers != null)
            foreach (var renderer in m_SpriteRenderers)
                ChangeSpriteOpacity(renderer, VISIBLE_ALPHA);
        if (m_Tilemaps != null)
            foreach (var map in m_Tilemaps)
                ChangeTilemapOpacity(map, VISIBLE_ALPHA);
    }

    // Executes apply alpha operation.
    private void ApplyAlpha(float alpha)
    {
        if (m_SpriteRenderers != null)
            foreach (var r in m_SpriteRenderers)
                if (r != null) { var c = r.color; r.color = new Color(c.r, c.g, c.b, alpha); }
        if (m_Tilemaps != null)
            foreach (var t in m_Tilemaps)
                if (t != null) { var c = t.color; t.color = new Color(c.r, c.g, c.b, alpha); }
    }

    // Executes change sprite opacity operation.
    private void ChangeSpriteOpacity(SpriteRenderer renderer, float targetAlpha)
    {
        Color color = renderer.color;
        renderer.color = new Color(color.r, color.g, color.b, Mathf.MoveTowards(color.a, targetAlpha, Time.deltaTime * 4));
    }

    // Executes change tilemap opacity operation.
    private void ChangeTilemapOpacity(Tilemap map, float targetAlpha)
    {
        Color color = map.color;
        map.color = new Color(color.r, color.g, color.b, Mathf.MoveTowards(color.a, targetAlpha, Time.deltaTime * 4));
    }
}
