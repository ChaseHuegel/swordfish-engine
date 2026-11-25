using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WaywardBeyond.AssetStudio.Converters;

/// <summary>
///     Converts a hex color string to an Avalonia solid color brush for live swatches.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    /// <summary>
    ///     Shared singleton instance for XAML bindings.
    /// </summary>
    public static readonly HexToBrushConverter INSTANCE = new();

    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string text && Color.TryParse(text.Trim(), out var color))
        {
            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(Color.Parse("#64748B"));
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() ?? string.Empty;
    }
}
