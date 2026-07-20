using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BuildingFader3 : MonoBehaviour
{
    const float VISIBLE_ALPHA = 1f;
    const float TRANSPARENT_ALPHA = 0.3f;

    private SpriteRenderer[] m_SpriteRenderers;
    private Tilemap[] m_Tilemaps;
    private bool m_FadeOutEnabled = false;

    private int m_InitialSortOrder;
    private SpriteRenderer m_InteractorRenderer;

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

    void Start()
    {
        m_SpriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        m_Tilemaps = GetComponentsInChildren<Tilemap>();
    }

    void Update()
    {
        bool hasGraphics = (m_SpriteRenderers != null && m_SpriteRenderers.Length > 0) || 
                           (m_Tilemaps != null && m_Tilemaps.Length > 0);
        if (!hasGraphics) return;

        if (m_FadeOutEnabled && BackgroundObjectAlpha > TRANSPARENT_ALPHA)
        {
            FadeOut();

            if (BackgroundObjectAlpha <= TRANSPARENT_ALPHA)
            {
                if (m_InteractorRenderer != null) 
                {
                    // Optionally adjust sorting order if Unity's Y-Axis Custom Sort isn't enough
                    // m_InteractorRenderer.sortingOrder = -10; 
                }
            }
        }
        else if (!m_FadeOutEnabled && BackgroundObjectAlpha < VISIBLE_ALPHA)
        {
            FadeIn();
            if (m_InteractorRenderer != null) 
            {
                m_InteractorRenderer.sortingOrder = m_InitialSortOrder;
            }
        }
    }

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
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!m_FadeOutEnabled) return;

        if (collision.CompareTag("Player"))
        {
            m_FadeOutEnabled = false;
        }
    }

    private void FadeOut()
    {
        if (m_SpriteRenderers != null)
        {
            foreach (var renderer in m_SpriteRenderers)
                ChangeSpriteOpacity(renderer, TRANSPARENT_ALPHA);
        }
        if (m_Tilemaps != null)
        {
            foreach (var map in m_Tilemaps)
                ChangeTilemapOpacity(map, TRANSPARENT_ALPHA);
        }
    }

    private void FadeIn()
    {
        if (m_SpriteRenderers != null)
        {
            foreach (var renderer in m_SpriteRenderers)
                ChangeSpriteOpacity(renderer, VISIBLE_ALPHA);
        }
        if (m_Tilemaps != null)
        {
            foreach (var map in m_Tilemaps)
                ChangeTilemapOpacity(map, VISIBLE_ALPHA);
        }
    }

    private void ChangeSpriteOpacity(SpriteRenderer renderer, float targetAlpha)
    {
        Color color = renderer.color;
        renderer.color = new Color(color.r, color.g, color.b, Mathf.MoveTowards(color.a, targetAlpha, Time.deltaTime * 4));
    }

    private void ChangeTilemapOpacity(Tilemap map, float targetAlpha)
    {
        Color color = map.color;
        map.color = new Color(color.r, color.g, color.b, Mathf.MoveTowards(color.a, targetAlpha, Time.deltaTime * 4));
    }
}
