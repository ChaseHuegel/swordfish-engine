using System.Globalization;
using Avalonia.Data.Converters;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.Converters;

/// <summary>
///     Formats an asset provenance enum into its compact tree badge label.
/// </summary>
public sealed class ProvenanceToLabelConverter : IValueConverter
{
    /// <summary>
    ///     Shared singleton instance for XAML bindings.
    /// </summary>
    public static readonly ProvenanceToLabelConverter INSTANCE = new();

    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AssetProvenance provenance)
        {
            return string.Empty;
        }

        return provenance switch
        {
            AssetProvenance.Override => "ovr",
            AssetProvenance.Pack => "pack",
            AssetProvenance.Base => "base",
            AssetProvenance.Repo => "repo",
            _ => string.Empty,
        };
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return AssetProvenance.Base;
    }
}
