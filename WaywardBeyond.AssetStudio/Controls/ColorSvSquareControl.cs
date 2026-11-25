using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;

namespace WaywardBeyond.AssetStudio.Controls;

/// <summary>
///     Interactive 2D HSV saturation and value gradient plane with click-and-drag thumb selection.
/// </summary>
public sealed class ColorSvSquareControl : Control, ICustomHitTest
{
    /// <summary>
    ///     Styled property supplying the current hue angle in degrees (0 to 360).
    /// </summary>
    public static readonly StyledProperty<double> HueProperty =
        AvaloniaProperty.Register<ColorSvSquareControl, double>(nameof(Hue), defaultValue: 215.0);

    /// <summary>
    ///     Styled property supplying the selected saturation component (0 to 1).
    /// </summary>
    public static readonly StyledProperty<double> SaturationProperty =
        AvaloniaProperty.Register<ColorSvSquareControl, double>(
            nameof(Saturation),
            defaultValue: 0.0,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    ///     Styled property supplying the selected value brightness component (0 to 1).
    /// </summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ColorSvSquareControl, double>(
            nameof(Value),
            defaultValue: 1.0,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    ///     Styled property configuring the outer frame stroke brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<ColorSvSquareControl, IBrush?>(nameof(BorderBrush));

    private static readonly LinearGradientBrush VALUE_OVERLAY_BRUSH = new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        [
            new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0),
            new GradientStop(Color.FromArgb(255, 0, 0, 0), 1.0),
        ],
    };

    private static readonly IBrush DEFAULT_BORDER_BRUSH = new SolidColorBrush(Color.FromArgb(120, 128, 138, 156));
    private static readonly Pen THUMB_SHADOW_PEN = new(new SolidColorBrush(Color.FromArgb(165, 0, 0, 0)), 1.5);
    private static readonly Pen THUMB_RING_PEN = new(Brushes.White, 2.0);

    private bool _isDragging;

    static ColorSvSquareControl()
    {
        AffectsRender<ColorSvSquareControl>(HueProperty, SaturationProperty, ValueProperty, BorderBrushProperty);
    }

    /// <summary>
    ///     Initializes the saturation-value square control with a crosshair cursor.
    /// </summary>
    public ColorSvSquareControl()
    {
        Cursor = new Cursor(StandardCursorType.Cross);
        ClipToBounds = true;
    }

    /// <summary>
    ///     Raised when the user clicks or drags to choose a new saturation and value pair.
    /// </summary>
    public event EventHandler? ValueEdited;

    /// <summary>
    ///     Current hue angle in degrees (0 to 360).
    /// </summary>
    public double Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    /// <summary>
    ///     Selected saturation component (0 to 1).
    /// </summary>
    public double Saturation
    {
        get => GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>
    ///     Selected value brightness component (0 to 1).
    /// </summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>
    ///     Outer frame stroke brush for theme-aware contrast.
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
        if (width < 4 || height < 4)
        {
            return;
        }

        var rect = new Rect(0, 0, width, height);
        var pureHue = StudioColorPickerControl.HsvToRgb(Hue, 1.0, 1.0);
        var satBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Colors.White, 0.0),
                new GradientStop(pureHue, 1.0),
            ],
        };

        var roundedClip = new RoundedRect(rect, 6.0);
        using (context.PushClip(roundedClip))
        {
            context.DrawRectangle(satBrush, null, rect);
            context.DrawRectangle(VALUE_OVERLAY_BRUSH, null, rect);
        }

        var borderPen = new Pen(BorderBrush ?? DEFAULT_BORDER_BRUSH, 1.0);
        context.DrawRectangle(null, borderPen, rect.Deflate(0.5), 6.0, 6.0);

        var thumbX = Math.Clamp(Saturation, 0.0, 1.0) * width;
        var thumbY = (1.0 - Math.Clamp(Value, 0.0, 1.0)) * height;
        var center = new Point(thumbX, thumbY);
        var currentColor = StudioColorPickerControl.HsvToRgb(Hue, Saturation, Value);

        context.DrawEllipse(null, THUMB_SHADOW_PEN, center, 7.2, 7.2);
        context.DrawEllipse(new SolidColorBrush(currentColor), THUMB_RING_PEN, center, 5.5, 5.5);
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
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var nextSat = Math.Clamp(position.X / Bounds.Width, 0.0, 1.0);
        var nextVal = 1.0 - Math.Clamp(position.Y / Bounds.Height, 0.0, 1.0);
        Saturation = nextSat;
        Value = nextVal;
        ValueEdited?.Invoke(this, EventArgs.Empty);
    }
}
