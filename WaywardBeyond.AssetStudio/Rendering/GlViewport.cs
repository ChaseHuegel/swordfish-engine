using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Avalonia.Threading;
using Swordfish.Library.Util;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Meshing;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Avalonia OpenGL control handling context lifecycle, DPI sizing, orbit/pan/zoom input, and invalidation while delegating all GL calls to <see cref="SceneRenderer"/>.</summary>
public sealed class GlViewport : OpenGlControlBase, ICustomHitTest
{
    private const float ORBIT_SENSITIVITY = 0.45f;

    private static readonly Vector4 _defaultClearColor = new(0.085f, 0.095f, 0.115f, 1f);

    private readonly SceneRenderer _renderer = new();
    private readonly PreviewCamera _camera = new();

    private AlbedoTextureArrayData? _pendingAlbedo;
    private AlbedoTextureArrayData? _uploadedAlbedo;
    private StandaloneTextureData? _pendingStandaloneTexture;
    private IReadOnlyList<PreviewSceneMesh>? _pendingMeshes;
    private bool _albedoDirty;
    private bool _standaloneTextureDirty;
    private bool _meshesDirty;

    private bool _hasFramedBounds;
    private Vector3 _framedMin;
    private Vector3 _framedMax;
    private float _framedPadding = 1.25f;

    private bool _isDragging;
    private bool _isPanning;
    private Point _lastPointerPosition;

    private DispatcherTimer? _autoOrbitTimer;
    private bool _isAutoOrbiting;

    /// <summary>Initializes the OpenGL preview viewport with an interactive pointer cursor.</summary>
    public GlViewport()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>Raised when the underlying OpenGL context finishes initializing and reports driver metadata.</summary>
    public event Action<GlInfo>? GlInitialized;

    /// <summary>Raised when preview initialization or rendering encounters a non-fatal OpenGL error.</summary>
    public event Action<string>? GlErrorOccurred;

    /// <summary>Gets the orbit camera state driving this viewport.</summary>
    public PreviewCamera Camera => _camera;

    /// <summary>Gets the currently active preview surface mode.</summary>
    public PreviewSurfaceMode SurfaceMode { get; private set; } = PreviewSurfaceMode.SingleBrick;

    /// <summary>Gets the active cardinal brick orientation for brick previews.</summary>
    public BrickOrientation ActiveOrientation { get; private set; } = BrickOrientation.North;

    /// <summary>Gets the active 3D geometry primitive for material previews.</summary>
    public MaterialPreviewShape ActiveMaterialShape { get; private set; } = MaterialPreviewShape.Sphere;

    /// <summary>Gets or sets the RGBA viewport background clear color.</summary>
    public Vector4 ClearColor { get; set; } = _defaultClearColor;

    /// <summary>Gets the active OpenGL vendor and renderer info once initialized.</summary>
    public GlInfo? ActiveGlInfo => _renderer.Info;

    /// <summary>Gets the most recent non-fatal OpenGL error message, or null when healthy.</summary>
    public string? LastError { get; private set; }

    /// <summary>Gets whether continuous automatic orbit rotation is currently active.</summary>
    public bool IsAutoOrbiting => _isAutoOrbiting;

    /// <inheritdoc/>
    public bool HitTest(Point point) =>
        point.X >= 0 && point.Y >= 0 && point.X <= Bounds.Width && point.Y <= Bounds.Height;

    /// <summary>Starts or stops continuous horizontal orbit rotation of the preview camera.</summary>
    public void SetAutoOrbit(bool enabled)
    {
        _isAutoOrbiting = enabled;
        if (!enabled)
        {
            _autoOrbitTimer?.Stop();
            return;
        }

        if (_autoOrbitTimer == null)
        {
            _autoOrbitTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33),
            };
            _autoOrbitTimer.Tick += HandleAutoOrbitTick;
        }

        if (!_autoOrbitTimer.IsEnabled)
        {
            _autoOrbitTimer.Start();
        }
    }

    /// <summary>Queues a new albedo texture array to upload on the next render frame.</summary>
    public void SetAlbedoArray(AlbedoTextureArrayData data)
    {
        _pendingAlbedo = data;
        _albedoDirty = !ReferenceEquals(_uploadedAlbedo, data);
        RequestNextFrameRendering();
    }

    /// <summary>Queues a standalone 2D texture to upload on the next render frame for item and material previews.</summary>
    public void SetStandaloneTexture(StandaloneTextureData data)
    {
        _pendingStandaloneTexture = data;
        _standaloneTextureDirty = true;
        RequestNextFrameRendering();
    }

    /// <summary>Submits a single brick preview mesh at <paramref name="orientation"/> and optionally updates camera framing.</summary>
    public Result SetSingleBrick(
        BrickPreviewMeshBuilder builder,
        Brick brick,
        BrickShape shape,
        BrickOrientation orientation,
        bool preserveCamera = false
    ) {
        SurfaceMode = PreviewSurfaceMode.SingleBrick;
        ActiveOrientation = orientation;
        Result<VoxelMesh> built = builder.Build(brick, shape, ToVoxelOrientation(orientation));
        if (!built.Success)
        {
            return Result.FromFailure(built.Message);
        }

        bool doubleSided = brick.Alpha != BrickAlpha.Opaque;
        PreviewSceneMesh[] scene = [new PreviewSceneMesh(built.Value, Matrix4x4.Identity, brick.Alpha, 0.5f, doubleSided)];
        _hasFramedBounds = false;
        if (!preserveCamera)
        {
            _camera.ApplyPreset(PreviewCameraPreset.OrbitBrick);
        }

        QueueSceneMeshes(scene);
        return Result.FromSuccess();
    }

    /// <summary>Submits a six-shape grid preview for a <see cref="BrickShape.Any"/> brick sharing <paramref name="orientation"/>.</summary>
    public Result SetBrickShapeGrid(
        BrickPreviewMeshBuilder builder,
        Brick brick,
        BrickOrientation orientation,
        bool preserveCamera = false
    ) {
        SurfaceMode = PreviewSurfaceMode.BrickShapeGrid;
        ActiveOrientation = orientation;
        Result<IReadOnlyList<PreviewSceneMesh>> grid = PreviewSceneMesh.BuildShapeAnyGrid(builder, brick, ToVoxelOrientation(orientation));
        if (!grid.Success)
        {
            return Result.FromFailure(grid.Message);
        }

        (Vector3 min, Vector3 max) = PreviewSceneMesh.ComputeBounds(grid.Value);
        if (!preserveCamera)
        {
            _camera.ApplyPreset(PreviewCameraPreset.BrickShapeGrid);
            ApplyFramedBounds(in min, in max, paddingFactor: 1.2f);
        }
        else
        {
            UpdateFramedBoundsOnly(in min, in max, paddingFactor: 1.2f);
        }

        QueueSceneMeshes(grid.Value);
        return Result.FromSuccess();
    }

    /// <summary>Builds and submits a 5x5x1 connected-texture showcase displaying all 16 cardinal adjacency masks.</summary>
    public Result SetConnectedBrickShowcase(
        RegionMeshBuilder builder,
        Brick brick,
        BrickOrientation orientation,
        ushort brickDataId = 0,
        bool preserveCamera = false
    ) {
        if (builder == null)
        {
            return Result.FromFailure("RegionMeshBuilder is null.");
        }

        if (brick == null)
        {
            return Result.FromFailure("Brick is null.");
        }

        SurfaceMode = PreviewSurfaceMode.BrickConnectedShowcase;
        ActiveOrientation = orientation;

        ushort resolvedId = brickDataId != 0 ? brickDataId : brick.DataID;
        Result<VoxelRegionData> regionResult = ConnectedBrickShowcaseBuilder.BuildRegion(resolvedId, ToVoxelOrientation(orientation));
        if (!regionResult.Success)
        {
            return Result.FromFailure(regionResult.Message);
        }

        VoxelRegionData region = regionResult.Value;
        Result<VoxelMesh> built = builder.Build(in region);
        if (!built.Success)
        {
            return Result.FromFailure(built.Message);
        }

        bool doubleSided = brick.Alpha != BrickAlpha.Opaque;
        PreviewSceneMesh[] scene = [new PreviewSceneMesh(built.Value, Matrix4x4.Identity, brick.Alpha, 0.5f, doubleSided)];
        (Vector3 min, Vector3 max) = PreviewSceneMesh.ComputeBounds(scene);
        if (!preserveCamera)
        {
            _camera.ApplyPreset(PreviewCameraPreset.ConnectedShowcase);
            ApplyFramedBounds(in min, in max, paddingFactor: 1.18f);
        }
        else
        {
            UpdateFramedBoundsOnly(in min, in max, paddingFactor: 1.18f);
        }

        QueueSceneMeshes(scene);
        return Result.FromSuccess();
    }

    /// <summary>Builds and submits a 5x5x1 connected-texture showcase by resolving <paramref name="brick"/> in <paramref name="brickRegistry"/>.</summary>
    public Result SetConnectedBrickShowcase(
        RegionMeshBuilder builder,
        IBrickRegistry brickRegistry,
        Brick brick,
        BrickOrientation orientation,
        bool preserveCamera = false
    ) {
        ushort resolvedId = brickRegistry != null && brick != null ? brickRegistry.Id(brick.ID) : (ushort)0;
        return SetConnectedBrickShowcase(builder, brick!, orientation, resolvedId, preserveCamera);
    }

    /// <summary>Submits an item view-model or world-model mesh using its corresponding framing preset and optional standalone 2D texture.</summary>
    public void SetItemPreview(
        PreviewSceneMesh itemMesh,
        bool isWorldModel,
        StandaloneTextureData? standaloneTexture = null,
        bool preserveCamera = false)
    {
        SurfaceMode = isWorldModel ? PreviewSurfaceMode.ItemWorldModel : PreviewSurfaceMode.ItemViewModel;

        PreviewSceneMesh effectiveMesh = itemMesh;
        if (standaloneTexture.HasValue && standaloneTexture.Value.IsValid)
        {
            _pendingStandaloneTexture = standaloneTexture.Value;
            _standaloneTextureDirty = true;
            if (!effectiveMesh.UseStandaloneTexture)
            {
                effectiveMesh = new PreviewSceneMesh(
                    effectiveMesh.Geometry,
                    effectiveMesh.Model,
                    effectiveMesh.AlphaMode,
                    effectiveMesh.AlphaCutoff,
                    effectiveMesh.DoubleSided,
                    useStandaloneTexture: true);
            }
        }
        else if (effectiveMesh.UseStandaloneTexture)
        {
            _pendingStandaloneTexture = StandaloneTextureLoader.CreateMissingCheckerboard();
            _standaloneTextureDirty = true;
        }

        PreviewSceneMesh[] scene = [effectiveMesh];
        (Vector3 min, Vector3 max) = PreviewSceneMesh.ComputeBounds(scene);
        float padding = isWorldModel ? 1.35f : 1.15f;
        if (!preserveCamera)
        {
            _camera.ApplyPreset(isWorldModel ? PreviewCameraPreset.ItemWorldModel : PreviewCameraPreset.ItemViewModel);
            ApplyFramedBounds(in min, in max, paddingFactor: padding);
        }
        else
        {
            UpdateFramedBoundsOnly(in min, in max, paddingFactor: padding);
        }

        QueueSceneMeshes(scene);
    }

    /// <summary>Submits a textured 3D preview mesh for a material asset using a standalone 2D texture.</summary>
    public void SetMaterialPreview(
        StandaloneTextureData? texture,
        BrickAlpha alphaMode = BrickAlpha.Opaque,
        float alphaCutoff = 0.5f,
        bool preserveCamera = false,
        MaterialPreviewShape shape = MaterialPreviewShape.Sphere)
    {
        SurfaceMode = PreviewSurfaceMode.MaterialPreview;
        ActiveMaterialShape = shape;
        _pendingStandaloneTexture = texture.HasValue && texture.Value.IsValid
            ? texture.Value
            : StandaloneTextureLoader.CreateMissingCheckerboard();
        _standaloneTextureDirty = true;

        VoxelMesh mesh = shape switch
        {
            MaterialPreviewShape.Plane => PreviewGeometryFactory.CreateTexturedPlane(),
            MaterialPreviewShape.Cube => PreviewGeometryFactory.CreateTexturedCube(),
            _ => PreviewGeometryFactory.CreateTexturedSphere(),
        };
        bool doubleSided = shape == MaterialPreviewShape.Plane || alphaMode != BrickAlpha.Opaque;
        float clampedCutoff = Math.Clamp(alphaCutoff, 0f, 1f);
        PreviewSceneMesh[] scene =
        [
            new PreviewSceneMesh(
                mesh,
                Matrix4x4.Identity,
                alphaMode,
                clampedCutoff,
                doubleSided,
                useStandaloneTexture: true),
        ];

        _hasFramedBounds = false;
        if (!preserveCamera)
        {
            _camera.ApplyPreset(PreviewCameraPreset.OrbitBrick);
        }

        QueueSceneMeshes(scene);
    }

    /// <summary>Builds and submits a voxel blueprint region preview and auto-frames its bounding volume.</summary>
    public Result SetBlueprintPreview(RegionMeshBuilder builder, in VoxelRegionData region, bool preserveCamera = false)
    {
        SurfaceMode = PreviewSurfaceMode.BlueprintVoxel;
        Result<VoxelMesh> built = builder.Build(in region);
        if (!built.Success)
        {
            return Result.FromFailure(built.Message);
        }

        PreviewSceneMesh[] scene = [new PreviewSceneMesh(built.Value, Matrix4x4.Identity, BrickAlpha.Masked, 0.5f, doubleSided: false)];
        (Vector3 min, Vector3 max) = PreviewSceneMesh.ComputeBounds(scene);
        if (!preserveCamera)
        {
            _camera.ApplyPreset(PreviewCameraPreset.Blueprint);
            ApplyFramedBounds(in min, in max, paddingFactor: 1.15f);
        }
        else
        {
            UpdateFramedBoundsOnly(in min, in max, paddingFactor: 1.15f);
        }

        QueueSceneMeshes(scene);
        return Result.FromSuccess();
    }

    /// <summary>Advances <see cref="ActiveOrientation"/> clockwise by 90 degrees.</summary>
    public BrickOrientation CycleOrientation()
    {
        ActiveOrientation = ActiveOrientation switch
        {
            BrickOrientation.North => BrickOrientation.East,
            BrickOrientation.East => BrickOrientation.South,
            BrickOrientation.South => BrickOrientation.West,
            _ => BrickOrientation.North,
        };
        return ActiveOrientation;
    }

    /// <summary>Resets the orbit camera to the active preset and schedules a redraw.</summary>
    public void ResetCamera()
    {
        _camera.Reset();
        if (_hasFramedBounds)
        {
            _camera.FrameBounds(in _framedMin, in _framedMax, _framedPadding);
        }

        RequestNextFrameRendering();
    }

    /// <summary>Maps a studio cardinal <see cref="BrickOrientation"/> to a voxel <see cref="Orientation"/>.</summary>
    public static Orientation ToVoxelOrientation(BrickOrientation orientation)
    {
        return orientation switch
        {
            BrickOrientation.East => new Orientation(pitch: 0, yaw: 1, roll: 0),
            BrickOrientation.South => new Orientation(pitch: 0, yaw: 2, roll: 0),
            BrickOrientation.West => new Orientation(pitch: 0, yaw: 3, roll: 0),
            _ => Orientation.Identity,
        };
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsEffectivelyVisible && _renderer.IsInitialized)
        {
            RequestNextFrameRendering();
        }
    }

    /// <inheritdoc/>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        _uploadedAlbedo = null;
        bool isGles = GlVersion.Type == GlProfileType.OpenGLES;
        Result<GlInfo> initResult = _renderer.Initialize(gl.GetProcAddress, isGles);
        if (!initResult.Success)
        {
            ReportError(initResult.Message);
            return;
        }

        LastError = null;
        _albedoDirty = _pendingAlbedo != null;
        _standaloneTextureDirty = _pendingStandaloneTexture.HasValue;
        _meshesDirty = _pendingMeshes != null;
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
            Result uploadResult = _renderer.UploadAlbedoArray(_pendingAlbedo);
            if (!uploadResult.Success)
            {
                ReportError(uploadResult.Message);
                return;
            }

            _uploadedAlbedo = _pendingAlbedo;
            _albedoDirty = false;
        }

        if (_standaloneTextureDirty && _pendingStandaloneTexture.HasValue)
        {
            Result standaloneResult = _renderer.UploadStandaloneTexture(_pendingStandaloneTexture.Value);
            if (!standaloneResult.Success)
            {
                ReportError(standaloneResult.Message);
                return;
            }

            _standaloneTextureDirty = false;
        }

        if (_meshesDirty && _pendingMeshes != null)
        {
            Result meshResult = _renderer.SetSceneMeshes(_pendingMeshes);
            if (!meshResult.Success)
            {
                ReportError(meshResult.Message);
                return;
            }

            _meshesDirty = false;
        }

        double scaling = VisualRoot != null ? VisualRoot.RenderScaling : 1.0;
        int pixelWidth = Math.Max(1, (int)(Bounds.Width * scaling));
        int pixelHeight = Math.Max(1, (int)(Bounds.Height * scaling));

        Result renderResult = _renderer.RenderToFramebuffer((uint)fb, pixelWidth, pixelHeight, _camera, ClearColor);
        if (!renderResult.Success)
        {
            ReportError(renderResult.Message);
            return;
        }

        LastError = null;
    }

    /// <inheritdoc/>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _uploadedAlbedo = null;
        _renderer.Deinitialize();
    }

    /// <inheritdoc/>
    protected override void OnOpenGlLost()
    {
        _uploadedAlbedo = null;
        _renderer.HandleContextLost();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isAutoOrbiting = false;
        _autoOrbitTimer?.Stop();
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.ClickCount >= 2)
        {
            _isDragging = false;
            _isPanning = false;
            e.Pointer.Capture(null);
            ResetCamera();
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
        float dx = (float)(current.X - _lastPointerPosition.X);
        float dy = (float)(current.Y - _lastPointerPosition.Y);
        _lastPointerPosition = current;

        if (_isPanning)
        {
            _camera.Pan(dx, dy);
        }
        else
        {
            _camera.Orbit(-dx * ORBIT_SENSITIVITY, dy * ORBIT_SENSITIVITY);
        }

        RequestNextFrameRendering();
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
        float delta = (float)e.Delta.Y;
        if (MathF.Abs(delta) <= 1e-5f && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            delta = (float)e.Delta.X;
        }

        if (MathF.Abs(delta) > 1e-5f)
        {
            _camera.Zoom(delta);
            RequestNextFrameRendering();
        }

        e.Handled = true;
    }

    private void HandleAutoOrbitTick(object? sender, EventArgs e)
    {
        if (!_isAutoOrbiting || !IsEffectivelyVisible || _isDragging || _isPanning)
        {
            return;
        }

        _camera.Orbit(1.2f, 0f);
        RequestNextFrameRendering();
    }

    private void ApplyFramedBounds(in Vector3 min, in Vector3 max, float paddingFactor)
    {
        UpdateFramedBoundsOnly(in min, in max, paddingFactor);
        _camera.FrameBounds(in min, in max, paddingFactor);
    }

    private void UpdateFramedBoundsOnly(in Vector3 min, in Vector3 max, float paddingFactor)
    {
        _hasFramedBounds = true;
        _framedMin = min;
        _framedMax = max;
        _framedPadding = paddingFactor;
    }

    private void QueueSceneMeshes(IReadOnlyList<PreviewSceneMesh> meshes)
    {
        _pendingMeshes = meshes;
        _meshesDirty = true;
        RequestNextFrameRendering();
    }

    private void ReportError(string? message)
    {
        string text = string.IsNullOrWhiteSpace(message) ? "OpenGL preview error." : message;
        Trace.TraceWarning($"GlViewport OpenGL error: {text}");
        LastError = text;
        GlErrorOccurred?.Invoke(text);
    }
}
