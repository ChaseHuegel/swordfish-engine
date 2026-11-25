using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;

namespace WaywardBeyond.AssetStudio.Controls;

/// <summary>
///     Interactive horizontal HSV hue spectrum bar with click-and-drag thumb selection.
/// </summary>
public sealed class ColorHueBarControl : Control, ICustomHitTest
{
    /// <summary>
    ///     Styled property supplying the selected hue angle in degrees (0 to 360).
    /// </summary>
    public static readonly StyledProperty<double> HueProperty =
        AvaloniaProperty.Register<ColorHueBarControl, double>(
            nameof(Hue),
            defaultValue: 215.0,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    ///     Styled property configuring the track outline stroke brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<ColorHueBarControl, IBrush?>(nameof(BorderBrush));

    private static readonly LinearGradientBrush HUE_SPECTRUM_BRUSH = new()
    {
        StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        GradientStops =
        [
            new GradientStop(Color.FromRgb(255, 0, 0), 0.0),
            new GradientStop(Color.FromRgb(255, 255, 0), 1.0 / 6.0),
            new GradientStop(Color.FromRgb(0, 255, 0), 2.0 / 6.0),
            new GradientStop(Color.FromRgb(0, 255, 255), 3.0 / 6.0),
            new GradientStop(Color.FromRgb(0, 0, 255), 4.0 / 6.0),
            new GradientStop(Color.FromRgb(255, 0, 255), 5.0 / 6.0),
            new GradientStop(Color.FromRgb(255, 0, 0), 1.0),
        ],
    };

    private static readonly IBrush DEFAULT_BORDER_BRUSH = new SolidColorBrush(Color.FromArgb(120, 128, 138, 156));
    private static readonly Pen THUMB_SHADOW_PEN = new(new SolidColorBrush(Color.FromArgb(165, 0, 0, 0)), 1.4);
    private static readonly Pen THUMB_RING_PEN = new(Brushes.White, 2.0);

    private const double THUMB_RADIUS = 6.5;

    private bool _isDragging;

    static ColorHueBarControl()
    {
        AffectsRender<ColorHueBarControl>(HueProperty, BorderBrushProperty);
    }

    /// <summary>
    ///     Initializes the hue spectrum bar with a hand cursor.
    /// </summary>
    public ColorHueBarControl()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>
    ///     Raised when the user clicks or drags to choose a new hue angle.
    /// </summary>
    public event EventHandler? ValueEdited;

    /// <summary>
    ///     Selected hue angle in degrees (0 to 360).
    /// </summary>
    public double Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, Math.Clamp(value, 0.0, 360.0));
    }

    /// <summary>
    ///     Track outline stroke brush for theme-aware contrast.
    /// </summary>
    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <inheritdoc/>
    public bool HitTest(Point point)
    {
        return point.X >= 0 && point.Y >= 0 && point.X <= Bounds.Width && point.Y <= Bounds.Height;
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width < THUMB_RADIUS * 2 + 4 || height < 8)
        {
            return;
        }

        var trackHeight = Math.Min(10.0, height - 4.0);
        var trackY = (height - trackHeight) * 0.5;
        var trackRect = new Rect(0, trackY, width, trackHeight);
        var radius = trackHeight * 0.5;

        var borderPen = new Pen(BorderBrush ?? DEFAULT_BORDER_BRUSH, 1.0);
        context.DrawRectangle(HUE_SPECTRUM_BRUSH, borderPen, trackRect.Deflate(0.5), radius, radius);

        var usableWidth = width - THUMB_RADIUS * 2.0;
        var normalized = Math.Clamp(Hue / 360.0, 0.0, 1.0);
        var thumbX = THUMB_RADIUS + normalized * usableWidth;
        var center = new Point(thumbX, height * 0.5);
        var pureHue = StudioColorPickerControl.HsvToRgb(Hue, 1.0, 1.0);

        context.DrawEllipse(null, THUMB_SHADOW_PEN, center, THUMB_RADIUS + 0.8, THUMB_RADIUS + 0.8);
        context.DrawEllipse(new SolidColorBrush(pureHue), THUMB_RING_PEN, center, THUMB_RADIUS - 0.8, THUMB_RADIUS - 0.8);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isDragging = true;
        e.Pointer.Capture(this);
        UpdateFromPointer(e.GetPosition(this));
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDragging)
        {
            return;
        }

        UpdateFromPointer(e.GetPosition(this));
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;
    }

    private void UpdateFromPointer(Point position)
    {
        var usableWidth = Bounds.Width - THUMB_RADIUS * 2.0;
        if (usableWidth <= 0)
        {
            return;
        }

        var ratio = Math.Clamp((position.X - THUMB_RADIUS) / usableWidth, 0.0, 1.0);
        Hue = ratio * 360.0;
        ValueEdited?.Invoke(this, EventArgs.Empty);
    }
}
