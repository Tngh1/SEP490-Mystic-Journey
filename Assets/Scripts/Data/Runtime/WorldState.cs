using UnityEngine;
using MysticJourney.Core.Services;

// Initializes a new default instance of the WorldState class.
public static class WorldState
{
    // Executes _service operation.
    private static GameStateService _service => GameStateService.Instance;

    // Executes has character operation.
    public static bool HasCharacter
    {
        get => _service.HasCharacter;
        set => _service.HasCharacter = value;
    }

    // Executes player profile id operation.
    public static int PlayerProfileId
    {
        get => _service.PlayerProfileId;
        set => _service.PlayerProfileId = value;
    }

    // Executes player level operation.
    public static int PlayerLevel
    {
        get => _service.PlayerLevel;
        set => _service.PlayerLevel = value;
    }

    // Executes player name operation.
    public static string PlayerName
    {
        get => _service.PlayerName;
        set => _service.PlayerName = value;
    }

    // Executes player class operation.
    public static string PlayerClass
    {
        get => _service.PlayerClass;
        set => _service.PlayerClass = value;
    }

    // Executes equipped skin id operation.
    public static int EquippedSkinId
    {
        get => _service.EquippedSkinId;
        set => _service.EquippedSkinId = value;
    }

    // Executes avatar url operation.
    public static string AvatarUrl
    {
        get => _service.AvatarUrl;
        set => _service.AvatarUrl = value;
    }

    // Executes current map name operation.
    public static string CurrentMapName
    {
        get => _service.CurrentMapName;
        set => _service.CurrentMapName = value;
    }

    // Executes highest unlocked map id operation.
    public static int HighestUnlockedMapId
    {
        get => _service.HighestUnlockedMapId;
        set => _service.HighestUnlockedMapId = value;
    }

    // Executes last position operation.
    public static Vector3 LastPosition
    {
        get => _service.LastPosition;
        set => _service.LastPosition = value;
    }

    // Executes reset operation.
    public static void Reset() => _service.Reset();
    // Executes load from player prefs operation.
    public static void LoadFromPlayerPrefs() => _service.LoadFromPlayerPrefs();
    // Executes save to player prefs operation.
    public static void SaveToPlayerPrefs() => _service.SaveToPlayerPrefs();
}
