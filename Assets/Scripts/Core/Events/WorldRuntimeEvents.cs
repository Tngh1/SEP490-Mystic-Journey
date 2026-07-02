using System;

public static class WorldRuntimeEvents
{
    public static event Action QuestsChanged;
    public static event Action LevelChanged;
    public static event Action<string> Message;

    // Raise khi player claim quest cuối cùng của một map (= questId là unlockQuestId của map tiếp theo)
    // Tham số: questId vừa được Claimed
    public static event Action<int> MapCompleted;

    public static void RaiseQuestsChanged() => QuestsChanged?.Invoke();
    public static void RaiseLevelChanged() => LevelChanged?.Invoke();
    public static void RaiseMessage(string message) => Message?.Invoke(message);
    public static void RaiseMapCompleted(int questId) => MapCompleted?.Invoke(questId);
}
