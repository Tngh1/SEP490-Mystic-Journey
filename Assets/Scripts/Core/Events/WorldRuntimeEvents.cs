using System;

public static class WorldRuntimeEvents
{
    public static event Action QuestsChanged;
    public static event Action LevelChanged;
    public static event Action<string> Message;
    public static event Action<string> MapChanged;

    // Raised when a claimed quest unlocks a map slot.
    public static event Action<int> MapCompleted;

    public static void RaiseQuestsChanged() => QuestsChanged?.Invoke();
    public static void RaiseLevelChanged() => LevelChanged?.Invoke();
    public static void RaiseMessage(string message) => Message?.Invoke(message);
    public static void RaiseMapChanged(string mapName) => MapChanged?.Invoke(mapName);
    public static void RaiseMapCompleted(int questId) => MapCompleted?.Invoke(questId);
}
