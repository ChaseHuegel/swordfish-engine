using System.Globalization;
using Avalonia.Data.Converters;
using WaywardBeyond.AssetStudio.Services;

namespace WaywardBeyond.AssetStudio.Converters;

/// <summary>
///     Converts an image file path into a cached Avalonia thumbnail bitmap for picker previews.
/// </summary>
public sealed class PathToThumbnailConverter : IValueConverter
{
    /// <summary>
    ///     Shared singleton instance for XAML bindings.
    /// </summary>
    public static readonly PathToThumbnailConverter INSTANCE = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string path ? ThumbnailCache.GetOrLoad(path) : null;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
