using System;

// Initializes a new default instance of the LoadingProgress class.
public static class LoadingProgress
{
    public static event Action<float, string> OnProgress;

    // Executes value operation.
    public static float Value { get; private set; }
    // Executes status operation.
    public static string Status { get; private set; } = string.Empty;

    // Executes reset operation.
    public static void Reset()
    {
        Value = 0f;
        Status = string.Empty;
    }

    // Executes report operation.
    public static void Report(float value, string status)
    {
        Value = value < 0f ? 0f : (value > 1f ? 1f : value);
        Status = status ?? string.Empty;
        OnProgress?.Invoke(Value, Status);
    }
}
