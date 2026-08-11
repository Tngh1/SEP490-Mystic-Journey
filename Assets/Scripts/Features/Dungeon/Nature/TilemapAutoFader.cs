using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TilemapAutoFader : MonoBehaviour
{
    [Header("Settings")]
    public float transparentAlpha = 0.3f;
    public float fadeSpeed = 4f;

    private Tilemap m_Tilemap;
    private bool m_IsFadingOut = false;
    private HashSet<PlayerEntity> m_PlayersBehind = new HashSet<PlayerEntity>();

    // Cache Collider2D per player — tránh GetComponentInChildren mỗi frame
    private static Dictionary<PlayerEntity, Collider2D> s_ColliderCache = new Dictionary<PlayerEntity, Collider2D>();

    void Start()
    {
        m_Tilemap = GetComponent<Tilemap>();
    }

    /// <summary>
    /// Lấy Collider2D của player từ cache. Chỉ gọi GetComponentInChildren
    /// lần đầu tiên hoặc khi cache bị invalidate (player null).
    /// </summary>
    private static Collider2D GetPlayerCollider(PlayerEntity player)
    {
        if (s_ColliderCache.TryGetValue(player, out var col))
        {
            // Nếu collider bị destroy (scene change, respawn...) thì refresh
            if (col != null) return col;
            s_ColliderCache.Remove(player);
        }
        col = player.GetComponentInChildren<Collider2D>();
        s_ColliderCache[player] = col;
        return col;
    }

    void Update()
    {
        m_IsFadingOut = false;
        var allPlayers = PlayerEntity.AllPlayers;
        for (int i = 0; i < allPlayers.Count; i++)
        {
            var player = allPlayers[i];
            if (player == null) continue;

            // Dùng cached collider — không gọi GetComponentInChildren mỗi frame
            var col = GetPlayerCollider(player);
            float feetY = col != null ? col.bounds.min.y : player.transform.position.y - 0.5f;
            Vector3 feetPos = player.transform.position;
            feetPos.y = feetY;

            Vector3Int playerCell = m_Tilemap.WorldToCell(feetPos);
            bool isBehind = m_Tilemap.HasTile(playerCell);

            if (isBehind)
            {
                // Chỉ tính là đứng sau wall nếu feet thực sự nằm trong tile
                // và không có tile nào ngay bên dưới (base of wall)
                Vector3 cellCenter = m_Tilemap.GetCellCenterWorld(playerCell);
                if (feetY < cellCenter.y - 0.2f)
                {
                    Vector3Int cellBelow = playerCell + new Vector3Int(0, -1, 0);
                    if (!m_Tilemap.HasTile(cellBelow))
                        isBehind = false;
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
        // Cập nhật làm mờ — chỉ ghi color khi alpha thực sự thay đổi, tránh dirty TilemapRenderer mỗi frame
        Color color = m_Tilemap.color;
        float targetAlpha = m_IsFadingOut ? transparentAlpha : 1f;
        float newAlpha = Mathf.MoveTowards(color.a, targetAlpha, Time.deltaTime * fadeSpeed);

        if (Mathf.Abs(color.a - newAlpha) > 0.001f)
        {
            m_Tilemap.color = new Color(color.r, color.g, color.b, newAlpha);
        }
    }
    
    private void OnDestroy()
    {
        // Cleanup overlaps nếu fader bị destroy
        foreach (var player in m_PlayersBehind)
        {
            if (player != null) player.RemoveWallOverlap();
        }
        m_PlayersBehind.Clear();
    }

    /// <summary>
    /// Gọi khi một PlayerEntity bị destroy để xoá cache tránh memory leak.
    /// PlayerEntity.OnDisable sẽ gọi qua đây.
    /// </summary>
    public static void InvalidatePlayerCache(PlayerEntity player)
    {
        s_ColliderCache.Remove(player);
    }
}
