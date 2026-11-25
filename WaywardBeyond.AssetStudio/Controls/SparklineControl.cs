using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WaywardBeyond.AssetStudio.Controls;

/// <summary>
///     Custom vector sparkline control visualizing a skill's XP progression curve.
/// </summary>
public sealed class SparklineControl : Control
{
    /// <summary>
    ///     Styled property binding the ordered numeric series to plot.
    /// </summary>
    public static readonly StyledProperty<IEnumerable<double>?> ValuesProperty =
        AvaloniaProperty.Register<SparklineControl, IEnumerable<double>?>(nameof(Values));

    /// <summary>
    ///     Styled property configuring the stroke accent brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush?>(nameof(AccentBrush));

    /// <summary>
    ///     Styled property configuring horizontal guide line stroke brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush?>(nameof(GridBrush));

    /// <summary>
    ///     Styled property configuring axis label foreground brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush?>(nameof(LabelBrush));

    private INotifyCollectionChanged? _subscribedCollection;

    static SparklineControl()
    {
        AffectsRender<SparklineControl>(ValuesProperty, AccentBrushProperty, GridBrushProperty, LabelBrushProperty);
    }

    /// <summary>
    ///     Ordered series of XP requirements per level.
    /// </summary>
    public IEnumerable<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>
    ///     Stroke brush for the curve and milestone markers.
    /// </summary>
    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    /// <summary>
    ///     Subtle gridline brush behind the chart.
    /// </summary>
    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    /// <summary>
    ///     Foreground brush for min/max axis labels.
    /// </summary>
    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty)
        {
            if (_subscribedCollection != null)
            {
                _subscribedCollection.CollectionChanged -= HandleCollectionChanged;
                _subscribedCollection = null;
            }

            if (change.NewValue is INotifyCollectionChanged observable)
            {
                _subscribedCollection = observable;
                _subscribedCollection.CollectionChanged += HandleCollectionChanged;
            }

            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        if (bounds.Width < 40 || bounds.Height < 30)
        {
            return;
        }

        var data = CollectPoints();
        if (data.Count < 2)
        {
            return;
        }

        var accent = AccentBrush ?? new SolidColorBrush(Color.Parse("#4F8CFF"));
        var grid = GridBrush ?? new SolidColorBrush(Color.Parse("#2A3244"));
        var labelBrush = LabelBrush ?? new SolidColorBrush(Color.Parse("#8B9BB4"));

        const double padLeft = 50;
        const double padRight = 14;
        const double padTop = 12;
        const double padBottom = 22;

        var plotWidth = Math.Max(10, bounds.Width - padLeft - padRight);
        var plotHeight = Math.Max(10, bounds.Height - padTop - padBottom);

        var minVal = double.MaxValue;
        var maxVal = double.MinValue;
        for (var i = 0; i < data.Count; i++)
        {
            if (data[i] < minVal)
            {
                minVal = data[i];
            }

            if (data[i] > maxVal)
            {
                maxVal = data[i];
            }
        }

        var span = Math.Max(1.0, maxVal - minVal);
        var gridPen = new Pen(grid, 1);

        // Draw 3 horizontal guide lines with right-aligned Y labels
        for (var step = 0; step <= 2; step++)
        {
            var ratio = step / 2.0;
            var y = padTop + plotHeight * (1.0 - ratio);
            context.DrawLine(gridPen, new Point(padLeft, y), new Point(padLeft + plotWidth, y));

            var tickVal = minVal + span * ratio;
            var formatted = new FormattedText(
                $"{tickVal:N0}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Liberation Sans"),
                10,
                labelBrush);
            context.DrawText(formatted, new Point(Math.Max(2, padLeft - formatted.Width - 6), y - formatted.Height / 2));
        }

        var points = new Point[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var x = padLeft + plotWidth * i / (data.Count - 1);
            var norm = (data[i] - minVal) / span;
            var y = padTop + plotHeight * (1.0 - norm);
            points[i] = new Point(x, y);
        }

        // Draw soft translucent area fill beneath curve
        var areaGeo = new StreamGeometry();
        using (var gc = areaGeo.Open())
        {
            gc.BeginFigure(new Point(points[0].X, padTop + plotHeight), true);
            for (var i = 0; i < points.Length; i++)
            {
                gc.LineTo(points[i]);
            }

            gc.LineTo(new Point(points[^1].X, padTop + plotHeight));
            gc.EndFigure(true);
        }

        var baseColor = accent is ISolidColorBrush solid ? solid.Color : Color.Parse("#4F8CFF");
        var fillBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.FromArgb(88, baseColor.R, baseColor.G, baseColor.B), 0),
                new GradientStop(Color.FromArgb(8, baseColor.R, baseColor.G, baseColor.B), 1),
            ],
        };
        context.DrawGeometry(fillBrush, null, areaGeo);

        // Draw curve line
        var lineGeo = new StreamGeometry();
        using (var gc = lineGeo.Open())
        {
            gc.BeginFigure(points[0], false);
            for (var i = 1; i < points.Length; i++)
            {
                gc.LineTo(points[i]);
            }

            gc.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(accent, 2.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), lineGeo);

        // Draw milestone points (all points for short curves; start, quartiles, and peak for dense 100-level curves)
        if (points.Length <= 24)
        {
            for (var i = 0; i < points.Length; i++)
            {
                context.DrawEllipse(accent, null, points[i], 3.0, 3.0);
            }
        }
        else
        {
            int[] milestones = [0, points.Length / 4, points.Length / 2, (points.Length * 3) / 4, points.Length - 1];
            for (var m = 0; m < milestones.Length; m++)
            {
                context.DrawEllipse(accent, null, points[milestones[m]], 3.2, 3.2);
            }
        }

        DrawAxisLabel(context, "Lv 1", new Point(padLeft, padTop + plotHeight + 5), labelBrush);
        DrawAxisLabel(context, $"Lv {data.Count}", new Point(padLeft + plotWidth - 28, padTop + plotHeight + 5), labelBrush);
    }

    private List<double> CollectPoints()
    {
        var list = new List<double>();
        if (Values == null)
        {
            return list;
        }

        foreach (var value in Values)
        {
            list.Add(value);
        }

        return list;
    }

    private void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private static void DrawAxisLabel(DrawingContext context, string text, Point origin, IBrush brush)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Liberation Sans"),
            10,
            brush);
        context.DrawText(formatted, origin);
    }
}
