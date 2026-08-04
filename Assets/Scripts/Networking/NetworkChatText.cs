using System.Text;

/// <summary>
/// Length clamping for strings that travel inside a Fusion RPC.
/// </summary>
/// <remarks>
/// Fusion measures every RPC against a hard 512-byte ceiling (Fusion.RpcAttribute.MaxPayloadSize,
/// header included) and DROPS the whole message when the estimate goes over: the send silently
/// fails and only Fusion's own error log mentions it.
///
/// A plain <c>string</c> parameter is weaved as variable-length UTF-8 (4-byte length prefix plus
/// the encoded bytes, word-aligned), so the budget to respect is UTF-8 BYTES, not characters.
/// A NetworkString&lt;_N&gt; parameter instead always costs its FULL width, and _N counts 32-bit
/// words rather than characters — NetworkString&lt;_128&gt; is a fixed 516 bytes, over the limit
/// on its own.
///
/// One Vietnamese character with diacritics costs 3 UTF-8 bytes, which is why the character cap
/// below is the byte budget divided by 3.
/// </remarks>
public static class NetworkChatText
{
    /// <summary>Wire budget for a chat body: <see cref="MaxContentChars"/> worst-case characters.</summary>
    public const int MaxContentBytes = 384;

    /// <summary>Wire budget for a sender display name.</summary>
    public const int MaxSenderNameBytes = 64;

    /// <summary>Wire budget for an ISO-8601 timestamp ("O" round-trip format is 28 ASCII bytes).</summary>
    public const int MaxTimestampBytes = 32;

    /// <summary>
    /// Character cap to expose in the UI, so the player sees the limit while typing instead of
    /// discovering it as a message that was cut on everyone else's screen. Sized so that even an
    /// all-diacritics Vietnamese message still fits <see cref="MaxContentBytes"/>.
    /// </summary>
    public const int MaxContentChars = MaxContentBytes / 3;

    /// <summary>
    /// Clamp <paramref name="value"/> so its UTF-8 encoding fits <paramref name="maxBytes"/>,
    /// cutting on a character boundary and never splitting a surrogate pair.
    /// </summary>
    public static string ClampUtf8(string value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;

        int chars = 0;
        int bytes = 0;
        while (chars < value.Length)
        {
            // Step over a surrogate PAIR as a single unit: half a pair is not a character, and
            // it encodes as U+FFFD — that is how a truncated emoji becomes a black diamond.
            int step = char.IsHighSurrogate(value[chars]) && chars + 1 < value.Length ? 2 : 1;
            int size = Encoding.UTF8.GetByteCount(value, chars, step);
            if (bytes + size > maxBytes) break;

            bytes += size;
            chars += step;
        }

        return value.Substring(0, chars);
    }
}
