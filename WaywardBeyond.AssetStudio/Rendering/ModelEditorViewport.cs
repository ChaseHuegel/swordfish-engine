using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Meshing;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Live OpenGL viewport for interactive ViewModel and WorldModel transform editing with 3D gizmos.</summary>
public sealed class ModelEditorViewport : OpenGlControlBase, ICustomHitTest
{
    private const float ORBIT_SENSITIVITY = 0.45f;

    private static readonly Cursor _defaultCursor = Cursor.Default;
    private static readonly Cursor _handCursor = new(StandardCursorType.Hand);
    private static readonly Cursor _gizmoCursor = new(StandardCursorType.SizeAll);
    private static readonly Vector4 _defaultClearColor = new(0.068f, 0.078f, 0.098f, 1f);

    private readonly SceneRenderer _renderer = new();
    private readonly PreviewCamera _camera = new();
    private readonly TransformGizmoController _gizmo = new();

    private ModelEditorRenderState _state = ModelEditorRenderState.Default;
    private AlbedoTextureArrayData? _pendingAlbedo;
    private AlbedoTextureArrayData? _uploadedAlbedo;
    private StandaloneTextureData? _pendingItemTexture;
    private StandaloneTextureData? _uploadedItemTexture;
    private StandaloneTextureData? _pendingReferenceTexture;
    private StandaloneTextureData? _uploadedReferenceTexture;
    private bool _albedoDirty;
    private bool _itemTextureDirty;
    private bool _referenceTextureDirty;
    private bool _sceneDirty = true;
    private bool _lastRenderSucceeded;

    private bool _isOrbiting;
    private bool _isPanning;
    private bool _hasCameraModeInitialized;
    private bool _lastIsWorldModel;
    private Point _lastPointerPos;
    private Vector3 _dragStartGizmoAnchor;
    private Vector3 _dragStartPosition;

    /// <summary>Raised when the underlying OpenGL context finishes initializing.</summary>
    public event Action<GlInfo>? GlInitialized;

    /// <summary>Raised when OpenGL initialization or rendering reports a non-fatal error.</summary>
    public event Action<string>? GlErrorOccurred;

    /// <summary>Raised when a live OpenGL frame finishes rendering cleanly.</summary>
    public event Action? FrameRendered;

    /// <summary>Raised when the user starts dragging a 3D gizmo handle.</summary>
    public event Action? GizmoDragStarted;

    /// <summary>Raised during a 3D gizmo drag with updated Position, Rotation, Scale, and readout caption.</summary>
    public event Action<Vector3, Vector3, Vector3, string?>? TransformDragged;

    /// <summary>Raised when the user releases a 3D gizmo handle.</summary>
    public event Action<Vector3, Vector3, Vector3>? GizmoDragEnded;

    /// <summary>Gets or sets the RGBA clear color behind the scene.</summary>
    public Vector4 ClearColor { get; set; } = _defaultClearColor;

    /// <summary>True when the underlying OpenGL context and shader pipeline have initialized cleanly.</summary>
    public bool IsGlInitialized => _renderer.IsInitialized;

    /// <inheritdoc/>
    public bool HitTest(Point point) =>
        point.X >= 0 && point.Y >= 0 && point.X <= Bounds.Width && point.Y <= Bounds.Height;

    /// <summary>Queues a new block albedo texture array to upload before the next frame.</summary>
    public void SetAlbedoArray(AlbedoTextureArrayData? data)
    {
        if (data == null)
        {
            return;
        }

        _pendingAlbedo = data;
        _albedoDirty = !ReferenceEquals(_uploadedAlbedo, data);
        RequestNextFrameRendering();
    }

    /// <summary>Updates the active render state (transform, camera FOV, reference toggles, and mesh) and schedules redraw.</summary>
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

        _pendingItemTexture = state.ItemStandaloneTexture is { IsValid: true } validItem ? validItem : null;
        if (_pendingItemTexture.HasValue)
        {
            _itemTextureDirty = !TexturesMatch(_uploadedItemTexture, _pendingItemTexture);
        }
        else if (state.TextureMode == PreviewTextureMode.StandaloneTexture)
        {
            _pendingItemTexture = StandaloneTextureLoader.CreateMissingCheckerboard();
            _itemTextureDirty = !TexturesMatch(_uploadedItemTexture, _pendingItemTexture);
        }
        else
        {
            _itemTextureDirty = false;
        }

        _pendingReferenceTexture = state.ReferenceBodyTexture is { IsValid: true } validRef ? validRef : null;
        _referenceTextureDirty = _pendingReferenceTexture.HasValue && !TexturesMatch(_uploadedReferenceTexture, _pendingReferenceTexture);

        Cursor = state.IsWorldModel ? _handCursor : _defaultCursor;
        _sceneDirty = true;
        RequestNextFrameRendering();
    }

    /// <summary>Resets the WorldModel orbit camera to the human-scale default framing.</summary>
    public void ResetOrbitCamera()
    {
        if (_state.IsWorldModel)
        {
            _camera.ApplyPreset(PreviewCameraPreset.ItemWorldModelEditor);
            _sceneDirty = true;
            RequestNextFrameRendering();
        }
    }

    /// <summary>Clears cached session camera state so the next <see cref="UpdateEditorState"/> applies the active mode preset.</summary>
    public void ResetCameraForNewSession()
    {
        _hasCameraModeInitialized = false;
        _isOrbiting = false;
        _isPanning = false;
        _lastRenderSucceeded = false;
        _sceneDirty = true;
    }

    /// <summary>Marks the scene dirty and schedules a fresh OpenGL frame for the visible editor session.</summary>
    public void EnsureRenderedForVisibleSession()
    {
        _sceneDirty = true;
        RequestNextFrameRendering();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsEffectivelyVisible && _renderer.IsInitialized)
        {
            _sceneDirty = true;
            RequestNextFrameRendering();
        }
    }

    /// <inheritdoc/>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        _uploadedAlbedo = null;
        _uploadedItemTexture = null;
        _uploadedReferenceTexture = null;
        _lastRenderSucceeded = false;

        bool isGles = GlVersion.Type == GlProfileType.OpenGLES;
        Result<GlInfo> initResult = _renderer.Initialize(gl.GetProcAddress, isGles);
        if (!initResult.Success)
        {
            string message = initResult.Message ?? "OpenGL error.";
            Trace.TraceWarning($"ModelEditorViewport OpenGL initialization error: {message}");
            GlErrorOccurred?.Invoke(message);
            return;
        }

        _albedoDirty = _pendingAlbedo != null;
        _itemTextureDirty = _pendingItemTexture.HasValue;
        _referenceTextureDirty = _pendingReferenceTexture.HasValue;
        _sceneDirty = true;
        GlInitialized?.Invoke(initResult.Value);
        RequestNextFrameRendering();
    }

    /// <inheritdoc/>
    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (!_renderer.IsInitialized || !IsEffectivelyVisible || Bounds.Width < 1.0 || Bounds.Height < 1.0)
        {
            return;
        }

        if (_albedoDirty && _pendingAlbedo != null)
        {
            Result res = _renderer.UploadAlbedoArray(_pendingAlbedo);
            if (!res.Success)
            {
                _lastRenderSucceeded = false;
                string message = res.Message ?? "OpenGL error.";
                Trace.TraceWarning($"ModelEditorViewport OpenGL error: {message}");
                GlErrorOccurred?.Invoke(message);
                return;
            }

            _uploadedAlbedo = _pendingAlbedo;
            _albedoDirty = false;
        }

        if (_itemTextureDirty && _pendingItemTexture.HasValue)
        {
            Result res = _renderer.UploadStandaloneTexture(_pendingItemTexture.Value);
            if (!res.Success)
            {
                _lastRenderSucceeded = false;
                string message = res.Message ?? "OpenGL error.";
                Trace.TraceWarning($"ModelEditorViewport OpenGL error: {message}");
                GlErrorOccurred?.Invoke(message);
                return;
            }

            _uploadedItemTexture = _pendingItemTexture;
            _itemTextureDirty = false;
        }

        if (_referenceTextureDirty && _pendingReferenceTexture.HasValue)
        {
            Result res = _renderer.UploadReferenceTexture(_pendingReferenceTexture.Value);
            if (!res.Success)
            {
                _lastRenderSucceeded = false;
                string message = res.Message ?? "OpenGL error.";
                Trace.TraceWarning($"ModelEditorViewport OpenGL error: {message}");
                GlErrorOccurred?.Invoke(message);
                return;
            }

            _uploadedReferenceTexture = _pendingReferenceTexture;
            _referenceTextureDirty = false;
        }

        if (_sceneDirty)
        {
            IReadOnlyList<PreviewSceneMesh> scene = BuildCombinedScene();
            Result meshRes = _renderer.SetSceneMeshes(scene);
            if (!meshRes.Success)
            {
                _lastRenderSucceeded = false;
                string message = meshRes.Message ?? "OpenGL error.";
                Trace.TraceWarning($"ModelEditorViewport OpenGL error: {message}");
                GlErrorOccurred?.Invoke(message);
                return;
            }

            _sceneDirty = false;
        }

        double scaling = VisualRoot != null ? VisualRoot.RenderScaling : 1.0;
        int pixelWidth = Math.Max(1, (int)(Bounds.Width * scaling));
        int pixelHeight = Math.Max(1, (int)(Bounds.Height * scaling));

        Result renderResult = _renderer.RenderToFramebuffer((uint)fb, pixelWidth, pixelHeight, _camera, ClearColor);
        if (!renderResult.Success)
        {
            _lastRenderSucceeded = false;
            string message = renderResult.Message ?? "OpenGL error.";
            Trace.TraceWarning($"ModelEditorViewport OpenGL error: {message}");
            GlErrorOccurred?.Invoke(message);
            return;
        }

        if (!_lastRenderSucceeded)
        {
            _lastRenderSucceeded = true;
            FrameRendered?.Invoke();
        }
    }

    /// <inheritdoc/>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _uploadedAlbedo = null;
        _uploadedItemTexture = null;
        _uploadedReferenceTexture = null;
        _lastRenderSucceeded = false;
        _renderer.Deinitialize();
    }

    /// <inheritdoc/>
    protected override void OnOpenGlLost()
    {
        _uploadedAlbedo = null;
        _uploadedItemTexture = null;
        _uploadedReferenceTexture = null;
        _lastRenderSucceeded = false;
        _renderer.HandleContextLost();
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
                _lastPointerPos = point.Position;
                Cursor = _gizmoCursor;
                e.Pointer.Capture(this);
                GizmoDragStarted?.Invoke();
                _sceneDirty = true;
                RequestNextFrameRendering();
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
                _sceneDirty = true;
                RequestNextFrameRendering();
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

            _sceneDirty = true;
            RequestNextFrameRendering();
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
            _sceneDirty = true;
            RequestNextFrameRendering();
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
            _sceneDirty = true;
            RequestNextFrameRendering();
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
            _sceneDirty = true;
            RequestNextFrameRendering();
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
            _sceneDirty = true;
            RequestNextFrameRendering();
        }

        e.Handled = true;
    }

    private IReadOnlyList<PreviewSceneMesh> BuildCombinedScene()
    {
        var meshes = new List<PreviewSceneMesh>(8);

        if (_state.IsWorldModel)
        {
            if (_state.ShowFloorPlane)
            {
                meshes.AddRange(PreviewGeometryFactory.CreateWorldModelFloorPlane());
            }

            if (_state.ShowBodyReference && _state.ReferenceBodyTexture.HasValue && _state.ReferenceBodyTexture.Value.IsValid)
            {
                meshes.Add(PreviewGeometryFactory.CreateBodyReferencePlane(_state.ReferenceBodyAspect));
            }
        }

        if (!_state.MeshGeometry.IsEmpty)
        {
            Matrix4x4 itemModel = ModelTransformMath.CreateModelMatrix(_state.Position, _state.RotationDegrees, _state.Scale);
            meshes.Add(new PreviewSceneMesh(
                _state.MeshGeometry,
                itemModel,
                _state.AlphaMode,
                _state.AlphaCutoff,
                _state.DoubleSided,
                textureMode: _state.TextureMode,
                isOverlay: false));
        }

        if (!_state.IsReadOnly)
        {
            Vector3 gizmoAnchor = ResolveCurrentGizmoAnchor((float)Math.Max(Bounds.Width, 1.0), (float)Math.Max(Bounds.Height, 1.0));
            Matrix4x4 basis = TransformGizmoController.ResolveBasisRotation(_state.GizmoMode, _state.GizmoSpace, _state.RotationDegrees);
            float gizmoScale = TransformGizmoMeshBuilder.ComputeScreenConstantScale(gizmoAnchor, _camera);
            meshes.AddRange(TransformGizmoMeshBuilder.BuildMeshes(
                _state.GizmoMode,
                gizmoAnchor,
                in basis,
                gizmoScale,
                _gizmo.HoveredAxis,
                _gizmo.ActiveAxis));
        }

        return meshes;
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

    private static bool TexturesMatch(StandaloneTextureData? a, StandaloneTextureData? b)
    {
        if (!a.HasValue && !b.HasValue)
        {
            return true;
        }

        if (!a.HasValue || !b.HasValue)
        {
            return false;
        }

        return ReferenceEquals(a.Value.RgbaPixels, b.Value.RgbaPixels)
            && a.Value.Width == b.Value.Width
            && a.Value.Height == b.Value.Height;
    }
}
