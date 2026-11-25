using System.Globalization;
using Avalonia.Data.Converters;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.Converters;

/// <summary>
///     Formats a validation severity enum into its display title.
/// </summary>
public sealed class SeverityToLabelConverter : IValueConverter
{
    /// <summary>
    ///     Shared singleton instance for XAML bindings.
    /// </summary>
    public static readonly SeverityToLabelConverter INSTANCE = new();

    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ValidationSeverity severity)
        {
            return string.Empty;
        }

        return severity switch
        {
            ValidationSeverity.Fatal => "Fatal",
            ValidationSeverity.Broken => "Broken",
            ValidationSeverity.CrossPlatform => "Cross-platform",
            ValidationSeverity.Style => "Style",
            _ => string.Empty,
        };
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return ValidationSeverity.Style;
    }
}
