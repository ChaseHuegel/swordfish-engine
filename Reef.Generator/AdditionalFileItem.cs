using System;
using Microsoft.CodeAnalysis.Text;

namespace Reef.Generator;

internal sealed class AdditionalFileItem : IEquatable<AdditionalFileItem>
{
    public readonly string Path;
    public readonly string Text;
    public readonly SourceText SourceText;

    public AdditionalFileItem(string path, string text)
    {
        Path = path ?? string.Empty;
        Text = text ?? string.Empty;
        SourceText = SourceText.From(Text);
    }

    public bool Equals(AdditionalFileItem? other)
    {
        if (other == null)
        {
            return false;
        }

        return string.Equals(Path, other.Path, StringComparison.Ordinal)
               && string.Equals(Text, other.Text, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is AdditionalFileItem other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(Path) * 397) ^ StringComparer.Ordinal.GetHashCode(Text);
        }
    }
}
