using System.Globalization;
using System.Text;

namespace RandomSteamGame.Events;

public static class GamePickTelemetryName
{
    private const int MaxLength = 256;

    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        var pendingWhitespace = false;

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (TrySkipAnsiEscapeSequence(value, ref index, character))
            {
                continue;
            }

            if (character is '\r' or '\n' or '\t' || char.IsWhiteSpace(character))
            {
                pendingWhitespace = builder.Length > 0;
                continue;
            }

            var category = char.GetUnicodeCategory(character);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format)
            {
                continue;
            }

            if (pendingWhitespace)
            {
                if (builder.Length >= MaxLength)
                {
                    break;
                }

                builder.Append(' ');
                pendingWhitespace = false;
            }

            if (builder.Length >= MaxLength)
            {
                break;
            }

            builder.Append(character);

            if (builder.Length >= MaxLength)
            {
                break;
            }
        }

        return builder.Length == 0
            ? null
            : builder.ToString();
    }

    private static bool TrySkipAnsiEscapeSequence(
        string value,
        ref int index,
        char character)
    {
        if (character != '\u001b')
        {
            return false;
        }

        if (index + 1 >= value.Length || value[index + 1] != '[')
        {
            return true;
        }

        index += 2;
        while (index < value.Length && value[index] is < '@' or > '~')
        {
            index++;
        }

        return true;
    }
}
