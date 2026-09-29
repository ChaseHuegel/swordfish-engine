using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Reef.Text;

/// <summary>
///     Formatting parsed out of a text run. Text is the visible characters and
///     Color maps each character to a color, or null to inherit the element color.
/// </summary>
public readonly struct RichText(string text, Vector4?[] color)
{
    public static readonly RichText Empty = new(string.Empty, []);

    public readonly string Text = text;
    public readonly Vector4?[] Color = color;

    /// <summary>
    ///     Strips "#" color control sequences out of a text window and records a color per remaining character.
    /// </summary>
    public static RichText Parse(string text, int start, int length)
    {
        int end = start + length;
        var builder = new StringBuilder(length);
        var colors = new List<Vector4?>(length);

        Vector4? current = null;
        var index = start;
        while (index < end)
        {
            char c = text[index];

            //  "##" is a literal hash.
            if (c == '#' && index + 1 < end && text[index + 1] == '#')
            {
                builder.Append('#');
                colors.Add(current);
                index += 2;
                continue;
            }

            //  A "#" control character starts a color run when a valid value follows.
            if (c == '#' && index + 1 < end && RichTextColor.TryParseValue(text, index + 1, end - (index + 1), out Vector4? value, out int consumed))
            {
                current = value;
                index += 1 + consumed;

                //  Consume a single trailing space so it is not rendered as leading whitespace.
                if (index < end && text[index] == ' ')
                {
                    index++;
                }
                continue;
            }

            builder.Append(c);
            colors.Add(current);
            index++;
        }

        return new RichText(builder.ToString(), colors.ToArray());
    }
}