using UnityEngine;
using MysticJourney.Core.Services;

/// <summary>
/// Transitional wrapper around GameStateService.
/// Prefer GameStateService.Instance directly in new code.
/// </summary>
public static class WorldState
{
    private static GameStateService _service => GameStateService.Instance;

    public static bool HasCharacter
    {
        get => _service.HasCharacter;
        set => _service.HasCharacter = value;
    }

    public static int PlayerProfileId
    {
        get => _service.PlayerProfileId;
        set => _service.PlayerProfileId = value;
    }

    public static int PlayerLevel
    {
        get => _service.PlayerLevel;
        set => _service.PlayerLevel = value;
    }

    public static string PlayerName
    {
        get => _service.PlayerName;
        set => _service.PlayerName = value;
    }

    public static string PlayerClass
    {
        get => _service.PlayerClass;
        set => _service.PlayerClass = value;
    }

    public static string CurrentMapName
    {
        get => _service.CurrentMapName;
        set => _service.CurrentMapName = value;
    }

    public static Vector3 LastPosition
    {
        get => _service.LastPosition;
        set => _service.LastPosition = value;
    }

    public static void Reset() => _service.Reset();
    public static void LoadFromPlayerPrefs() => _service.LoadFromPlayerPrefs();
    public static void SaveToPlayerPrefs() => _service.SaveToPlayerPrefs();
}
