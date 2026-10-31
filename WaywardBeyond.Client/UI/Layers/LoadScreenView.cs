using System.Numerics;
using System.Text;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Display data for the loading screen.</summary>
internal readonly record struct LoadScreenState(
    string Status,
    string Hint,
    float ContentFade,
    float StatusFade,
    float HintFade,
    int SignalX
);

/// <summary>Draws the full-screen loading state.</summary>
internal partial class LoadScreenView
{
    /// <summary>Returns the faded status text color.</summary>
    protected static Vector4 TextColor(float alpha)
    {
        return DynamicColors.Fade(DynamicColors.Text, alpha);
    }

    /// <summary>Returns the faded hint text color.</summary>
    protected static Vector4 MutedColor(float alpha)
    {
        return DynamicColors.Fade(DynamicColors.TextMuted, alpha);
    }

    /// <summary>Returns the faded signal line color.</summary>
    protected static Vector4 BorderColor(float alpha)
    {
        return DynamicColors.Fade(DynamicColors.Border, alpha);
    }

    /// <summary>Returns the faded moving segment color.</summary>
    protected static Vector4 AccentColor(float alpha)
    {
        return DynamicColors.Fade(DynamicColors.Accent, alpha);
    }

    /// <summary>Formats bracketed keys in a hint with rich-text color tags.</summary>
    protected static string FormatHint(string hint)
    {
        if (hint.IndexOf('[') < 0)
        {
            return hint;
        }

        var builder = new StringBuilder(hint.Length + 16);
        var inKey = false;
        for (var i = 0; i < hint.Length; i++)
        {
            char c = hint[i];
            if (c == '[' && !inKey)
            {
                builder.Append(DynamicColors.AccentRichText + " ");
                inKey = true;
            }

            builder.Append(c);
            if (c == ']' && inKey)
            {
                builder.Append("#R ");
                inKey = false;
            }
        }

        return builder.ToString();
    }
}
