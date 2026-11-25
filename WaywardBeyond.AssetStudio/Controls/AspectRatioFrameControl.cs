using System;
using Avalonia;
using Avalonia.Controls;

namespace WaywardBeyond.AssetStudio.Controls;

/// <summary>Inscribes and centers its single child at a configurable width-to-height aspect ratio.</summary>
public sealed class AspectRatioFrameControl : Decorator
{
    /// <summary>Target width/height aspect ratio, or 0 to fill all available bounds without letterboxing.</summary>
    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<AspectRatioFrameControl, double>(nameof(AspectRatio), 0.0);

    static AspectRatioFrameControl()
    {
        AffectsMeasure<AspectRatioFrameControl>(AspectRatioProperty);
        AffectsArrange<AspectRatioFrameControl>(AspectRatioProperty);
    }

    /// <summary>Gets or sets the target width/height ratio (0 fills all available space).</summary>
    public double AspectRatio
    {
        get => GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child == null)
        {
            return default;
        }

        double ratio = AspectRatio;
        if (ratio <= 0.01 || double.IsNaN(ratio) || double.IsInfinity(ratio)
            || double.IsInfinity(availableSize.Width) || double.IsInfinity(availableSize.Height))
        {
            Child.Measure(availableSize);
            return Child.DesiredSize;
        }

        Size fitted = FitSize(availableSize, ratio);
        Child.Measure(fitted);
        return fitted;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child == null)
        {
            return finalSize;
        }

        double ratio = AspectRatio;
        if (ratio <= 0.01 || double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            Child.Arrange(new Rect(finalSize));
            return finalSize;
        }

        Size fitted = FitSize(finalSize, ratio);
        double x = Math.Max(0, (finalSize.Width - fitted.Width) * 0.5);
        double y = Math.Max(0, (finalSize.Height - fitted.Height) * 0.5);
        Child.Arrange(new Rect(x, y, fitted.Width, fitted.Height));
        return finalSize;
    }

    private static Size FitSize(Size bounds, double aspectRatio)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return new Size(0, 0);
        }

        double widthFromHeight = bounds.Height * aspectRatio;
        if (widthFromHeight <= bounds.Width)
        {
            return new Size(widthFromHeight, bounds.Height);
        }

        return new Size(bounds.Width, bounds.Width / aspectRatio);
    }
}
