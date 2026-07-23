using System;

/// <summary>
/// Bus tĩnh báo tiến trình loading từ GameBootstrap sang UI. Không cần object trong scene:
/// GameBootstrap gọi Report(...) ở từng mốc, BootstrapLoadingUI (trong Bootstrap scene) lắng
/// nghe OnProgress để cập nhật thanh bar + status text. Reset() khi bắt đầu để dọn state cũ.
/// </summary>
public static class LoadingProgress
{
    /// <summary>Tiến trình 0..1 kèm nhãn trạng thái hiện tại.</summary>
    public static event Action<float, string> OnProgress;

    public static float Value { get; private set; }
    public static string Status { get; private set; } = string.Empty;

    public static void Reset()
    {
        Value = 0f;
        Status = string.Empty;
    }

    public static void Report(float value, string status)
    {
        Value = value < 0f ? 0f : (value > 1f ? 1f : value);
        Status = status ?? string.Empty;
        OnProgress?.Invoke(Value, Status);
    }
}
