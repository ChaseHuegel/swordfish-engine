using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.Meshing;

namespace WaywardBeyond.AssetStudio.Controls;

/// <summary>Software 3D-projected viewport fallback and headless UI mirror for the item model editor.</summary>
public sealed class ModelEditorFallbackControl : Control, ICustomHitTest
{
    private const float ORBIT_SENSITIVITY = 0.45f;
    private const float DEGREES_TO_RADIANS = MathF.PI / 180f;

    private static readonly Cursor _defaultCursor = Cursor.Default;
    private static readonly Cursor _handCursor = new(StandardCursorType.Hand);
    private static readonly Cursor _gizmoCursor = new(StandardCursorType.SizeAll);

    private readonly PreviewCamera _camera = new();
    private readonly TransformGizmoController _gizmo = new();

    private ModelEditorRenderState _state = ModelEditorRenderState.Default;
    private bool _hasCameraModeInitialized;
    private bool _lastIsWorldModel;
    private bool _isOrbiting;
    private bool _isPanning;
    private Point _lastPointerPos;
    private Vector3 _dragStartGizmoAnchor;
    private Vector3 _dragStartPosition;

    private string? _cachedBodyPath;
    private DateTime _cachedBodyWriteUtc;
    private Bitmap? _cachedBodyBitmap;

    /// <summary>Gets the orbit or first-person camera driving this fallback viewport.</summary>
    public PreviewCamera Camera => _camera;

    /// <summary>Raised when the user starts dragging a 3D gizmo handle.</summary>
    public event Action? GizmoDragStarted;

    /// <summary>Raised during a 3D gizmo drag with updated Position, Rotation, Scale, and readout caption.</summary>
    public event Action<Vector3, Vector3, Vector3, string?>? TransformDragged;

    /// <summary>Raised when the user releases a 3D gizmo handle.</summary>
    public event Action<Vector3, Vector3, Vector3>? GizmoDragEnded;

    /// <inheritdoc/>
    public bool HitTest(Point point) =>
        point.X >= 0 && point.Y >= 0 && point.X <= Bounds.Width && point.Y <= Bounds.Height;

    /// <summary>Updates the rendered camera, transform, mesh, and reference state and requests repaint.</summary>
    public void UpdateEditorState(in ModelEditorRenderState state)
    {
        bool modeChanged = !_hasCameraModeInitialized || _lastIsWorldModel != state.IsWorldModel;
        bool fovChanged = MathF.Abs(_state.FovDegrees - state.FovDegrees) > 0.01f;

        _state = state;
        _hasCameraModeInitialized = true;
        _lastIsWorldModel = state.IsWorldModel;

        if (!state.IsWorldModel)
        {
            if (modeChanged || fovChanged)
            {
                _camera.ConfigureFirstPersonViewModel(state.FovDegrees);
            }
        }
        else if (modeChanged)
        {
            _camera.ApplyPreset(PreviewCameraPreset.ItemWorldModelEditor);
        }

        Cursor = state.IsWorldModel ? _handCursor : _defaultCursor;
        InvalidateVisual();
    }

    /// <summary>Resets the WorldModel orbit camera to its default human-scale framing.</summary>
    public void ResetOrbitCamera()
    {
        if (_state.IsWorldModel)
        {
            _camera.ApplyPreset(PreviewCameraPreset.ItemWorldModelEditor);
            InvalidateVisual();
        }
    }

    /// <summary>Clears cached session camera state so the next <see cref="UpdateEditorState"/> applies the active mode preset.</summary>
    public void ResetCameraForNewSession()
    {
        _hasCameraModeInitialized = false;
        _isOrbiting = false;
        _isPanning = false;
    }

    /// <summary>Builds an Avalonia projective 3x3 matrix mapping a <paramref name="sourceWidth"/>x<paramref name="sourceHeight"/> quad onto a fixed 3D world quad.</summary>
    public static bool TryBuildWorldQuadPerspectiveMatrix(
        Vector3 topLeft,
        Vector3 topRight,
        Vector3 bottomLeft,
        float sourceWidth,
        float sourceHeight,
        in Matrix4x4 viewProj,
        float viewportWidth,
        float viewportHeight,
        out Matrix matrix)
    {
        matrix = Matrix.Identity;
        if (sourceWidth <= 1e-3f || sourceHeight <= 1e-3f || viewportWidth <= 1f || viewportHeight <= 1f)
        {
            return false;
        }

        Vector3 du = (topRight - topLeft) / sourceWidth;
        Vector3 dv = (bottomLeft - topLeft) / sourceHeight;

        Vector4 c0 = Vector4.Transform(new Vector4(topLeft, 1f), viewProj);
        Vector4 cu = Vector4.Transform(new Vector4(du, 0f), viewProj);
        Vector4 cv = Vector4.Transform(new Vector4(dv, 0f), viewProj);

        Vector4 cTr = c0 + (cu * sourceWidth);
        Vector4 cBl = c0 + (cv * sourceHeight);
        Vector4 cBr = cTr + (cv * sourceHeight);
        if (c0.W <= 1e-4f || cTr.W <= 1e-4f || cBl.W <= 1e-4f || cBr.W <= 1e-4f)
        {
            return false;
        }

        double invW0 = 1.0 / c0.W;
        double halfW = viewportWidth * 0.5;
        double halfH = viewportHeight * 0.5;

        double m11 = halfW * (cu.X + cu.W) * invW0;
        double m12 = halfH * (cu.W - cu.Y) * invW0;
        double m13 = cu.W * invW0;

        double m21 = halfW * (cv.X + cv.W) * invW0;
        double m22 = halfH * (cv.W - cv.Y) * invW0;
        double m23 = cv.W * invW0;

        double m31 = halfW * (c0.X + c0.W) * invW0;
        double m32 = halfH * (c0.W - c0.Y) * invW0;
        const double m33 = 1.0;

        if (!double.IsFinite(m11) || !double.IsFinite(m12) || !double.IsFinite(m13)
            || !double.IsFinite(m21) || !double.IsFinite(m22) || !double.IsFinite(m23)
            || !double.IsFinite(m31) || !double.IsFinite(m32))
        {
            return false;
        }

        matrix = new Matrix(m11, m12, m13, m21, m22, m23, m31, m32, m33);
        return true;
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DisposeCachedBitmap(ref _cachedBodyBitmap, ref _cachedBodyPath);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        PointerPoint point = e.GetCurrentPoint(this);
        PointerPointProperties props = point.Properties;
        var pos = new Vector2((float)point.Position.X, (float)point.Position.Y);
        float w = (float)Bounds.Width;
        float h = (float)Bounds.Height;

        if (!_state.IsReadOnly && props.IsLeftButtonPressed && e.ClickCount == 1)
        {
            Vector3 gizmoAnchor = ResolveCurrentGizmoAnchor(w, h);
            TransformGizmoAxis hit = _gizmo.HitTest(
                pos,
                w,
                h,
                _camera,
                _state.GizmoMode,
                _state.GizmoSpace,
                gizmoAnchor,
                _state.RotationDegrees);
            if (hit != TransformGizmoAxis.None && _gizmo.BeginDrag(
                hit,
                pos,
                w,
                h,
                _camera,
                _state.GizmoMode,
                _state.GizmoSpace,
                _state.Position,
                _state.RotationDegrees,
                _state.Scale,
                gizmoAnchor))
            {
                _dragStartGizmoAnchor = gizmoAnchor;
                _dragStartPosition = _state.Position;
                _isOrbiting = false;
                _isPanning = false;
                Cursor = _gizmoCursor;
                e.Pointer.Capture(this);
                GizmoDragStarted?.Invoke();
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        if (!_state.IsWorldModel)
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            _isOrbiting = false;
            _isPanning = false;
            e.Pointer.Capture(null);
            ResetOrbitCamera();
            e.Handled = true;
            return;
        }

        if (props.IsRightButtonPressed
            || props.IsMiddleButtonPressed
            || (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            _isPanning = true;
            _isOrbiting = false;
        }
        else if (props.IsLeftButtonPressed)
        {
            _isOrbiting = true;
            _isPanning = false;
        }
        else
        {
            return;
        }

        _lastPointerPos = point.Position;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point current = e.GetPosition(this);
        var pos = new Vector2((float)current.X, (float)current.Y);
        float w = (float)Bounds.Width;
        float h = (float)Bounds.Height;

        if (_gizmo.IsDragging)
        {
            if (_gizmo.UpdateDrag(
                pos,
                w,
                h,
                _camera,
                _state.GizmoMode,
                _state.SnapEnabled,
                _state.UniformScale,
                out Vector3 newPos,
                out Vector3 newRot,
                out Vector3 newScale,
                out string? readout))
            {
                _state = _state with
                {
                    Position = newPos,
                    RotationDegrees = newRot,
                    Scale = newScale,
                };
                TransformDragged?.Invoke(newPos, newRot, newScale, readout);
                InvalidateVisual();
            }

            e.Handled = true;
            return;
        }

        if (_isPanning || _isOrbiting)
        {
            float dx = (float)(current.X - _lastPointerPos.X);
            float dy = (float)(current.Y - _lastPointerPos.Y);
            _lastPointerPos = current;

            if (_isPanning)
            {
                _camera.Pan(dx, dy);
            }
            else
            {
                _camera.Orbit(-dx * ORBIT_SENSITIVITY, dy * ORBIT_SENSITIVITY);
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        Vector3 gizmoAnchor = ResolveCurrentGizmoAnchor(w, h);
        if (_gizmo.UpdateHover(
            pos,
            w,
            h,
            _camera,
            _state.GizmoMode,
            _state.GizmoSpace,
            gizmoAnchor,
            _state.RotationDegrees,
            _state.IsReadOnly))
        {
            Cursor = _gizmo.HoveredAxis != TransformGizmoAxis.None
                ? _gizmoCursor
                : (_state.IsWorldModel ? _handCursor : _defaultCursor);
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_gizmo.IsDragging)
        {
            _gizmo.EndDrag();
            e.Pointer.Capture(null);
            Cursor = _gizmo.HoveredAxis != TransformGizmoAxis.None
                ? _gizmoCursor
                : (_state.IsWorldModel ? _handCursor : _defaultCursor);
            GizmoDragEnded?.Invoke(_state.Position, _state.RotationDegrees, _state.Scale);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isOrbiting || _isPanning)
        {
            _isOrbiting = false;
            _isPanning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_gizmo.IsDragging)
        {
            _gizmo.EndDrag();
            GizmoDragEnded?.Invoke(_state.Position, _state.RotationDegrees, _state.Scale);
            InvalidateVisual();
        }

        _isOrbiting = false;
        _isPanning = false;
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!_state.IsWorldModel)
        {
            return;
        }

        float delta = (float)e.Delta.Y;
        if (MathF.Abs(delta) <= 1e-5f && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            delta = (float)e.Delta.X;
        }

        if (MathF.Abs(delta) > 1e-5f)
        {
            _camera.Zoom(delta);
            InvalidateVisual();
        }

        e.Handled = true;
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        Rect bounds = Bounds;
        if (bounds.Width < 16 || bounds.Height < 16)
        {
            return;
        }

        float w = (float)bounds.Width;
        float h = (float)bounds.Height;

        var backdrop = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.Parse("#0B0E14"), 0.0),
                new GradientStop(Color.Parse("#131823"), 1.0),
            ],
        };
        context.DrawRectangle(backdrop, null, new Rect(0, 0, w, h));

        using (context.PushClip(new Rect(0, 0, w, h)))
        {
            if (_state.IsWorldModel)
            {
                if (_state.ShowFloorPlane)
                {
                    DrawFloorReference(context, w, h);
                }

                bool drawBodyInFront = false;
                if (_state.ShowBodyReference)
                {
                    Vector3 eye = _camera.GetEyePosition();
                    var bodyCenter = new Vector3(0f, ModelTransformMath.PLAYER_STANDING_HEIGHT_METERS * 0.5f, -1f);
                    drawBodyInFront = Vector3.DistanceSquared(eye, bodyCenter) < Vector3.DistanceSquared(eye, _state.Position);
                }

                if (_state.ShowBodyReference && !drawBodyInFront)
                {
                    DrawBodyReference(context, w, h);
                }

                DrawProjectedItemMesh(context, w, h);

                if (_state.ShowBodyReference && drawBodyInFront)
                {
                    DrawBodyReference(context, w, h);
                }
            }
            else
            {
                DrawFirstPersonHorizon(context, w, h);
                DrawProjectedItemMesh(context, w, h);
            }

            if (!_state.IsReadOnly)
            {
                DrawProjectedGizmo(context, w, h);
            }
        }
    }

    private static void DrawFirstPersonHorizon(DrawingContext context, float w, float h)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(24, 148, 163, 184)), 1.0);
        double cy = h * 0.5;
        context.DrawLine(pen, new Point(0, cy), new Point(w, cy));
        double cx = w * 0.5;
        context.DrawLine(pen, new Point(cx, 0), new Point(cx, h));
    }

    private void DrawFloorReference(DrawingContext context, float w, float h)
    {
        const float half = 0.5f;
        Vector3[] corners =
        [
            new Vector3(-half, 0f, -half),
            new Vector3(half, 0f, -half),
            new Vector3(half, 0f, half),
            new Vector3(-half, 0f, half),
        ];

        var projected = new Point[4];
        for (var i = 0; i < 4; i++)
        {
            if (!_camera.TryProjectToScreen(corners[i], w, h, out Vector2 sp, out _))
            {
                return;
            }

            projected[i] = new Point(sp.X, sp.Y);
        }

        DrawPolygon(
            context,
            projected,
            new SolidColorBrush(Color.FromArgb(140, 36, 46, 66)),
            new Pen(new SolidColorBrush(Color.FromArgb(215, 136, 165, 212)), 1.5));

        var subPen = new Pen(new SolidColorBrush(Color.FromArgb(95, 100, 116, 139)), 1.0);
        for (var i = -1; i <= 1; i++)
        {
            float c = i * 0.25f;
            DrawProjectedLine(context, new Vector3(-half, 0f, c), new Vector3(half, 0f, c), w, h, subPen);
            DrawProjectedLine(context, new Vector3(c, 0f, -half), new Vector3(c, 0f, half), w, h, subPen);
        }

        // RGB origin axes at (0, 0, 0)
        DrawProjectedLine(context, Vector3.Zero, new Vector3(0.30f, 0f, 0f), w, h, new Pen(new SolidColorBrush(Color.Parse("#EF4444")), 1.8));
        DrawProjectedLine(context, Vector3.Zero, new Vector3(0f, 0.30f, 0f), w, h, new Pen(new SolidColorBrush(Color.Parse("#22C55E")), 1.8));
        DrawProjectedLine(context, Vector3.Zero, new Vector3(0f, 0f, 0.30f), w, h, new Pen(new SolidColorBrush(Color.Parse("#3B82F6")), 1.8));

        if (_camera.TryProjectToScreen(new Vector3(-0.5f, 0f, 0.5f), w, h, out Vector2 cornerScreen, out _))
        {
            var dimLabel = new FormattedText(
                "1.0 m × 1.0 m (0,0,0)",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Liberation Mono, Monospace"),
                9.5,
                new SolidColorBrush(Color.FromArgb(190, 148, 163, 184)));
            context.DrawText(dimLabel, new Point(cornerScreen.X - (dimLabel.Width * 0.5), cornerScreen.Y + 10));
        }
    }

    private void DrawBodyReference(DrawingContext context, float w, float h)
    {
        float height = ModelTransformMath.PLAYER_STANDING_HEIGHT_METERS;
        float aspect = Math.Clamp(_state.ReferenceBodyAspect, 0.25f, 2.5f);
        float halfW = (height * aspect) * 0.5f;
        const float z = -1.0f;

        Vector3 bl = new(-halfW, 0f, z);
        Vector3 br = new(halfW, 0f, z);
        Vector3 tr = new(halfW, height, z);
        Vector3 tl = new(-halfW, height, z);

        if (!_camera.TryProjectToScreen(bl, w, h, out Vector2 sBl, out _)
            || !_camera.TryProjectToScreen(br, w, h, out Vector2 sBr, out _)
            || !_camera.TryProjectToScreen(tr, w, h, out Vector2 sTr, out _)
            || !_camera.TryProjectToScreen(tl, w, h, out Vector2 sTl, out _))
        {
            return;
        }

        // Floor footprint line under the fixed z = -1 plane
        DrawProjectedLine(
            context,
            bl,
            br,
            w,
            h,
            new Pen(new SolidColorBrush(Color.FromArgb(145, 96, 165, 250)), 1.4));

        static float Cross2D(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
        float area = (0.5f * MathF.Abs(Cross2D(sTr - sTl, sBl - sTl)))
            + (0.5f * MathF.Abs(Cross2D(sTr - sBr, sBl - sBr)));
        float edgeSpan = MathF.Max(Vector2.Distance(sTl, sTr), Vector2.Distance(sBl, sBr));

        Bitmap? spriteBitmap = EnsureBodySpriteBitmap(_state.ReferenceBodySpritePath);
        if (area < 2.5f || edgeSpan < 1.25f)
        {
            // Plane is viewed nearly edge-on; draw a crisp edge line rather than facing the camera
            DrawProjectedLine(
                context,
                new Vector3(0f, 0f, z),
                new Vector3(0f, height, z),
                w,
                h,
                new Pen(new SolidColorBrush(Color.FromArgb(190, 96, 165, 250)), 1.5));
        }
        else
        {
            Matrix4x4 viewProj = _camera.GetViewMatrix() * _camera.GetProjectionMatrix(w / Math.Max(h, 1f));
            if (spriteBitmap != null)
            {
                float bw = Math.Max(1f, spriteBitmap.PixelSize.Width);
                float bh = Math.Max(1f, spriteBitmap.PixelSize.Height);
                if (TryBuildWorldQuadPerspectiveMatrix(tl, tr, bl, bw, bh, in viewProj, w, h, out Matrix spriteMatrix))
                {
                    using (context.PushTransform(spriteMatrix))
                    using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                    {
                        context.DrawImage(spriteBitmap, new Rect(0, 0, bw, bh));
                    }
                }
            }
            else if (TryBuildWorldQuadPerspectiveMatrix(tl, tr, bl, 100f, 180f, in viewProj, w, h, out Matrix fallbackMatrix))
            {
                // Minimal silhouette guide fixed in the z = -1 world plane when no body PNGs exist
                using (context.PushTransform(fallbackMatrix))
                {
                    var bodyBrush = new SolidColorBrush(Color.FromArgb(65, 96, 165, 250));
                    var bodyPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 96, 165, 250)), 1.2);
                    const double headR = 180.0 * 0.085;
                    context.DrawEllipse(bodyBrush, bodyPen, new Point(50.0, headR + 2.0), headR, headR);
                    var torso = new Rect(50.0 - 24.0, headR * 2.2, 48.0, 180.0 * 0.68);
                    context.DrawRectangle(bodyBrush, bodyPen, torso, 4, 4);
                }
            }
        }

        if (_camera.TryProjectToScreen(new Vector3(0f, height, z), w, h, out Vector2 topCenterScreen, out _))
        {
            string caption = !string.IsNullOrWhiteSpace(_state.ReferenceBodyId)
                ? $"{_state.ReferenceBodyId} · 1.8 m"
                : "1.8 m Scale Reference";
            var label = new FormattedText(
                caption,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Liberation Mono, Monospace"),
                9.5,
                new SolidColorBrush(Color.FromArgb(200, 147, 197, 253)));
            context.DrawText(label, new Point(topCenterScreen.X - (label.Width * 0.5), topCenterScreen.Y - label.Height - 4));
        }
    }

    private void DrawProjectedItemMesh(DrawingContext context, float w, float h)
    {
        VoxelMesh mesh = _state.MeshGeometry;
        if (mesh.IsEmpty || mesh.Triangles == null || mesh.Triangles.Length < 3)
        {
            return;
        }

        Matrix4x4 model = ModelTransformMath.CreateModelMatrix(_state.Position, _state.RotationDegrees, _state.Scale);
        Matrix4x4 viewProj = _camera.GetViewMatrix() * _camera.GetProjectionMatrix(w / Math.Max(h, 1f));

        Vector3[] verts = mesh.Vertices;
        uint[] indices = mesh.Triangles;
        int triCount = indices.Length / 3;
        int stride = Math.Max(1, triCount / 900);

        var faces = new List<(Point P0, Point P1, Point P2, float Depth, Color Fill)>(Math.Min(triCount, 900));
        Color baseColor = ParseItemBaseTint(_state.AccentHex);
        Vector3 lightDir = Vector3.Normalize(new Vector3(0.45f, 0.78f, 0.52f));

        for (var t = 0; t < triCount; t += stride)
        {
            int idxBase = t * 3;
            uint i0 = indices[idxBase];
            uint i1 = indices[idxBase + 1];
            uint i2 = indices[idxBase + 2];
            if (i0 >= verts.Length || i1 >= verts.Length || i2 >= verts.Length)
            {
                continue;
            }

            Vector3 w0 = Vector3.Transform(verts[i0], model);
            Vector3 w1 = Vector3.Transform(verts[i1], model);
            Vector3 w2 = Vector3.Transform(verts[i2], model);

            if (!ProjectClipPoint(w0, in viewProj, w, h, out Point p0, out float d0)
                || !ProjectClipPoint(w1, in viewProj, w, h, out Point p1, out float d1)
                || !ProjectClipPoint(w2, in viewProj, w, h, out Point p2, out float d2))
            {
                continue;
            }

            Vector3 e1 = w1 - w0;
            Vector3 e2 = w2 - w0;
            Vector3 cross = Vector3.Cross(e1, e2);
            float lenSq = cross.LengthSquared();
            Vector3 faceNormal = lenSq > 1e-10f ? cross / MathF.Sqrt(lenSq) : Vector3.UnitY;
            float lambert = Math.Clamp((Vector3.Dot(faceNormal, lightDir) * 0.35f) + 0.72f, 0.38f, 1.12f);

            Color vertTint = baseColor;
            if (mesh.Colors != null && i0 < mesh.Colors.Length)
            {
                Vector4 vc = mesh.Colors[i0];
                byte r = (byte)Math.Clamp(baseColor.R * vc.X * lambert, 0, 255);
                byte g = (byte)Math.Clamp(baseColor.G * vc.Y * lambert, 0, 255);
                byte b = (byte)Math.Clamp(baseColor.B * vc.Z * lambert, 0, 255);
                vertTint = Color.FromRgb(r, g, b);
            }
            else
            {
                byte r = (byte)Math.Clamp(baseColor.R * lambert, 0, 255);
                byte g = (byte)Math.Clamp(baseColor.G * lambert, 0, 255);
                byte b = (byte)Math.Clamp(baseColor.B * lambert, 0, 255);
                vertTint = Color.FromRgb(r, g, b);
            }

            float avgDepth = (d0 + d1 + d2) / 3f;
            faces.Add((p0, p1, p2, avgDepth, vertTint));
        }

        faces.Sort(static (a, b) => b.Depth.CompareTo(a.Depth));

        var edgePen = new Pen(new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)), 0.75);
        for (var i = 0; i < faces.Count; i++)
        {
            (Point p0, Point p1, Point p2, _, Color fill) = faces[i];
            DrawPolygon(context, [p0, p1, p2], new SolidColorBrush(fill), edgePen);
        }
    }

    private void DrawProjectedGizmo(DrawingContext context, float w, float h)
    {
        Vector3 origin = ResolveCurrentGizmoAnchor(w, h);
        if (!_camera.TryProjectToScreen(origin, w, h, out Vector2 originScreen, out _))
        {
            return;
        }

        Matrix4x4 basis = TransformGizmoController.ResolveBasisRotation(_state.GizmoMode, _state.GizmoSpace, _state.RotationDegrees);
        (Vector3 axisX, Vector3 axisY, Vector3 axisZ) = TransformGizmoMeshBuilder.ExtractBasisAxes(in basis);
        float s = TransformGizmoMeshBuilder.ComputeScreenConstantScale(origin, _camera);

        Color colX = ResolveAxisColor(TransformGizmoAxis.X, Color.Parse("#EF4444"));
        Color colY = ResolveAxisColor(TransformGizmoAxis.Y, Color.Parse("#22C55E"));
        Color colZ = ResolveAxisColor(TransformGizmoAxis.Z, Color.Parse("#3B82F6"));

        switch (_state.GizmoMode)
        {
            case TransformGizmoMode.Translate:
            {
                DrawScreenPlanarQuad(context, origin, axisX, axisY, s, TransformGizmoAxis.XY, Color.Parse("#3B82F6"), w, h);
                DrawScreenPlanarQuad(context, origin, axisX, axisZ, s, TransformGizmoAxis.XZ, Color.Parse("#22C55E"), w, h);
                DrawScreenPlanarQuad(context, origin, axisY, axisZ, s, TransformGizmoAxis.YZ, Color.Parse("#EF4444"), w, h);

                DrawScreenAxisArrow(context, originScreen, origin, axisX, s * 1.12f, colX, "X", w, h);
                DrawScreenAxisArrow(context, originScreen, origin, axisY, s * 1.12f, colY, "Y", w, h);
                DrawScreenAxisArrow(context, originScreen, origin, axisZ, s * 1.12f, colZ, "Z", w, h);
                break;
            }

            case TransformGizmoMode.Rotate:
            {
                DrawScreenRing(context, origin, axisY, axisZ, s * 0.92f, colX, w, h);
                DrawScreenRing(context, origin, axisX, axisZ, s * 0.92f, colY, w, h);
                DrawScreenRing(context, origin, axisX, axisY, s * 0.92f, colZ, w, h);
                break;
            }

            case TransformGizmoMode.Scale:
            {
                DrawScreenScaleHandle(context, originScreen, origin, axisX, s * 1.0f, colX, "X", w, h);
                DrawScreenScaleHandle(context, originScreen, origin, axisY, s * 1.0f, colY, "Y", w, h);
                DrawScreenScaleHandle(context, originScreen, origin, axisZ, s * 1.0f, colZ, "Z", w, h);

                Color centerCol = ResolveAxisColor(TransformGizmoAxis.Uniform, Color.Parse("#E2E8F0"));
                var centerRect = new Rect(originScreen.X - 5, originScreen.Y - 5, 10, 10);
                context.DrawRectangle(new SolidColorBrush(centerCol), new Pen(Brushes.Black, 1), centerRect, 2, 2);
                break;
            }
        }

        context.DrawEllipse(
            new SolidColorBrush(Color.Parse("#F8FAFC")),
            new Pen(new SolidColorBrush(Color.Parse("#0F172A")), 1),
            new Point(originScreen.X, originScreen.Y),
            3.5,
            3.5);
    }

    private Vector3 ResolveCurrentGizmoAnchor(float w, float h)
    {
        if (_gizmo.IsDragging)
        {
            return _dragStartGizmoAnchor + (_state.Position - _dragStartPosition);
        }

        VoxelMesh mesh = _state.MeshGeometry;
        return TransformGizmoMeshBuilder.ResolveGizmoAnchor(
            _state.Position,
            _state.RotationDegrees,
            _state.Scale,
            in mesh,
            _camera,
            w,
            h);
    }

    private Color ResolveAxisColor(TransformGizmoAxis axis, Color baseColor)
    {
        if (_gizmo.ActiveAxis == axis)
        {
            return Color.Parse("#FACC15");
        }

        if (_gizmo.HoveredAxis == axis)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, baseColor.R + 55),
                (byte)Math.Min(255, baseColor.G + 55),
                (byte)Math.Min(255, baseColor.B + 55));
        }

        return baseColor;
    }

    private void DrawScreenPlanarQuad(
        DrawingContext context,
        Vector3 origin,
        Vector3 u,
        Vector3 v,
        float scale,
        TransformGizmoAxis axis,
        Color baseColor,
        float w,
        float h)
    {
        float min = 0.24f * scale;
        float max = 0.48f * scale;
        if (!_camera.TryProjectToScreen(origin + (u * min) + (v * min), w, h, out Vector2 s00, out _)
            || !_camera.TryProjectToScreen(origin + (u * max) + (v * min), w, h, out Vector2 s10, out _)
            || !_camera.TryProjectToScreen(origin + (u * max) + (v * max), w, h, out Vector2 s11, out _)
            || !_camera.TryProjectToScreen(origin + (u * min) + (v * max), w, h, out Vector2 s01, out _))
        {
            return;
        }

        Color resolved = ResolveAxisColor(axis, baseColor);
        byte alpha = _gizmo.ActiveAxis == axis || _gizmo.HoveredAxis == axis ? (byte)135 : (byte)75;
        DrawPolygon(
            context,
            [new Point(s00.X, s00.Y), new Point(s10.X, s10.Y), new Point(s11.X, s11.Y), new Point(s01.X, s01.Y)],
            new SolidColorBrush(Color.FromArgb(alpha, resolved.R, resolved.G, resolved.B)),
            new Pen(new SolidColorBrush(Color.FromArgb(200, resolved.R, resolved.G, resolved.B)), 1.2));
    }

    private void DrawScreenAxisArrow(
        DrawingContext context,
        Vector2 originScreen,
        Vector3 origin,
        Vector3 dir,
        float length,
        Color color,
        string label,
        float w,
        float h)
    {
        if (!_camera.TryProjectToScreen(origin + (dir * length), w, h, out Vector2 tipScreen, out _))
        {
            return;
        }

        var brush = new SolidColorBrush(color);
        var pen = new Pen(brush, 2.4, lineCap: PenLineCap.Round);
        var p0 = new Point(originScreen.X, originScreen.Y);
        var p1 = new Point(tipScreen.X, tipScreen.Y);
        context.DrawLine(pen, p0, p1);

        Vector2 delta = tipScreen - originScreen;
        float len = delta.Length();
        if (len > 4f)
        {
            Vector2 d = delta / len;
            Vector2 perp = new(-d.Y, d.X);
            Point a0 = p1;
            Point a1 = new(p1.X - (d.X * 10) + (perp.X * 4.5), p1.Y - (d.Y * 10) + (perp.Y * 4.5));
            Point a2 = new(p1.X - (d.X * 10) - (perp.X * 4.5), p1.Y - (d.Y * 10) - (perp.Y * 4.5));
            DrawPolygon(context, [a0, a1, a2], brush, null);
        }

        var tag = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter, Liberation Sans"), 10, brush);
        context.DrawText(tag, new Point(p1.X + 4, p1.Y - (tag.Height * 0.5)));
    }

    private void DrawScreenScaleHandle(
        DrawingContext context,
        Vector2 originScreen,
        Vector3 origin,
        Vector3 dir,
        float length,
        Color color,
        string label,
        float w,
        float h)
    {
        if (!_camera.TryProjectToScreen(origin + (dir * length), w, h, out Vector2 tipScreen, out _))
        {
            return;
        }

        var brush = new SolidColorBrush(color);
        var pen = new Pen(brush, 2.2, lineCap: PenLineCap.Round);
        context.DrawLine(pen, new Point(originScreen.X, originScreen.Y), new Point(tipScreen.X, tipScreen.Y));
        context.DrawRectangle(brush, new Pen(Brushes.Black, 0.8), new Rect(tipScreen.X - 4.5, tipScreen.Y - 4.5, 9, 9), 1.5, 1.5);

        var tag = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter, Liberation Sans"), 10, brush);
        context.DrawText(tag, new Point(tipScreen.X + 6, tipScreen.Y - (tag.Height * 0.5)));
    }

    private void DrawScreenRing(
        DrawingContext context,
        Vector3 origin,
        Vector3 u,
        Vector3 v,
        float radius,
        Color color,
        float w,
        float h)
    {
        const int segments = 40;
        var pen = new Pen(new SolidColorBrush(color), 2.3, lineCap: PenLineCap.Round);
        Vector2? prev = null;
        for (var i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * MathF.Tau;
            Vector3 p = origin + (((u * MathF.Cos(angle)) + (v * MathF.Sin(angle))) * radius);
            if (_camera.TryProjectToScreen(p, w, h, out Vector2 sp, out _))
            {
                if (prev.HasValue)
                {
                    context.DrawLine(pen, new Point(prev.Value.X, prev.Value.Y), new Point(sp.X, sp.Y));
                }

                prev = sp;
            }
            else
            {
                prev = null;
            }
        }
    }

    private void DrawProjectedLine(DrawingContext context, Vector3 a, Vector3 b, float w, float h, Pen pen)
    {
        if (_camera.TryProjectToScreen(a, w, h, out Vector2 pa, out _)
            && _camera.TryProjectToScreen(b, w, h, out Vector2 pb, out _))
        {
            context.DrawLine(pen, new Point(pa.X, pa.Y), new Point(pb.X, pb.Y));
        }
    }

    private static bool ProjectClipPoint(
        Vector3 worldPos,
        in Matrix4x4 viewProj,
        float width,
        float height,
        out Point screenPoint,
        out float ndcDepth)
    {
        Vector4 clip = Vector4.Transform(new Vector4(worldPos, 1f), viewProj);
        if (clip.W <= 1e-4f)
        {
            screenPoint = default;
            ndcDepth = -1f;
            return false;
        }

        float invW = 1f / clip.W;
        float ndcX = clip.X * invW;
        float ndcY = clip.Y * invW;
        ndcDepth = clip.Z * invW;
        screenPoint = new Point(
            (ndcX * 0.5f + 0.5f) * width,
            (0.5f - ndcY * 0.5f) * height);
        return true;
    }

    private static void DrawPolygon(DrawingContext context, IReadOnlyList<Point> points, IBrush? fill, Pen? pen)
    {
        if (points.Count < 3)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (StreamGeometryContext g = geometry.Open())
        {
            g.BeginFigure(points[0], isFilled: fill != null);
            for (var i = 1; i < points.Count; i++)
            {
                g.LineTo(points[i]);
            }

            g.EndFigure(isClosed: true);
        }

        context.DrawGeometry(fill, pen, geometry);
    }

    private Bitmap? EnsureBodySpriteBitmap(string? path)
    {
        return EnsureCachedBitmap(path, ref _cachedBodyPath, ref _cachedBodyWriteUtc, ref _cachedBodyBitmap);
    }

    private static Bitmap? EnsureCachedBitmap(
        string? path,
        ref string? cachedPath,
        ref DateTime cachedWriteUtc,
        ref Bitmap? cachedBitmap)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            DisposeCachedBitmap(ref cachedBitmap, ref cachedPath);
            return null;
        }

        DateTime lastWrite = File.GetLastWriteTimeUtc(path);
        if (cachedBitmap != null && string.Equals(cachedPath, path, StringComparison.OrdinalIgnoreCase) && cachedWriteUtc == lastWrite)
        {
            return cachedBitmap;
        }

        DisposeCachedBitmap(ref cachedBitmap, ref cachedPath);
        try
        {
            cachedBitmap = new Bitmap(path);
            cachedPath = path;
            cachedWriteUtc = lastWrite;
            return cachedBitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed loading preview sprite bitmap '{path}': {ex.Message}");
            return null;
        }
    }

    private static void DisposeCachedBitmap(ref Bitmap? bitmap, ref string? path)
    {
        bitmap?.Dispose();
        bitmap = null;
        path = null;
    }

    private static Color ParseItemBaseTint(string? hex)
    {
        if (!string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex, out Color parsed))
        {
            return parsed;
        }

        return Color.Parse("#60A5FA");
    }
}
