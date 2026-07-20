using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapAutoFader : MonoBehaviour
{
    [Header("Settings")]
    public float transparentAlpha = 0.3f;
    public float fadeSpeed = 4f;

    private Tilemap m_Tilemap;
    private PlayerEntity m_Player;
    private bool m_IsFadingOut = false;

    void Start()
    {
        m_Tilemap = GetComponent<Tilemap>();
    }

    void Update()
    {
        // Tự động tìm Player nếu chưa có
        if (m_Player == null)
        {
            m_Player = PlayerEntity.Instance;
            if (m_Player == null) return;
        }

        // Lấy tọa độ của chân nhân vật trên lưới (Grid) của Tilemap
        Vector3Int playerCell = m_Tilemap.WorldToCell(m_Player.transform.position);

        // Kiểm tra xem tại vị trí chân nhân vật có viên gạch tường nào của Wall_Visual không?
        // (Nếu có gạch tức là nhân vật đang nấp sau phần ngọn của bức tường)
        m_IsFadingOut = m_Tilemap.HasTile(playerCell);

        // Cập nhật làm mờ
        Color color = m_Tilemap.color;
        float targetAlpha = m_IsFadingOut ? transparentAlpha : 1f;
        
        m_Tilemap.color = new Color(color.r, color.g, color.b, 
            Mathf.MoveTowards(color.a, targetAlpha, Time.deltaTime * fadeSpeed));
    }
}
