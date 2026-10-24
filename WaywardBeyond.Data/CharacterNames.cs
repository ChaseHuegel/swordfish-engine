using System.Text;

namespace WaywardBeyond.Data;

/// <summary>Limits and normalization for the client-supplied character display name.</summary>
public static class CharacterNames
{
    /// <summary>
    /// The most characters a display name may carry. The longest name format composes five parts;
    /// at a 24-character part this still leaves headroom.
    /// </summary>
    public const int MAX_LENGTH = 128;

    /// <summary>
    /// Normalizes a display name: trims surrounding whitespace, replaces control characters with a
    /// single space, and truncates to <see cref="MAX_LENGTH"/>. Null and whitespace yield an empty name.
    /// </summary>
    public static string Clamp(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        string trimmed = name.Trim();
        if (trimmed.Length <= MAX_LENGTH && !ContainsControlCharacter(trimmed))
        {
            return trimmed;
        }

        var builder = new StringBuilder(MAX_LENGTH);
        var lastWasSpace = false;
        foreach (char character in trimmed)
        {
            char value = char.IsControl(character) ? ' ' : character;
            if (value == ' ')
            {
                if (lastWasSpace)
                {
                    continue;
                }

                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }

            builder.Append(value);
            if (builder.Length == MAX_LENGTH)
            {
                break;
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static bool ContainsControlCharacter(string value)
    {
        foreach (char character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}
