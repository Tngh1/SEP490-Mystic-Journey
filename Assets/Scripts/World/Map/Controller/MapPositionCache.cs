using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lưu last position của từng map trong session hiện tại.
/// - Lần đầu vào map: không có entry → dùng PlayerSpawn tag trong scene.
/// - Quay lại map cũ: có entry → dùng vị trí cuối cùng player đứng.
/// Cache bị reset khi thoát game (in-memory only, không persist qua session).
/// </summary>
public static class MapPositionCache
{
    private static readonly Dictionary<string, Vector3> _positions =
        new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Cập nhật vị trí của map hiện tại.</summary>
    public static void Save(string mapName, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(mapName)) return;
        _positions[mapName] = position;
    }

    /// <summary>
    /// Lấy vị trí lần cuối của map. 
    /// Trả về true nếu có; false nếu chưa từng vào map này trong session.
    /// </summary>
    public static bool TryGet(string mapName, out Vector3 position)
    {
        if (!string.IsNullOrWhiteSpace(mapName) && _positions.TryGetValue(mapName, out position))
            return true;

        position = Vector3.zero;
        return false;
    }

    /// <summary>Xóa toàn bộ cache (dùng khi logout).</summary>
    public static void Clear() => _positions.Clear();
}
