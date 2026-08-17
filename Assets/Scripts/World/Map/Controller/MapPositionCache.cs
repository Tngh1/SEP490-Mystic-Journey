using System.Collections.Generic;
using UnityEngine;

// Initializes a new default instance of the MapPositionCache class.
public static class MapPositionCache
{
    private static readonly Dictionary<string, Vector3> _positions =
        new(System.StringComparer.OrdinalIgnoreCase);

    // Executes save operation.
    // Validates input parameters against null or empty values.
    // Evaluates conditions and returns a boolean result.
    public static void Save(string mapName, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(mapName)) return;
        _positions[mapName] = position;
    }

    // Executes try get operation.
    // Validates input parameters against null or empty values.
    public static bool TryGet(string mapName, out Vector3 position)
    {
        if (!string.IsNullOrWhiteSpace(mapName) && _positions.TryGetValue(mapName, out position))
            return true;

        position = Vector3.zero;
        return false;
    }

    // Executes clear operation.
    public static void Clear() => _positions.Clear();
}
