using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Rendering;

namespace WaywardBeyond.AssetStudio.Controls;

/// <summary>
///     Preview viewport host rendering crisp unlit software surfaces with interactive orbit, pan, and zoom fallback.
/// </summary>
public sealed class PreviewViewportControl : Control, ICustomHitTest
{
    private const double MIN_INTERACTIVE_ZOOM = 0.5;
    private const double MAX_INTERACTIVE_ZOOM = 3.0;

    private static readonly Cursor _handCursor = new(StandardCursorType.Hand);

    private string? _cachedSpritePath;
    private DateTime _cachedSpriteWriteUtc;
    private Bitmap? _cachedSpriteBitmap;

    private string? _cachedMaterialPath;
    private DateTime _cachedMaterialWriteUtc;
    private Bitmap? _cachedMaterialBitmap;

    private string? _cachedPaintPath;
    private DateTime _cachedPaintWriteUtc;
    private Bitmap? _cachedPaintBitmap;

    private double _interactiveYawOffset;
    private double _interactivePitchOffset;
    private double _interactivePanX;
    private double _interactivePanY;
    private double _interactiveZoom = 1.0;
    private bool _isDragging;
    private bool _isPanning;
    private Point _lastPointerPosition;

    /// <summary>
    ///     Styled property supplying the active asset preview descriptor.
    /// </summary>
    public static readonly StyledProperty<PreviewRenderDescriptor> DescriptorProperty =
        AvaloniaProperty.Register<PreviewViewportControl, PreviewRenderDescriptor>(nameof(Descriptor));

    /// <summary>
    ///     Styled property supplying the viewport text label foreground brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<PreviewViewportControl, IBrush?>(nameof(LabelBrush));

    /// <summary>
    ///     Styled property supplying subtle viewport gridline brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<PreviewViewportControl, IBrush?>(nameof(GridBrush));

    static PreviewViewportControl()
    {
        AffectsRender<PreviewViewportControl>(DescriptorProperty, LabelBrushProperty, GridBrushProperty);
    }

    /// <summary>
    ///     Initializes the fallback preview control with interactive pointer support.
    /// </summary>
    public PreviewViewportControl()
    {
        Cursor = _handCursor;
    }

    /// <summary>
    ///     Active preview descriptor describing what surface to render.
    /// </summary>
    public PreviewRenderDescriptor Descriptor
    {
        get => GetValue(DescriptorProperty);
        set => SetValue(DescriptorProperty, value);
    }

    /// <summary>
    ///     Foreground brush used for overlay labels inside the viewport.
    /// </summary>
    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    /// <summary>
    ///     Subtle accent grid brush for isometric platform lines.
    /// </summary>
    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    /// <inheritdoc/>
    public bool HitTest(Point point) =>
        point.X >= 0 && point.Y >= 0 && point.X <= Bounds.Width && point.Y <= Bounds.Height;

    /// <summary>
    ///     Resets interactive 2D orbit, pan, and zoom offsets to their defaults.
    /// </summary>
    public void ResetInteractiveView()
    {
        _interactiveYawOffset = 0;
        _interactivePitchOffset = 0;
        _interactivePanX = 0;
        _interactivePanY = 0;
        _interactiveZoom = 1.0;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != DescriptorProperty)
        {
            return;
        }

        PreviewRenderDescriptor oldDesc = change.GetOldValue<PreviewRenderDescriptor>();
        PreviewRenderDescriptor newDesc = change.GetNewValue<PreviewRenderDescriptor>();
        Cursor = newDesc.Mode == PreviewSurfaceMode.DataDocument ? Cursor.Default : _handCursor;

        if (!string.Equals(oldDesc.Title, newDesc.Title, StringComparison.Ordinal) || oldDesc.Mode != newDesc.Mode)
        {
            _interactiveYawOffset = 0;
            _interactivePitchOffset = 0;
            _interactivePanX = 0;
            _interactivePanY = 0;
            _interactiveZoom = 1.0;
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ClearSpriteBitmapCache();
        ClearMaterialBitmapCache();
        ClearPaintBitmapCache();
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Descriptor.Mode == PreviewSurfaceMode.DataDocument)
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            _isDragging = false;
            _isPanning = false;
            e.Pointer.Capture(null);
            ResetInteractiveView();
            e.Handled = true;
            return;
        }

        PointerPoint point = e.GetCurrentPoint(this);
        PointerPointProperties props = point.Properties;
        bool isPanGesture = props.IsRightButtonPressed
            || props.IsMiddleButtonPressed
            || (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift));

        if (isPanGesture)
        {
            _isPanning = true;
            _isDragging = false;
        }
        else if (props.IsLeftButtonPressed)
        {
            _isDragging = true;
            _isPanning = false;
        }
        else
        {
            return;
        }

        _lastPointerPosition = point.Position;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDragging && !_isPanning)
        {
            return;
        }

        Point current = e.GetPosition(this);
        double dx = current.X - _lastPointerPosition.X;
        double dy = current.Y - _lastPointerPosition.Y;
        _lastPointerPosition = current;

        if (_isPanning)
        {
            double maxPanX = Math.Max(40.0, Bounds.Width * 0.6);
            double maxPanY = Math.Max(40.0, Bounds.Height * 0.6);
            _interactivePanX = Math.Clamp(_interactivePanX + dx, -maxPanX, maxPanX);
            _interactivePanY = Math.Clamp(_interactivePanY + dy, -maxPanY, maxPanY);
        }
        else
        {
            _interactiveYawOffset = (_interactiveYawOffset + (dx * 0.7)) % 360.0;
            _interactivePitchOffset = Math.Clamp(_interactivePitchOffset + (dy * 0.45), -45.0, 45.0);
        }

        InvalidateVisual();
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isDragging && !_isPanning)
        {
            return;
        }

        _isDragging = false;
        _isPanning = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;
        _isPanning = false;
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Descriptor.Mode == PreviewSurfaceMode.DataDocument)
        {
            return;
        }

        double delta = e.Delta.Y;
        if (Math.Abs(delta) <= 1e-5 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            delta = e.Delta.X;
        }

        if (Math.Abs(delta) > 1e-5)
        {
            double clampedDelta = Math.Clamp(delta, -10.0, 10.0);
            _interactiveZoom = Math.Clamp(_interactiveZoom * Math.Pow(1.12, clampedDelta), MIN_INTERACTIVE_ZOOM, MAX_INTERACTIVE_ZOOM);
            InvalidateVisual();
        }

        e.Handled = true;
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        if (bounds.Width < 32 || bounds.Height < 32)
        {
            return;
        }

        var desc = Descriptor;
        var labelBrush = LabelBrush ?? new SolidColorBrush(Color.Parse("#94A3B8"));
        var gridBrush = GridBrush ?? new SolidColorBrush(Color.Parse("#2A3244"));
        var accentColor = ParseColor(desc.AccentHex, Color.Parse("#4F8CFF"));

        DrawSubtleBackdropGrid(context, bounds, gridBrush);

        double cx = bounds.Width * 0.5;
        double cy = bounds.Height * 0.5;
        double zoom = desc.Mode == PreviewSurfaceMode.DataDocument ? 1.0 : _interactiveZoom;
        double panX = desc.Mode == PreviewSurfaceMode.DataDocument ? 0.0 : _interactivePanX;
        double panY = desc.Mode == PreviewSurfaceMode.DataDocument ? 0.0 : _interactivePanY;
        double effectiveOrbit = desc.OrbitAngleDegrees + _interactiveYawOffset;
        double effectivePitch = _interactivePitchOffset;

        var viewTransform = new Matrix(
            zoom,
            0,
            0,
            zoom,
            cx + panX - (cx * zoom),
            cy + panY - (cy * zoom));

        using (context.PushClip(new Rect(0, 0, bounds.Width, bounds.Height)))
        using (context.PushTransform(viewTransform))
        {
            switch (desc.Mode)
            {
                case PreviewSurfaceMode.BrickShapeGrid:
                    DrawShapeGrid(context, bounds, accentColor, desc.HasMissingTexture, desc.Orientation, labelBrush);
                    break;
                case PreviewSurfaceMode.BrickConnectedShowcase:
                    DrawConnectedBrickShowcase(context, bounds, accentColor, desc.HasMissingTexture, desc.Orientation, effectiveOrbit, effectivePitch);
                    break;
                case PreviewSurfaceMode.PaintSwatch:
                    DrawPaintSwatch(context, bounds, accentColor, desc.IsTexturePaint, desc.PaintTexturePath, labelBrush);
                    break;
                case PreviewSurfaceMode.BodySprite:
                    DrawBodySprite(context, bounds, accentColor, desc.SpriteTexturePath);
                    break;
                case PreviewSurfaceMode.BlueprintVoxel:
                    DrawBlueprintCluster(context, bounds, effectiveOrbit, effectivePitch);
                    break;
                case PreviewSurfaceMode.MaterialPreview:
                    DrawMaterialPreview(
                        context,
                        bounds,
                        accentColor,
                        desc.MaterialTexturePath,
                        desc.MaterialShape,
                        effectiveOrbit,
                        effectivePitch);
                    break;
                case PreviewSurfaceMode.ItemViewModel:
                case PreviewSurfaceMode.ItemWorldModel:
                    DrawItemPreview(context, bounds, accentColor, desc.Mode == PreviewSurfaceMode.ItemWorldModel, effectiveOrbit, effectivePitch, labelBrush);
                    break;
                case PreviewSurfaceMode.DataDocument:
                    DrawDataDocumentPlaceholder(context, bounds, desc.Subtitle, labelBrush);
                    break;
                default:
                    DrawSingleBrick(context, bounds, accentColor, desc.HasMissingTexture, desc.ShapeName, desc.Orientation, effectiveOrbit, effectivePitch);
                    break;
            }
        }

        // Top-left crisp telemetry pill (stays pinned above pan/zoom transform)
        if (!string.IsNullOrWhiteSpace(desc.Subtitle))
        {
            var tag = new FormattedText(
                desc.Subtitle,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Liberation Mono, Monospace"),
                10,
                labelBrush);
            context.DrawText(tag, new Point(8, 6));
        }
    }

    private static void DrawSubtleBackdropGrid(DrawingContext context, Rect bounds, IBrush gridBrush)
    {
        var pen = new Pen(gridBrush, 1);
        var step = 22.0;
        for (var x = step; x < bounds.Width; x += step)
        {
            context.DrawLine(pen, new Point(x, 0), new Point(x, bounds.Height));
        }

        for (var y = step; y < bounds.Height; y += step)
        {
            context.DrawLine(pen, new Point(0, y), new Point(bounds.Width, y));
        }
    }

    private static void DrawTransparencyCheckerboard(DrawingContext context, Rect rect, double tileSize = 8.0)
    {
        var lightBrush = new SolidColorBrush(Color.Parse("#334155"));
        var darkBrush = new SolidColorBrush(Color.Parse("#1E293B"));

        using (context.PushClip(rect))
        {
            context.DrawRectangle(darkBrush, null, rect);
            int cols = (int)Math.Ceiling(rect.Width / tileSize);
            int rows = (int)Math.Ceiling(rect.Height / tileSize);
            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    if (((row + col) & 1) == 0)
                    {
                        var tile = new Rect(
                            rect.X + (col * tileSize),
                            rect.Y + (row * tileSize),
                            tileSize,
                            tileSize);
                        context.DrawRectangle(lightBrush, null, tile);
                    }
                }
            }
        }
    }

    private static void DrawSingleBrick(
        DrawingContext context,
        Rect bounds,
        Color tint,
        bool missingTexture,
        string shapeName,
        BrickOrientation orientation,
        double orbitOffset,
        double pitchOffset)
    {
        var cx = bounds.Width * 0.5;
        var cy = bounds.Height * 0.54;
        var size = Math.Min(bounds.Width, bounds.Height) * 0.28;
        var orbitRad = orbitOffset * (Math.PI / 180.0);
        var skew = ((int)orientation * 2.5) + (Math.Sin(orbitRad) * size * 0.34);
        var pitchShift = Math.Clamp(pitchOffset, -45.0, 45.0) * 0.008 * size;

        var topColor = missingTexture ? Color.Parse("#E11D8F") : Lighten(tint, 1.18);
        var leftColor = missingTexture ? Color.Parse("#9D176D") : Darken(tint, 0.82);
        var rightColor = missingTexture ? Color.Parse("#2B0A22") : Darken(tint, 0.62);

        var heightFactor = string.Equals(shapeName, "Slab", StringComparison.OrdinalIgnoreCase)
            ? 0.55
            : (string.Equals(shapeName, "Plate", StringComparison.OrdinalIgnoreCase) ? 0.26 : 1.0);
        var topLift = size * heightFactor;

        var top = new Point(cx + (skew * 0.4), cy - topLift - pitchShift);
        var right = new Point(cx + (size * 1.15), cy - (topLift * 0.45) - (pitchShift * 0.3));
        var center = new Point(cx + (skew * 0.2), cy + (size * 0.12) + (pitchShift * 0.4));
        var left = new Point(cx - (size * 1.15), cy - (topLift * 0.45) - (pitchShift * 0.3));
        var bottom = new Point(cx + (skew * 0.2), cy + (size * 1.08) + (pitchShift * 0.3));
        var bottomLeft = new Point(cx - (size * 1.15), cy + (size * 0.54));
        var bottomRight = new Point(cx + (size * 1.15), cy + (size * 0.54));

        var edgePen = new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1.1);

        DrawPolygon(context, [top, right, center, left], new SolidColorBrush(topColor), edgePen);
        DrawPolygon(context, [left, center, bottom, bottomLeft], new SolidColorBrush(leftColor), edgePen);
        DrawPolygon(context, [right, center, bottom, bottomRight], new SolidColorBrush(rightColor), edgePen);

        if (missingTexture)
        {
            var badgeRect = new Rect(cx - 46, cy - 10, 92, 20);
            context.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(220, 24, 8, 22)),
                new Pen(new SolidColorBrush(Color.Parse("#F472B6")), 1),
                badgeRect,
                4,
                4);
            var text = new FormattedText(
                "MISSING LAYER",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Liberation Mono, Monospace"),
                9.5,
                new SolidColorBrush(Color.Parse("#FBCFE8")));
            context.DrawText(text, new Point(cx - (text.Width / 2), cy - (text.Height / 2)));
        }
        else if (string.Equals(shapeName, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            var nozzleCenter = new Point(cx + (size * 0.55), cy + (size * 0.32));
            context.DrawEllipse(
                new SolidColorBrush(Color.Parse("#0F172A")),
                new Pen(new SolidColorBrush(Color.Parse("#60A5FA")), 1.5),
                nozzleCenter,
                size * 0.24,
                size * 0.34);
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(180, 96, 165, 250)),
                null,
                nozzleCenter,
                size * 0.11,
                size * 0.17);
        }
    }

    private static void DrawConnectedBrickShowcase(
        DrawingContext context,
        Rect bounds,
        Color tint,
        bool missingTexture,
        BrickOrientation orientation,
        double orbitOffset,
        double pitchOffset)
    {
        const int cols = ConnectedBrickShowcaseBuilder.SHOWCASE_WIDTH;
        const int rows = ConnectedBrickShowcaseBuilder.SHOWCASE_HEIGHT;

        double availableW = bounds.Width - 36;
        double availableH = bounds.Height - 44;
        double cell = Math.Max(10.0, Math.Min(availableW / (cols + 0.5), availableH / (rows + 0.5)));
        double totalW = cols * cell;
        double totalH = rows * cell;
        double originX = (bounds.Width - totalW) * 0.5;
        double originY = ((bounds.Height - totalH) * 0.5) + 10.0;

        double depthX = (cell * 0.24) + (Math.Sin(orbitOffset * (Math.PI / 180.0)) * cell * 0.12) + (((int)orientation - 1.5) * 0.4);
        double depthY = -(cell * 0.20) - (Math.Clamp(pitchOffset, -45.0, 45.0) * 0.003 * cell);

        var frontBrush = new SolidColorBrush(missingTexture ? Color.Parse("#C026D3") : tint);
        var topBrush = new SolidColorBrush(missingTexture ? Color.Parse("#E879F9") : Lighten(tint, 1.22));
        var sideBrush = new SolidColorBrush(missingTexture ? Color.Parse("#581C87") : Darken(tint, 0.68));
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(210, 241, 245, 249)), 1.4);
        var seamPen = new Pen(new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)), 0.65);

        // Draw back-to-front (top rows first, right-to-left or left-to-right based on depthX sign) so extrusions layer cleanly.
        var layout = ConnectedBrickShowcaseBuilder.ShowcaseLayout;
        for (var i = 0; i < layout.Count; i++)
        {
            var entry = layout[i];
            int gx = entry.Coords.X;
            int gy = (rows - 1) - entry.Coords.Y; // y=4 is top row, y=0 is bottom row
            byte mask = entry.ExpectedFrontMask;

            double x0 = originX + (gx * cell);
            double y0 = originY + (gy * cell);
            double x1 = x0 + cell;
            double y1 = y0 + cell;

            // Top extrusion only when no neighbor above (Up bit = 1)
            if ((mask & 1) == 0)
            {
                DrawPolygon(
                    context,
                    [new Point(x0, y0), new Point(x1, y0), new Point(x1 + depthX, y0 + depthY), new Point(x0 + depthX, y0 + depthY)],
                    topBrush,
                    seamPen);
            }

            // Right extrusion when depthX >= 0 and no right neighbor (Right bit = 4), or left extrusion when depthX < 0 and no left neighbor (Left bit = 2)
            if (depthX >= 0 && (mask & 4) == 0)
            {
                DrawPolygon(
                    context,
                    [new Point(x1, y0), new Point(x1 + depthX, y0 + depthY), new Point(x1 + depthX, y1 + depthY), new Point(x1, y1)],
                    sideBrush,
                    seamPen);
            }
            else if (depthX < 0 && (mask & 2) == 0)
            {
                DrawPolygon(
                    context,
                    [new Point(x0, y0), new Point(x0 + depthX, y0 + depthY), new Point(x0 + depthX, y1 + depthY), new Point(x0, y1)],
                    sideBrush,
                    seamPen);
            }

            // Front face tile
            var faceRect = new Rect(x0, y0, cell, cell);
            context.DrawRectangle(frontBrush, null, faceRect);

            // Subtle inner connected grid guide on connected edges; crisp border outline ONLY on unconnected edges
            context.DrawLine((mask & 1) == 0 ? borderPen : seamPen, new Point(x0, y0), new Point(x1, y0));
            context.DrawLine((mask & 2) == 0 ? borderPen : seamPen, new Point(x0, y0), new Point(x0, y1));
            context.DrawLine((mask & 4) == 0 ? borderPen : seamPen, new Point(x1, y0), new Point(x1, y1));
            context.DrawLine((mask & 8) == 0 ? borderPen : seamPen, new Point(x0, y1), new Point(x1, y1));
        }
    }

    private static void DrawShapeGrid(
        DrawingContext context,
        Rect bounds,
        Color tint,
        bool missingTexture,
        BrickOrientation orientation,
        IBrush labelBrush)
    {
        string[] shapes = ["Block", "Slab", "Stair", "Slope", "Column", "Plate"];
        const int cols = 3;
        const int rows = 2;
        var padX = 8.0;
        var padTop = 24.0;
        var cellW = (bounds.Width - (padX * 2)) / cols;
        var cellH = (bounds.Height - padTop - 6) / rows;
        var flip = orientation == BrickOrientation.East || orientation == BrickOrientation.South ? -1.0 : 1.0;

        var topBrush = new SolidColorBrush(missingTexture ? Color.Parse("#E11D8F") : Lighten(tint, 1.18));
        var leftBrush = new SolidColorBrush(missingTexture ? Color.Parse("#9D176D") : Darken(tint, 0.80));
        var rightBrush = new SolidColorBrush(missingTexture ? Color.Parse("#50073C") : Darken(tint, 0.58));
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(95, 255, 255, 255)), 0.85);

        for (var i = 0; i < shapes.Length; i++)
        {
            var col = i % cols;
            var row = i / cols;
            var rect = new Rect(padX + (col * cellW), padTop + (row * cellH), cellW - 4, cellH - 4);
            var cx = rect.X + (rect.Width * 0.5);
            var cy = rect.Y + (rect.Height * 0.45);
            var r = Math.Min(rect.Width, rect.Height) * 0.25;

            switch (shapes[i])
            {
                case "Stair":
                    DrawMiniStair(context, cx, cy, r, flip, topBrush, leftBrush, rightBrush, pen);
                    break;
                case "Slope":
                    DrawMiniSlope(context, cx, cy, r, flip, topBrush, leftBrush, rightBrush, pen);
                    break;
                case "Slab":
                    DrawMiniBox(context, cx, cy + (r * 0.22), r, 0.50, 1.0, topBrush, leftBrush, rightBrush, pen);
                    break;
                case "Plate":
                    DrawMiniBox(context, cx, cy + (r * 0.40), r, 0.22, 1.0, topBrush, leftBrush, rightBrush, pen);
                    break;
                case "Column":
                    DrawMiniBox(context, cx, cy + (r * 0.02), r, 1.05, 0.48, topBrush, leftBrush, rightBrush, pen);
                    break;
                default:
                    DrawMiniBox(context, cx, cy + (r * 0.04), r, 0.92, 1.0, topBrush, leftBrush, rightBrush, pen);
                    break;
            }

            var lbl = new FormattedText(
                shapes[i],
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Liberation Sans"),
                9,
                labelBrush);
            context.DrawText(lbl, new Point(cx - (lbl.Width / 2), rect.Bottom - lbl.Height - 1));
        }
    }

    private static void DrawMiniBox(
        DrawingContext context,
        double cx,
        double cy,
        double r,
        double hScale,
        double wScale,
        IBrush topBrush,
        IBrush leftBrush,
        IBrush rightBrush,
        Pen pen)
    {
        var w = r * wScale;
        var d = r * 0.44 * wScale;
        var h = r * 1.02 * hScale;
        var top = new Point(cx, cy - h - (d * 0.2));
        var right = new Point(cx + w, cy - h + (d * 0.45));
        var mid = new Point(cx, cy - h + (d * 1.15));
        var left = new Point(cx - w, cy - h + (d * 0.45));
        var bot = new Point(cx, cy + (d * 1.15));
        var botL = new Point(cx - w, cy + (d * 0.45));
        var botR = new Point(cx + w, cy + (d * 0.45));

        DrawPolygon(context, [top, right, mid, left], topBrush, pen);
        DrawPolygon(context, [left, mid, bot, botL], leftBrush, pen);
        DrawPolygon(context, [right, mid, bot, botR], rightBrush, pen);
    }

    private static void DrawMiniStair(
        DrawingContext context,
        double cx,
        double cy,
        double r,
        double flip,
        IBrush topBrush,
        IBrush leftBrush,
        IBrush rightBrush,
        Pen pen)
    {
        DrawMiniBox(context, cx, cy + (r * 0.22), r, 0.48, 1.0, topBrush, leftBrush, rightBrush, pen);
        var stepShiftX = -r * 0.24 * flip;
        var stepShiftY = -r * 0.34;
        DrawMiniBox(context, cx + stepShiftX, cy + stepShiftY, r * 0.72, 0.56, 0.82, topBrush, leftBrush, rightBrush, pen);
    }

    private static void DrawMiniSlope(
        DrawingContext context,
        double cx,
        double cy,
        double r,
        double flip,
        IBrush topBrush,
        IBrush leftBrush,
        IBrush rightBrush,
        Pen pen)
    {
        var w = r * 1.0;
        var d = r * 0.46;
        var h = r * 0.98;
        var backTop = new Point(cx - (w * 0.05 * flip), cy - (h * 0.92));
        var backSide = new Point(cx - (w * flip), cy - (h * 0.46));
        var frontBot = new Point(cx, cy + (d * 1.15));
        var rightBot = new Point(cx + (w * flip), cy + (d * 0.45));
        var leftBot = new Point(cx - (w * flip), cy + (d * 0.45));

        DrawPolygon(context, [backTop, backSide, frontBot, rightBot], topBrush, pen);
        DrawPolygon(context, [backSide, frontBot, leftBot], leftBrush, pen);
        DrawPolygon(context, [backTop, rightBot, frontBot], rightBrush, pen);
    }

    private void DrawPaintSwatch(
        DrawingContext context,
        Rect bounds,
        Color tint,
        bool isTexturePaint,
        string? paintTexturePath,
        IBrush labelBrush)
    {
        if (isTexturePaint)
        {
            var size = Math.Min(bounds.Width, bounds.Height) * 0.62;
            var frameRect = new Rect((bounds.Width - size) * 0.5, ((bounds.Height - size) * 0.5) + 6, size, size);
            var innerRect = frameRect.Deflate(3);
            var innerClip = new RoundedRect(innerRect, 7);
            var bitmap = ResolveTextureBitmap(paintTexturePath);

            if (bitmap != null && bitmap.PixelSize.Width > 0 && bitmap.PixelSize.Height > 0)
            {
                using (context.PushClip(innerClip))
                {
                    DrawTransparencyCheckerboard(context, innerRect, tileSize: 8.0);
                    var scale = Math.Min(innerRect.Width / bitmap.PixelSize.Width, innerRect.Height / bitmap.PixelSize.Height);
                    var drawW = bitmap.PixelSize.Width * scale;
                    var drawH = bitmap.PixelSize.Height * scale;
                    var dest = new Rect(
                        innerRect.X + ((innerRect.Width - drawW) * 0.5),
                        innerRect.Y + ((innerRect.Height - drawH) * 0.5),
                        drawW,
                        drawH);

                    using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                    {
                        context.DrawImage(bitmap, dest);
                    }
                }

                context.DrawRectangle(
                    null,
                    new Pen(new SolidColorBrush(Color.FromArgb(145, 255, 255, 255)), 1.5),
                    frameRect,
                    10,
                    10);

                if (bounds.Height - frameRect.Bottom >= 18)
                {
                    var dims = new FormattedText(
                        $"{bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}",
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Liberation Mono, Monospace"),
                        9.5,
                        labelBrush);
                    context.DrawText(dims, new Point(frameRect.Center.X - (dims.Width * 0.5), frameRect.Bottom + 4));
                }

                return;
            }

            using (context.PushClip(innerClip))
            {
                DrawMissingLayerCheckerboard(context, innerRect);
            }

            context.DrawRectangle(
                null,
                new Pen(new SolidColorBrush(Color.Parse("#F472B6")), 1.5),
                frameRect,
                10,
                10);

            var badgeWidth = Math.Min(96.0, Math.Max(72.0, frameRect.Width - 12));
            var badgeRect = new Rect(
                frameRect.Center.X - (badgeWidth * 0.5),
                frameRect.Center.Y - 10,
                badgeWidth,
                20);
            context.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(228, 24, 8, 22)),
                new Pen(new SolidColorBrush(Color.Parse("#F472B6")), 1),
                badgeRect,
                4,
                4);

            var missingText = new FormattedText(
                "MISSING LAYER",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Liberation Mono, Monospace"),
                9.5,
                new SolidColorBrush(Color.Parse("#FBCFE8")));
            context.DrawText(
                missingText,
                new Point(frameRect.Center.X - (missingText.Width * 0.5), frameRect.Center.Y - (missingText.Height * 0.5)));
            return;
        }

        var colorSize = Math.Min(bounds.Width, bounds.Height) * 0.56;
        var colorRect = new Rect((bounds.Width - colorSize) / 2, ((bounds.Height - colorSize) / 2) + 6, colorSize, colorSize);
        context.DrawRectangle(
            new SolidColorBrush(tint),
            new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), 1.5),
            colorRect,
            10,
            10);

        var shine = new Rect(colorRect.X + 8, colorRect.Y + 8, colorRect.Width * 0.35, 6);
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(95, 255, 255, 255)), null, shine, 3, 3);
    }

    private static void DrawMissingLayerCheckerboard(DrawingContext context, Rect rect)
    {
        var magentaBrush = new SolidColorBrush(Color.Parse("#E11D8F"));
        var darkBrush = new SolidColorBrush(Color.Parse("#1A0816"));
        var tileSize = Math.Max(8.0, Math.Min(rect.Width, rect.Height) * 0.25);

        context.DrawRectangle(darkBrush, null, rect);
        int cols = (int)Math.Ceiling(rect.Width / tileSize);
        int rows = (int)Math.Ceiling(rect.Height / tileSize);
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                if (((row + col) & 1) == 0)
                {
                    var tile = new Rect(
                        rect.X + (col * tileSize),
                        rect.Y + (row * tileSize),
                        tileSize,
                        tileSize);
                    context.DrawRectangle(magentaBrush, null, tile);
                }
            }
        }
    }

    private void DrawBodySprite(DrawingContext context, Rect bounds, Color tint, string? spriteTexturePath)
    {
        var bitmap = ResolveSpriteBitmap(spriteTexturePath);
        if (bitmap != null && bitmap.PixelSize.Width > 0 && bitmap.PixelSize.Height > 0)
        {
            var maxW = bounds.Width - 36;
            var maxH = bounds.Height - 40;
            var scale = Math.Min(maxW / bitmap.PixelSize.Width, maxH / bitmap.PixelSize.Height);
            var drawW = bitmap.PixelSize.Width * scale;
            var drawH = bitmap.PixelSize.Height * scale;
            var dest = new Rect((bounds.Width - drawW) * 0.5, ((bounds.Height - drawH) * 0.5) + 8, drawW, drawH);

            DrawTransparencyCheckerboard(context, dest, tileSize: 8.0);
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            {
                context.DrawImage(bitmap, dest);
            }

            context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1), dest);
            return;
        }

        var cx = bounds.Width * 0.5;
        var cy = bounds.Height * 0.56;
        var headBrush = new SolidColorBrush(Lighten(tint, 1.25));
        var suitBrush = new SolidColorBrush(tint);
        var visorBrush = new SolidColorBrush(Color.Parse("#7DD3FC"));

        context.DrawRectangle(headBrush, null, new Rect(cx - 14, cy - 42, 28, 24), 6, 6);
        context.DrawRectangle(visorBrush, null, new Rect(cx - 9, cy - 35, 18, 8), 3, 3);
        context.DrawRectangle(suitBrush, null, new Rect(cx - 18, cy - 16, 36, 34), 6, 6);
        context.DrawRectangle(new SolidColorBrush(Darken(tint, 0.72)), null, new Rect(cx - 14, cy + 20, 11, 22), 3, 3);
        context.DrawRectangle(new SolidColorBrush(Darken(tint, 0.72)), null, new Rect(cx + 3, cy + 20, 11, 22), 3, 3);
    }

    private Bitmap? ResolveSpriteBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ClearSpriteBitmapCache();
            return null;
        }

        DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
        if (string.Equals(_cachedSpritePath, path, StringComparison.Ordinal) && _cachedSpriteWriteUtc == lastWriteUtc)
        {
            return _cachedSpriteBitmap;
        }

        ClearSpriteBitmapCache();
        _cachedSpritePath = path;
        _cachedSpriteWriteUtc = lastWriteUtc;

        try
        {
            _cachedSpriteBitmap = new Bitmap(path);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed loading body preview sprite '{path}': {ex.Message}");
            _cachedSpriteBitmap = null;
        }

        return _cachedSpriteBitmap;
    }

    private void ClearSpriteBitmapCache()
    {
        _cachedSpriteBitmap?.Dispose();
        _cachedSpriteBitmap = null;
        _cachedSpritePath = null;
        _cachedSpriteWriteUtc = default;
    }

    private Bitmap? ResolveMaterialBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ClearMaterialBitmapCache();
            return null;
        }

        DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
        if (string.Equals(_cachedMaterialPath, path, StringComparison.Ordinal) && _cachedMaterialWriteUtc == lastWriteUtc)
        {
            return _cachedMaterialBitmap;
        }

        ClearMaterialBitmapCache();
        _cachedMaterialPath = path;
        _cachedMaterialWriteUtc = lastWriteUtc;

        try
        {
            _cachedMaterialBitmap = new Bitmap(path);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed loading material preview texture '{path}': {ex.Message}");
            _cachedMaterialBitmap = null;
        }

        return _cachedMaterialBitmap;
    }

    private void ClearMaterialBitmapCache()
    {
        _cachedMaterialBitmap?.Dispose();
        _cachedMaterialBitmap = null;
        _cachedMaterialPath = null;
        _cachedMaterialWriteUtc = default;
    }

    private Bitmap? ResolveTextureBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ClearPaintBitmapCache();
            return null;
        }

        DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
        if (string.Equals(_cachedPaintPath, path, StringComparison.Ordinal) && _cachedPaintWriteUtc == lastWriteUtc)
        {
            return _cachedPaintBitmap;
        }

        ClearPaintBitmapCache();
        _cachedPaintPath = path;
        _cachedPaintWriteUtc = lastWriteUtc;

        try
        {
            _cachedPaintBitmap = new Bitmap(path);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed loading paint preview texture '{path}': {ex.Message}");
            _cachedPaintBitmap = null;
        }

        return _cachedPaintBitmap;
    }

    private void ClearPaintBitmapCache()
    {
        _cachedPaintBitmap?.Dispose();
        _cachedPaintBitmap = null;
        _cachedPaintPath = null;
        _cachedPaintWriteUtc = default;
    }

    private static void DrawBlueprintCluster(DrawingContext context, Rect bounds, double orbitOffset, double pitchOffset)
    {
        var cx = bounds.Width * 0.5;
        var cy = bounds.Height * 0.55;
        var rotRad = orbitOffset * (Math.PI / 180.0);
        var cosR = Math.Cos(rotRad);
        var sinR = Math.Sin(rotRad);
        var pitchScale = 1.0 + (Math.Clamp(pitchOffset, -45.0, 45.0) * 0.008);

        (double dx, double dy, Color c)[] voxels =
        [
            (-24, 6, Color.Parse("#4A6B8A")),
            (0, 16, Color.Parse("#5A6988")),
            (24, 6, Color.Parse("#4A6B8A")),
            (-12, -8, Color.Parse("#88C0E8")),
            (12, -8, Color.Parse("#6E88A8")),
            (0, -20, Color.Parse("#4F8CFF")),
        ];

        for (var i = 0; i < voxels.Length; i++)
        {
            var rx = (voxels[i].dx * cosR) - (voxels[i].dy * sinR * 0.25);
            var ry = (voxels[i].dy * pitchScale) + (voxels[i].dx * sinR * 0.15);
            var vx = cx + rx;
            var vy = cy + ry;
            var r = 14.0;
            var top = new Point(vx, vy - r);
            var right = new Point(vx + r, vy - (r * 0.45));
            var mid = new Point(vx, vy + (r * 0.1));
            var left = new Point(vx - r, vy - (r * 0.45));
            var bot = new Point(vx, vy + (r * 0.85));
            var botL = new Point(vx - r, vy + (r * 0.35));
            var botR = new Point(vx + r, vy + (r * 0.35));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(75, 255, 255, 255)), 0.8);
            DrawPolygon(context, [top, right, mid, left], new SolidColorBrush(Lighten(voxels[i].c, 1.12)), pen);
            DrawPolygon(context, [left, mid, bot, botL], new SolidColorBrush(Darken(voxels[i].c, 0.82)), pen);
            DrawPolygon(context, [right, mid, bot, botR], new SolidColorBrush(Darken(voxels[i].c, 0.62)), pen);
        }
    }

    private void DrawMaterialPreview(
        DrawingContext context,
        Rect bounds,
        Color tint,
        string? materialTexturePath,
        MaterialPreviewShape materialShape,
        double orbitOffset,
        double pitchOffset)
    {
        var bitmap = ResolveMaterialBitmap(materialTexturePath);
        switch (materialShape)
        {
            case MaterialPreviewShape.Plane:
                DrawMaterialPlane(context, bounds, tint, bitmap, orbitOffset, pitchOffset);
                break;
            case MaterialPreviewShape.Cube:
                DrawMaterialCube(context, bounds, tint, bitmap, orbitOffset, pitchOffset);
                break;
            default:
                DrawMaterialSphere(context, bounds, tint, bitmap, orbitOffset, pitchOffset);
                break;
        }
    }

    private static void DrawMaterialSphere(
        DrawingContext context,
        Rect bounds,
        Color tint,
        Bitmap? bitmap,
        double orbitOffset,
        double pitchOffset)
    {
        var cx = bounds.Width * 0.5;
        var cy = bounds.Height * 0.54;
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.29;
        var sphereRect = new Rect(cx - radius, cy - radius, radius * 2.0, radius * 2.0);
        var sphereClip = new RoundedRect(sphereRect, radius);

        var orbitRad = orbitOffset * (Math.PI / 180.0);
        var pitchRad = Math.Clamp(pitchOffset, -45.0, 45.0) * (Math.PI / 180.0);
        var hx = Math.Clamp(0.35 + (Math.Sin(orbitRad) * 0.12), 0.18, 0.82);
        var hy = Math.Clamp(0.28 - (Math.Sin(pitchRad) * 0.12), 0.16, 0.78);
        var lightCenter = new RelativePoint(hx, hy, RelativeUnit.Relative);

        using (context.PushClip(sphereClip))
        {
            if (bitmap != null && bitmap.PixelSize.Width > 0 && bitmap.PixelSize.Height > 0)
            {
                DrawTransparencyCheckerboard(context, sphereRect, tileSize: 8.0);
                var scale = Math.Max(
                    sphereRect.Width / bitmap.PixelSize.Width,
                    sphereRect.Height / bitmap.PixelSize.Height);
                var drawW = bitmap.PixelSize.Width * scale;
                var drawH = bitmap.PixelSize.Height * scale;
                var dest = new Rect(cx - (drawW * 0.5), cy - (drawH * 0.5), drawW, drawH);
                using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                {
                    context.DrawImage(bitmap, dest);
                }

                var sphericalShading = new RadialGradientBrush
                {
                    Center = lightCenter,
                    GradientOrigin = lightCenter,
                    GradientStops =
                    [
                        new GradientStop(Color.FromArgb(42, 255, 255, 255), 0.0),
                        new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.50),
                        new GradientStop(Color.FromArgb(95, 8, 12, 22), 0.82),
                        new GradientStop(Color.FromArgb(175, 4, 8, 16), 1.0),
                    ],
                };
                context.DrawRectangle(sphericalShading, null, sphereRect);
            }
            else
            {
                var radial = new RadialGradientBrush
                {
                    Center = lightCenter,
                    GradientOrigin = lightCenter,
                    GradientStops =
                    [
                        new GradientStop(Lighten(tint, 1.45), 0.0),
                        new GradientStop(tint, 0.55),
                        new GradientStop(Darken(tint, 0.44), 1.0),
                    ],
                };
                context.DrawRectangle(radial, null, sphereRect);
            }

            var specX = cx + ((hx - 0.5) * radius * 1.45);
            var specY = cy + ((hy - 0.5) * radius * 1.45);
            var specRadius = radius * 0.48;
            var specular = new RadialGradientBrush
            {
                Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(Color.FromArgb(145, 255, 255, 255), 0.0),
                    new GradientStop(Color.FromArgb(45, 255, 255, 255), 0.45),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0),
                ],
            };
            context.DrawEllipse(specular, null, new Point(specX, specY), specRadius, specRadius);
        }

        context.DrawEllipse(
            null,
            new Pen(new SolidColorBrush(Color.FromArgb(145, 255, 255, 255)), 1.3),
            new Point(cx, cy),
            radius,
            radius);
    }

    private static void DrawMaterialPlane(
        DrawingContext context,
        Rect bounds,
        Color tint,
        Bitmap? bitmap,
        double orbitOffset,
        double pitchOffset)
    {
        float w = (float)bounds.Width;
        float h = (float)bounds.Height;
        var cam = new PreviewCamera();
        cam.ApplyPreset(PreviewCameraPreset.OrbitBrick);
        cam.YawDegrees = 24f + (float)orbitOffset;
        cam.PitchDegrees = Math.Clamp(14f + (float)pitchOffset, -80f, 80f);

        Vector3 eye = cam.GetEyePosition();
        bool frontSide = eye.Z >= 0f;
        const float half = 0.62f;
        var tl = new Vector3(frontSide ? -half : half, half, 0f);
        var tr = new Vector3(frontSide ? half : -half, half, 0f);
        var bl = new Vector3(frontSide ? -half : half, -half, 0f);
        var br = new Vector3(frontSide ? half : -half, -half, 0f);

        if (!cam.TryProjectToScreen(tl, w, h, out Vector2 sTl, out _)
            || !cam.TryProjectToScreen(tr, w, h, out Vector2 sTr, out _)
            || !cam.TryProjectToScreen(bl, w, h, out Vector2 sBl, out _)
            || !cam.TryProjectToScreen(br, w, h, out Vector2 sBr, out _))
        {
            return;
        }

        static float Cross2D(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
        float area = (0.5f * MathF.Abs(Cross2D(sTr - sTl, sBl - sTl)))
            + (0.5f * MathF.Abs(Cross2D(sTr - sBr, sBl - sBr)));
        float edgeSpan = MathF.Max(Vector2.Distance(sTl, sTr), Vector2.Distance(sBl, sBr));

        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(145, 255, 255, 255)), 1.3);
        if (area < 2.5f || edgeSpan < 1.25f)
        {
            if (cam.TryProjectToScreen(new Vector3(0f, half, 0f), w, h, out Vector2 edgeTop, out _)
                && cam.TryProjectToScreen(new Vector3(0f, -half, 0f), w, h, out Vector2 edgeBot, out _))
            {
                context.DrawLine(borderPen, new Point(edgeTop.X, edgeTop.Y), new Point(edgeBot.X, edgeBot.Y));
            }

            return;
        }

        Vector3 eyeDir = Vector3.Normalize(eye);
        double brightness = 0.68 + (0.32 * MathF.Abs(eyeDir.Z));
        Matrix4x4 viewProj = cam.GetViewMatrix() * cam.GetProjectionMatrix(w / Math.Max(h, 1f));
        DrawPerspectiveMaterialQuad(
            context,
            bitmap,
            tint,
            brightness,
            tl,
            tr,
            bl,
            sTl,
            sTr,
            sBr,
            sBl,
            in viewProj,
            w,
            h,
            borderPen);
    }

    private static void DrawMaterialCube(
        DrawingContext context,
        Rect bounds,
        Color tint,
        Bitmap? bitmap,
        double orbitOffset,
        double pitchOffset)
    {
        float w = (float)bounds.Width;
        float h = (float)bounds.Height;
        var cam = new PreviewCamera();
        cam.ApplyPreset(PreviewCameraPreset.OrbitBrick);
        cam.YawDegrees = 42f + (float)orbitOffset;
        cam.PitchDegrees = Math.Clamp(28f + (float)pitchOffset, -85f, 85f);

        Vector3 eye = cam.GetEyePosition();
        Matrix4x4 viewProj = cam.GetViewMatrix() * cam.GetProjectionMatrix(w / Math.Max(h, 1f));
        var edgePen = new Pen(new SolidColorBrush(Color.FromArgb(115, 255, 255, 255)), 1.1);
        const float half = 0.52f;

        (Vector3 Tl, Vector3 Tr, Vector3 Bl, Vector3 Br, Vector3 Normal, double Brightness)[] faces =
        [
            (new(-half, half, -half), new(half, half, -half), new(-half, half, half), new(half, half, half), Vector3.UnitY, 1.00),
            (new(-half, -half, half), new(half, -half, half), new(-half, -half, -half), new(half, -half, -half), -Vector3.UnitY, 0.78),
            (new(-half, half, half), new(half, half, half), new(-half, -half, half), new(half, -half, half), Vector3.UnitZ, 0.95),
            (new(half, half, -half), new(-half, half, -half), new(half, -half, -half), new(-half, -half, -half), -Vector3.UnitZ, 0.90),
            (new(half, half, half), new(half, half, -half), new(half, -half, half), new(half, -half, -half), Vector3.UnitX, 0.85),
            (new(-half, half, -half), new(-half, half, half), new(-half, -half, -half), new(-half, -half, half), -Vector3.UnitX, 0.85),
        ];

        for (var i = 0; i < faces.Length; i++)
        {
            var face = faces[i];
            Vector3 center = (face.Tl + face.Br) * 0.5f;
            if (Vector3.Dot(eye - center, face.Normal) <= 0f)
            {
                continue;
            }

            if (!cam.TryProjectToScreen(face.Tl, w, h, out Vector2 sTl, out _)
                || !cam.TryProjectToScreen(face.Tr, w, h, out Vector2 sTr, out _)
                || !cam.TryProjectToScreen(face.Br, w, h, out Vector2 sBr, out _)
                || !cam.TryProjectToScreen(face.Bl, w, h, out Vector2 sBl, out _))
            {
                continue;
            }

            DrawPerspectiveMaterialQuad(
                context,
                bitmap,
                tint,
                face.Brightness,
                face.Tl,
                face.Tr,
                face.Bl,
                sTl,
                sTr,
                sBr,
                sBl,
                in viewProj,
                w,
                h,
                edgePen);
        }
    }

    private static void DrawPerspectiveMaterialQuad(
        DrawingContext context,
        Bitmap? bitmap,
        Color tint,
        double brightness,
        Vector3 tl,
        Vector3 tr,
        Vector3 bl,
        Vector2 sTl,
        Vector2 sTr,
        Vector2 sBr,
        Vector2 sBl,
        in Matrix4x4 viewProj,
        float viewportWidth,
        float viewportHeight,
        Pen borderPen)
    {
        Point[] pts =
        [
            new Point(sTl.X, sTl.Y),
            new Point(sTr.X, sTr.Y),
            new Point(sBr.X, sBr.Y),
            new Point(sBl.X, sBl.Y),
        ];

        if (bitmap != null && bitmap.PixelSize.Width > 0 && bitmap.PixelSize.Height > 0)
        {
            if (ModelEditorFallbackControl.TryBuildWorldQuadPerspectiveMatrix(
                tl,
                tr,
                bl,
                64f,
                64f,
                in viewProj,
                viewportWidth,
                viewportHeight,
                out Matrix checkerMatrix))
            {
                using (context.PushTransform(checkerMatrix))
                {
                    DrawTransparencyCheckerboard(context, new Rect(0, 0, 64, 64), tileSize: 8.0);
                }
            }

            float bw = Math.Max(1f, bitmap.PixelSize.Width);
            float bh = Math.Max(1f, bitmap.PixelSize.Height);
            if (ModelEditorFallbackControl.TryBuildWorldQuadPerspectiveMatrix(
                tl,
                tr,
                bl,
                bw,
                bh,
                in viewProj,
                viewportWidth,
                viewportHeight,
                out Matrix textureMatrix))
            {
                using (context.PushTransform(textureMatrix))
                using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                {
                    context.DrawImage(bitmap, new Rect(0, 0, bw, bh));
                }
            }

            byte shadeAlpha = (byte)Math.Clamp((1.0 - brightness) * 210.0, 0.0, 175.0);
            IBrush? shadeBrush = shadeAlpha > 0
                ? new SolidColorBrush(Color.FromArgb(shadeAlpha, 4, 8, 16))
                : null;
            DrawPolygon(context, pts, shadeBrush, borderPen);
            return;
        }

        Color shaded = brightness >= 1.0 ? Lighten(tint, brightness) : Darken(tint, brightness);
        DrawPolygon(context, pts, new SolidColorBrush(shaded), borderPen);
    }

    private static void DrawItemPreview(
        DrawingContext context,
        Rect bounds,
        Color tint,
        bool isWorldModel,
        double orbitOffset,
        double pitchOffset,
        IBrush labelBrush)
    {
        var cx = bounds.Width * 0.5;
        var cy = bounds.Height * 0.55;
        if (isWorldModel)
        {
            var box = new Rect(cx - 42, cy - 36, 84, 72);
            context.DrawRectangle(null, new Pen(labelBrush, 1, DashStyle.Dash), box, 4, 4);
        }

        DrawSingleBrick(context, bounds, tint, false, "Custom", BrickOrientation.East, orbitOffset + (isWorldModel ? 0 : 18), pitchOffset);
    }

    private static void DrawDataDocumentPlaceholder(DrawingContext context, Rect bounds, string subtitle, IBrush labelBrush)
    {
        var cx = bounds.Width * 0.5;
        var cy = bounds.Height * 0.52;
        var card = new Rect(cx - 64, cy - 28, 128, 56);
        context.DrawRectangle(null, new Pen(labelBrush, 1, DashStyle.Dash), card, 6, 6);
        var line1 = new FormattedText(
            subtitle,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Liberation Sans", FontStyle.Normal, FontWeight.SemiBold),
            11,
            labelBrush);
        var line2 = new FormattedText(
            "No 3D surface",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Liberation Sans"),
            10,
            labelBrush);
        context.DrawText(line1, new Point(cx - (line1.Width / 2), cy - 16));
        context.DrawText(line2, new Point(cx - (line2.Width / 2), cy + 2));
    }

    private static void DrawPolygon(DrawingContext context, Point[] pts, IBrush? fill, Pen pen)
    {
        var geo = new StreamGeometry();
        using (var gc = geo.Open())
        {
            gc.BeginFigure(pts[0], true);
            for (var i = 1; i < pts.Length; i++)
            {
                gc.LineTo(pts[i]);
            }

            gc.EndFigure(true);
        }

        context.DrawGeometry(fill, pen, geo);
    }

    private static Color ParseColor(string? hex, Color fallback)
    {
        return !string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex.Trim(), out var parsed)
            ? parsed
            : fallback;
    }

    private static Color Lighten(Color c, double factor)
    {
        return Color.FromArgb(
            c.A,
            (byte)Math.Clamp(c.R * factor, 0, 255),
            (byte)Math.Clamp(c.G * factor, 0, 255),
            (byte)Math.Clamp(c.B * factor, 0, 255));
    }

    private static Color Darken(Color c, double factor)
    {
        return Color.FromArgb(
            c.A,
            (byte)Math.Clamp(c.R * factor, 0, 255),
            (byte)Math.Clamp(c.G * factor, 0, 255),
            (byte)Math.Clamp(c.B * factor, 0, 255));
    }
}
