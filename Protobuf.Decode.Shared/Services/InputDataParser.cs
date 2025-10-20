using System.Globalization;
using System.Text;

namespace Protobuf.Decode.Shared.Services;

public static class InputDataParser
{
    private const double TextThreshold = 0.8;

    public static ReadOnlyMemory<byte> Parse(string? text)
    {
        if (TryParseHex(text, out var hexBytes))
        {
            return hexBytes;
        }

        if (TryParseBase64(text, out var base64Bytes))
        {
            return base64Bytes;
        }

        throw new InvalidOperationException("无法识别输入格式：请提供十六进制、\\xAA 或 Base64 编码的 Protobuf 数据。");
    }

    public static bool LooksLikeText(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return true;
        }

        int printable = 0;
        int total = Math.Min(data.Length, 1024);

        for (int i = 0; i < total; i++)
        {
            byte b = data[i];
            if (b == 0)
            {
                return false;
            }

            if (b is >= 32 and <= 126 || b == (byte)'\n' || b == (byte)'\r' || b == (byte)'\t')
            {
                printable++;
            }
        }

        return printable >= total * TextThreshold;
    }

    public static bool TryParseHex(string? text, out ReadOnlyMemory<byte> result)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            result = ReadOnlyMemory<byte>.Empty;
            return true;
        }

        var candidate = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            if (c == '\\' && i + 1 < text.Length && (text[i + 1] == 'x' || text[i + 1] == 'X'))
            {
                i++;
                continue;
            }

            if (c == '0' && i + 1 < text.Length && (text[i + 1] == 'x' || text[i + 1] == 'X'))
            {
                i++;
                continue;
            }

            if (Uri.IsHexDigit(c))
            {
                candidate.Append(c);
                continue;
            }

            result = default;
            return false;
        }

        if (candidate.Length == 0)
        {
            result = ReadOnlyMemory<byte>.Empty;
            return true;
        }

        if (candidate.Length % 2 != 0)
        {
            result = default;
            return false;
        }

        try
        {
            var buffer = Convert.FromHexString(candidate.ToString());
            result = buffer;
            return true;
        }
        catch (FormatException)
        {
            result = default;
            return false;
        }
    }

    public static bool TryParseBase64(string? text, out ReadOnlyMemory<byte> result)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            result = ReadOnlyMemory<byte>.Empty;
            return true;
        }

        var noWhitespace = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c))
            {
                noWhitespace.Append(c);
            }
        }

        if (noWhitespace.Length == 0)
        {
            result = ReadOnlyMemory<byte>.Empty;
            return true;
        }

        var candidate = noWhitespace.ToString();
        var normalized = candidate.Replace('-', '+').Replace('_', '/');

        int padding = normalized.Length % 4;
        if (padding != 0)
        {
            normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
        }

        try
        {
            var buffer = Convert.FromBase64String(normalized);
            result = buffer;
            return true;
        }
        catch (FormatException)
        {
            result = default;
            return false;
        }
    }
}

