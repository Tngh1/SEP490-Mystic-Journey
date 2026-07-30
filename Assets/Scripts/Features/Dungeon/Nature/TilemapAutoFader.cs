using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapAutoFader : MonoBehaviour
{
    [Header("Settings")]
    public float transparentAlpha = 0.3f;
    public float fadeSpeed = 4f;

    private Tilemap m_Tilemap;
    private bool m_IsFadingOut = false;
    private System.Collections.Generic.HashSet<PlayerEntity> m_PlayersBehind = new System.Collections.Generic.HashSet<PlayerEntity>();

    void Start()
    {
        m_Tilemap = GetComponent<Tilemap>();
    }

    void Update()
    {
        m_IsFadingOut = false;
        foreach (var player in PlayerEntity.AllPlayers)
        {
            if (player == null) continue;
            
            // We use the player's actual feet bounds to check the wall cell accurately
            // instead of their center transform, which causes overlapping errors!
            var col = player.GetComponentInChildren<Collider2D>();
            float feetY = col != null ? col.bounds.min.y : player.transform.position.y - 0.5f;
            Vector3 feetPos = player.transform.position;
            feetPos.y = feetY;

            Vector3Int playerCell = m_Tilemap.WorldToCell(feetPos);
            bool isBehind = m_Tilemap.HasTile(playerCell);
            
            if (isBehind)
            {
                // Ensure they are actually behind the visible part of the wall (which usually starts at cell center Y)
                // We only consider them 'in front' if they are at the bottom of the cell AND there is no wall directly below it.
                Vector3 cellCenter = m_Tilemap.GetCellCenterWorld(playerCell);
                if (feetY < cellCenter.y - 0.2f) 
                {
                    Vector3Int cellBelow = playerCell + new Vector3Int(0, -1, 0);
                    if (!m_Tilemap.HasTile(cellBelow))
                    {
                        isBehind = false; // Standing at the true base of the wall
                    }
                }
            }
            
            if (isBehind)
            {
                m_IsFadingOut = true;
                if (!m_PlayersBehind.Contains(player))
                {
                    m_PlayersBehind.Add(player);
                    player.AddWallOverlap();
                }
            }
            else
            {
                if (m_PlayersBehind.Contains(player))
                {
                    m_PlayersBehind.Remove(player);
                    player.RemoveWallOverlap();
                }
            }
        }
        // Cập nhật làm mờ
        Color color = m_Tilemap.color;
        float targetAlpha = m_IsFadingOut ? transparentAlpha : 1f;
        
        m_Tilemap.color = new Color(color.r, color.g, color.b, 
            Mathf.MoveTowards(color.a, targetAlpha, Time.deltaTime * fadeSpeed));
    }
    
    private void OnDestroy()
    {
        // Cleanup overlaps if this fader is destroyed
        foreach (var player in m_PlayersBehind)
        {
            if (player != null) player.RemoveWallOverlap();
        }
        m_PlayersBehind.Clear();
    }
}
