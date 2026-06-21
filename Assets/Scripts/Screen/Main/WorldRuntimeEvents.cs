using System;

public static class WorldRuntimeEvents
{
    public static event Action QuestsChanged;
    public static event Action LevelChanged;
    public static event Action<string> Message;

    public static void RaiseQuestsChanged() => QuestsChanged?.Invoke();
    public static void RaiseLevelChanged() => LevelChanged?.Invoke();
    public static void RaiseMessage(string message) => Message?.Invoke(message);
}
