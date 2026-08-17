using System.Text;

// Initializes a new default instance of the NetworkChatText class.
public static class NetworkChatText
{
    public const int MaxContentBytes = 384;

    public const int MaxSenderNameBytes = 64;

    public const int MaxTimestampBytes = 32;

    public const int MaxContentChars = MaxContentBytes / 3;

    // Executes clamp utf8 operation.
    // Validates input parameters against null or empty values.
    public static string ClampUtf8(string value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;

        int chars = 0;
        int bytes = 0;
        while (chars < value.Length)
        {
            int step = char.IsHighSurrogate(value[chars]) && chars + 1 < value.Length ? 2 : 1;
            int size = Encoding.UTF8.GetByteCount(value, chars, step);
            if (bytes + size > maxBytes) break;

            bytes += size;
            chars += step;
        }

        return value.Substring(0, chars);
    }
}
