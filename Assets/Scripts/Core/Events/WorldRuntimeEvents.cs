using System;

// Initializes a new default instance of the WorldRuntimeEvents class.
public static class WorldRuntimeEvents
{
    public static event Action QuestsChanged;
    public static event Action CurrencyChanged;
    public static event Action LevelChanged;
    public static event Action<string> Message;
    public static event Action<string> MapChanged;

    public static event Action<int> MapCompleted;

    // Executes raise quests changed operation.
    public static void RaiseQuestsChanged() => QuestsChanged?.Invoke();
    // Executes raise currency changed operation.
    public static void RaiseCurrencyChanged() => CurrencyChanged?.Invoke();
    // Executes raise level changed operation.
    public static void RaiseLevelChanged() => LevelChanged?.Invoke();
    // Executes raise message operation.
    public static void RaiseMessage(string message) => Message?.Invoke(message);
    // Executes raise map changed operation.
    public static void RaiseMapChanged(string mapName) => MapChanged?.Invoke(mapName);
    // Executes raise map completed operation.
    public static void RaiseMapCompleted(int questId) => MapCompleted?.Invoke(questId);
}
