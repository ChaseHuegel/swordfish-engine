using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Util;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.AssetStudio.ViewModels;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Meshing;
using WaywardBeyond.Voxels;
using ToolsWorkspace = WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Drives the live <see cref="GlViewport"/> from real workspace assets and tracks reported OpenGL driver telemetry.
/// </summary>
public sealed class StudioGlPreviewController : IPreviewSurfaceProvider
{
    private readonly Dictionary<string, CachedStandaloneTexture> _standaloneTextureCache = new(StringComparer.Ordinal);

    private string _backendLabel;
    private string _vendorRendererLabel;
    private ToolsWorkspace.WorkspaceIndex? _workspace;
    private IReadOnlyList<string> _assetRoots = [];
    private AlbedoTextureArrayData? _albedoArray;
    private WeakReference<GlViewport>? _albedoUploadedViewport;
    private StudioPreviewBrickDatabase? _brickDb;
    private StudioPreviewPaintDatabase? _paintDb;
    private StudioPreviewMeshDatabase? _meshDb;
    private BrickPreviewMeshBuilder? _brickBuilder;
    private RegionMeshBuilder? _regionBuilder;
    private string? _lastSyncedDocumentKey;
    private PreviewSurfaceMode? _lastSyncedSurfaceMode;
    private MaterialPreviewShape? _lastMaterialShape;
    private bool _hasResolvedDefaultBodyRef;
    private BodyReferenceSpriteInfo? _cachedDefaultBodyRef;

    /// <summary>
    ///     Initializes the live OpenGL preview controller with platform-appropriate default labels.
    /// </summary>
    public StudioGlPreviewController()
    {
        _backendLabel = OperatingSystem.IsWindows() ? "WGL · 330 core (ready)" : "GLX · 330 core (ready)";
        _vendorRendererLabel = "Software Preview Fallback (awaiting OpenGL context)";
    }

    /// <inheritdoc/>
    public event Action? BackendInfoChanged;

    /// <inheritdoc/>
    public string BackendLabel => _backendLabel;

    /// <inheritdoc/>
    public string VendorRendererLabel => _vendorRendererLabel;

    /// <inheritdoc/>
    public PreviewSurfaceMode ResolveSurfaceMode(AssetKind kind, string? shapeName, bool useItemWorldModel)
    {
        return ResolveSurfaceMode(kind, shapeName, useItemWorldModel, isConnectedShowcaseMode: false);
    }

    /// <summary>
    ///     Resolves the active preview surface mode including optional connected-texture showcase mode for bricks.
    /// </summary>
    public PreviewSurfaceMode ResolveSurfaceMode(
        AssetKind kind,
        string? shapeName,
        bool useItemWorldModel,
        bool isConnectedShowcaseMode)
    {
        return kind switch
        {
            AssetKind.Brick when isConnectedShowcaseMode => PreviewSurfaceMode.BrickConnectedShowcase,
            AssetKind.Brick when string.Equals(shapeName, "Any", StringComparison.OrdinalIgnoreCase) => PreviewSurfaceMode.BrickShapeGrid,
            AssetKind.Brick => PreviewSurfaceMode.SingleBrick,
            AssetKind.Item when useItemWorldModel => PreviewSurfaceMode.ItemWorldModel,
            AssetKind.Item => PreviewSurfaceMode.ItemViewModel,
            AssetKind.Paint => PreviewSurfaceMode.PaintSwatch,
            AssetKind.Body => PreviewSurfaceMode.BodySprite,
            AssetKind.Material => PreviewSurfaceMode.MaterialPreview,
            AssetKind.Blueprint => PreviewSurfaceMode.BlueprintVoxel,
            AssetKind.Recipe => PreviewSurfaceMode.RecipeFlow,
            AssetKind.SlotGroup => PreviewSurfaceMode.SlotGroupLayout,
            _ => PreviewSurfaceMode.DataDocument,
        };
    }

    /// <inheritdoc/>
    public void ReportLiveGlInfo(string vendor, string renderer, string version, bool isOpenGlEs)
    {
        var profile = isOpenGlEs ? "300 es" : "330 core";
        var shortRenderer = string.IsNullOrWhiteSpace(renderer) ? "OpenGL" : renderer.Trim();
        if (shortRenderer.Length > 26)
        {
            shortRenderer = shortRenderer.Substring(0, 26).TrimEnd() + "…";
        }

        _backendLabel = $"GL · {profile} ({shortRenderer})";
        _vendorRendererLabel = $"{vendor} / {renderer} ({version})";
        BackendInfoChanged?.Invoke();
    }

    /// <inheritdoc/>
    public void ReportGlFallback(string reason)
    {
        _backendLabel = "Software Fallback (2D)";
        _vendorRendererLabel = $"Fallback preview active ({reason})";
        BackendInfoChanged?.Invoke();
    }

    /// <summary>
    ///     Reloads the shared albedo texture array and mesh databases when <paramref name="workspace"/> changes.
    /// </summary>
    public void BindWorkspace(ToolsWorkspace.WorkspaceIndex? workspace, GlViewport? viewport = null)
    {
        if (workspace == null)
        {
            _workspace = null;
            _assetRoots = [];
            _standaloneTextureCache.Clear();
            _albedoArray = null;
            _albedoUploadedViewport = null;
            _brickDb = null;
            _paintDb = null;
            _meshDb = null;
            _brickBuilder = null;
            _regionBuilder = null;
            _lastSyncedDocumentKey = null;
            _lastSyncedSurfaceMode = null;
            _lastMaterialShape = null;
            _hasResolvedDefaultBodyRef = false;
            _cachedDefaultBodyRef = null;
            return;
        }

        if (ReferenceEquals(_workspace, workspace))
        {
            if (viewport != null && _albedoArray != null && !IsViewportAlbedoCurrent(viewport))
            {
                viewport.SetAlbedoArray(_albedoArray);
                _albedoUploadedViewport = new WeakReference<GlViewport>(viewport);
            }

            return;
        }

        _workspace = workspace;
        _standaloneTextureCache.Clear();
        _albedoUploadedViewport = null;
        _lastSyncedDocumentKey = null;
        _lastSyncedSurfaceMode = null;
        _lastMaterialShape = null;
        _hasResolvedDefaultBodyRef = false;
        _cachedDefaultBodyRef = null;

        var roots = new List<string>(workspace.Roots.Count);
        for (var i = 0; i < workspace.Roots.Count; i++)
        {
            var r = workspace.Roots[i];
            if (r.IsEnabled && Directory.Exists(r.AssetsDirectory))
            {
                roots.Add(r.AssetsDirectory);
            }
        }

        _assetRoots = roots;
        var albedoResult = PbrAlbedoArrayLoader.LoadFromAssetRoots(roots);
        _albedoArray = albedoResult.Success ? albedoResult.Value : PbrAlbedoArrayLoader.CreateMissingOnly();
        _brickDb = StudioPreviewBrickDatabase.FromWorkspace(workspace);
        _paintDb = StudioPreviewPaintDatabase.FromWorkspace(workspace);
        _meshDb = new StudioPreviewMeshDatabase(roots);
        _brickBuilder = new BrickPreviewMeshBuilder(_brickDb, _brickDb, _albedoArray, _meshDb);
        _regionBuilder = new RegionMeshBuilder(_brickDb, _albedoArray, _meshDb, _paintDb);

        if (viewport != null && _albedoArray != null)
        {
            viewport.SetAlbedoArray(_albedoArray);
            _albedoUploadedViewport = new WeakReference<GlViewport>(viewport);
        }
    }

    /// <summary>Gets the active block albedo texture array loaded for the bound workspace.</summary>
    public AlbedoTextureArrayData? ActiveAlbedoArray => _albedoArray;

    /// <summary>Gets the most recently synchronized material preview geometry primitive.</summary>
    public MaterialPreviewShape ActiveMaterialShape => _lastMaterialShape ?? MaterialPreviewShape.Sphere;

    /// <summary>Resolves and caches the deterministic first standing Body sprite with a valid image on disk.</summary>
    public BodyReferenceSpriteInfo? ResolveDefaultBodyReference(IReadOnlyList<AssetDocumentViewModel>? fallbackDocuments = null)
    {
        if (_hasResolvedDefaultBodyRef)
        {
            return _cachedDefaultBodyRef;
        }

        _cachedDefaultBodyRef = WorkspaceModelReferenceResolver.ResolveDeterministicBodySprite(
            _workspace,
            fallbackDocuments,
            ResolveTextureAbsolutePath);
        _hasResolvedDefaultBodyRef = true;
        return _cachedDefaultBodyRef;
    }

    /// <summary>Resolves the active ViewModel or WorldModel 3D mesh, standalone texture, or placeable brick visual.</summary>
    public ResolvedItemModelVisual ResolveItemModelVisual(
        AssetDocumentViewModel doc,
        bool isWorldModel,
        BrickShape preferredBrickShape)
    {
        ArgumentNullException.ThrowIfNull(doc);

        string? meshId = isWorldModel
            ? doc.ItemWorldModelMeshField?.Value
            : doc.ItemViewModelMeshField?.Value;
        string? materialId = isWorldModel
            ? (!string.IsNullOrWhiteSpace(doc.ItemWorldModelMaterialField?.Value)
                ? doc.ItemWorldModelMaterialField.Value
                : doc.ItemViewModelMaterialField?.Value)
            : doc.ItemViewModelMaterialField?.Value;

        if (!string.IsNullOrWhiteSpace(meshId) && _meshDb != null)
        {
            Result<VoxelMesh> meshRes = _meshDb.Get(meshId);
            if (meshRes.Success)
            {
                if (TryResolveMaterialTexture(
                    materialId,
                    out StandaloneTextureData standaloneTexture,
                    out BrickAlpha alphaMode,
                    out float alphaCutoff))
                {
                    bool doubleSided = alphaMode != BrickAlpha.Opaque;
                    return new ResolvedItemModelVisual(
                        meshRes.Value,
                        PreviewTextureMode.StandaloneTexture,
                        alphaMode,
                        alphaCutoff,
                        doubleSided,
                        standaloneTexture,
                        IsPlaceableBrickAny: false,
                        IsPlaceholder: false,
                        SourceLabel: $"{meshId} · {materialId}",
                        AccentHex: "#60A5FA");
                }

                return new ResolvedItemModelVisual(
                    meshRes.Value,
                    PreviewTextureMode.BlockAlbedoArray,
                    BrickAlpha.Opaque,
                    0.5f,
                    DoubleSided: false,
                    StandaloneTexture: null,
                    IsPlaceableBrickAny: false,
                    IsPlaceholder: false,
                    SourceLabel: string.IsNullOrWhiteSpace(materialId) ? meshId : $"{meshId} · {materialId} (unresolved)",
                    AccentHex: "#60A5FA");
            }
        }

        string? placeableType = doc.ItemPlaceableTypeField?.Value;
        string? placeableId = doc.ItemPlaceableIdField?.Value;
        if (string.Equals(placeableType, "blueprint", StringComparison.OrdinalIgnoreCase)
            && TryBuildBlueprintPreviewMesh(placeableId, out VoxelMesh blueprintMesh, out BrickAlpha blueprintAlpha, out string blueprintLabel))
        {
            bool doubleSided = blueprintAlpha != BrickAlpha.Opaque;
            return new ResolvedItemModelVisual(
                blueprintMesh,
                PreviewTextureMode.BlockAlbedoArray,
                blueprintAlpha,
                0.5f,
                doubleSided,
                StandaloneTexture: null,
                IsPlaceableBrickAny: false,
                IsPlaceholder: false,
                SourceLabel: $"{blueprintLabel} (blueprint)",
                AccentHex: "#38BDF8");
        }

        if (string.Equals(placeableType, "brick", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(placeableId)
            && _brickDb != null
            && _brickBuilder != null)
        {
            Result<Brick> brickRes = _brickDb.Get(placeableId);
            if (brickRes.Success)
            {
                Brick brick = brickRes.Value;
                bool isAny = brick.Shape == BrickShape.Any;
                BrickShape shape = isAny
                    ? (preferredBrickShape == BrickShape.Any ? BrickShape.Block : preferredBrickShape)
                    : brick.Shape;
                Result<VoxelMesh> built = _brickBuilder.Build(brick, shape, GlViewport.ToVoxelOrientation(BrickOrientation.North));
                if (built.Success)
                {
                    bool doubleSided = brick.Alpha != BrickAlpha.Opaque;
                    var accent = string.Format(
                        CultureInfo.InvariantCulture,
                        "#{0:X2}{1:X2}{2:X2}",
                        (byte)Math.Clamp(brick.Color.X * 255f, 0f, 255f),
                        (byte)Math.Clamp(brick.Color.Y * 255f, 0f, 255f),
                        (byte)Math.Clamp(brick.Color.Z * 255f, 0f, 255f));
                    return new ResolvedItemModelVisual(
                        built.Value,
                        PreviewTextureMode.BlockAlbedoArray,
                        brick.Alpha,
                        0.5f,
                        doubleSided,
                        StandaloneTexture: null,
                        IsPlaceableBrickAny: isAny,
                        IsPlaceholder: false,
                        SourceLabel: $"{brick.ID} ({shape})",
                        AccentHex: accent);
                }
            }
        }

        VoxelMesh placeholder = PreviewGeometryFactory.CreateTexturedCube(1f);
        return new ResolvedItemModelVisual(
            placeholder,
            PreviewTextureMode.VertexColorOnly,
            BrickAlpha.Opaque,
            0.5f,
            DoubleSided: false,
            StandaloneTexture: null,
            IsPlaceableBrickAny: false,
            IsPlaceholder: true,
            SourceLabel: "Fallback Cube (no mesh or placeable set)",
            AccentHex: "#64748B");
    }

    /// <summary>
    ///     Resolves a workspace-relative texture path (such as <c>items/drill_uv.png</c>) to an absolute file path on disk.
    /// </summary>
    public string? ResolveTextureAbsolutePath(string? relativeTexturePath)
    {
        if (string.IsNullOrWhiteSpace(relativeTexturePath))
        {
            return null;
        }

        string normalized = relativeTexturePath.Trim().TrimStart('/', '\\').Replace('\\', '/');
        if (normalized.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["textures/".Length..];
        }

        if (_workspace != null)
        {
            Result<ToolsWorkspace.IndexedResource> exact = _workspace.GetResource(ToolsWorkspace.ResourceKind.Texture, normalized);
            if (exact.Success && File.Exists(exact.Value.FilePath))
            {
                return exact.Value.FilePath;
            }

            Result<ToolsWorkspace.IndexedResource> ci = _workspace.FindResourceCaseInsensitive(ToolsWorkspace.ResourceKind.Texture, normalized);
            if (ci.Success && File.Exists(ci.Value.FilePath))
            {
                return ci.Value.FilePath;
            }
        }

        return StandaloneTextureLoader.ResolveTexturePath(_assetRoots, normalized);
    }

    /// <summary>
    ///     Resolves a block albedo texture layer name (such as <c>panel.0</c> or <c>caution_panel</c>) to an absolute file path on disk.
    /// </summary>
    public string? ResolveBlockAlbedoLayerPath(string? layerName, bool connected = false)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return null;
        }

        string normalized = NormalizeBlockAlbedoLayerName(layerName);
        if (normalized.Length == 0)
        {
            return null;
        }

        List<string> candidates = BuildAlbedoLayerCandidates(normalized, connected);

        if (_workspace != null)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                if (_workspace.WinningBlockAlbedoLayers.TryGetValue(candidate, out ToolsWorkspace.IndexedResource exact)
                    && File.Exists(exact.FilePath))
                {
                    return exact.FilePath;
                }

                Result<ToolsWorkspace.IndexedResource> ci = _workspace.FindBlockAlbedoCaseInsensitive(candidate);
                if (ci.Success && File.Exists(ci.Value.FilePath))
                {
                    return ci.Value.FilePath;
                }
            }
        }

        return ResolveBlockAlbedoFromRoots(candidates);
    }

    /// <summary>
    ///     Pushes the active document's 3D scene mesh into <paramref name="viewport"/> and returns true when GL can display it.
    /// </summary>
    public bool TrySyncViewportScene(
        GlViewport viewport,
        AssetDocumentViewModel? document,
        BrickOrientation orientation,
        bool isShapeGridMode,
        bool useWorldModelPreset,
        bool isConnectedShowcaseMode = false,
        MaterialPreviewShape materialShape = MaterialPreviewShape.Sphere)
    {
        if (document == null)
        {
            _lastSyncedDocumentKey = null;
            _lastSyncedSurfaceMode = null;
            _lastMaterialShape = null;
            return false;
        }

        if (_albedoArray != null && !IsViewportAlbedoCurrent(viewport))
        {
            viewport.SetAlbedoArray(_albedoArray);
            _albedoUploadedViewport = new WeakReference<GlViewport>(viewport);
        }

        string documentKey = BuildDocumentSyncKey(document);
        string? shapeName = document.Kind == AssetKind.Brick
            ? (isShapeGridMode ? "Any" : document.BrickShapeField?.Value)
            : null;
        PreviewSurfaceMode targetSurfaceMode = ResolveSurfaceMode(
            document.Kind,
            shapeName,
            useWorldModelPreset,
            isConnectedShowcaseMode);
        bool preserveCamera = string.Equals(_lastSyncedDocumentKey, documentKey, StringComparison.Ordinal)
            && _lastSyncedSurfaceMode == targetSurfaceMode;

        bool synced = document.Kind switch
        {
            AssetKind.Brick => SyncBrickPreview(viewport, document, orientation, isShapeGridMode, isConnectedShowcaseMode, preserveCamera),
            AssetKind.Item => SyncItemPreview(viewport, document, useWorldModelPreset, preserveCamera),
            AssetKind.Material => SyncMaterialPreview(viewport, document, materialShape, preserveCamera),
            AssetKind.Blueprint => SyncBlueprintPreview(viewport, document, preserveCamera),
            _ => false,
        };

        if (synced)
        {
            _lastSyncedDocumentKey = documentKey;
            _lastSyncedSurfaceMode = targetSurfaceMode;
            _lastMaterialShape = document.Kind == AssetKind.Material ? materialShape : null;
        }
        else
        {
            _lastSyncedDocumentKey = null;
            _lastSyncedSurfaceMode = null;
            _lastMaterialShape = null;
        }

        return synced;
    }

    private bool IsViewportAlbedoCurrent(GlViewport viewport) =>
        _albedoUploadedViewport != null
        && _albedoUploadedViewport.TryGetTarget(out GlViewport? uploaded)
        && ReferenceEquals(uploaded, viewport);

    private static string BuildDocumentSyncKey(AssetDocumentViewModel doc) =>
        $"{doc.Kind}|{doc.AbsoluteFilePath ?? doc.RelativeFile}|{doc.TableIndex}|{doc.EntryIndex}|{doc.Id}";

    private bool SyncBrickPreview(
        GlViewport viewport,
        AssetDocumentViewModel doc,
        BrickOrientation orientation,
        bool isShapeGridMode,
        bool isConnectedShowcaseMode,
        bool preserveCamera)
    {
        if (_brickDb == null || _albedoArray == null || _meshDb == null)
        {
            return false;
        }

        var brick = BuildLiveEditedBrick(doc, forceConnected: isConnectedShowcaseMode);
        _brickDb.RegisterOrUpdate(brick);

        // Rebuild mesh builders after updating the live brick so VoxelSurfaceCache reflects edited color/texture/connected state.
        _brickBuilder = new BrickPreviewMeshBuilder(_brickDb, _brickDb, _albedoArray, _meshDb);
        _regionBuilder = new RegionMeshBuilder(_brickDb, _albedoArray, _meshDb, _paintDb);

        if (isConnectedShowcaseMode)
        {
            ushort brickDataId = _brickDb.Id(brick.ID);
            return viewport.SetConnectedBrickShowcase(_regionBuilder, brick, orientation, brickDataId, preserveCamera).Success;
        }

        if (isShapeGridMode || brick.Shape == BrickShape.Any)
        {
            return viewport.SetBrickShapeGrid(_brickBuilder, brick, orientation, preserveCamera).Success;
        }

        var targetShape = brick.Shape;
        return viewport.SetSingleBrick(_brickBuilder, brick, targetShape, orientation, preserveCamera).Success;
    }

    private bool SyncItemPreview(
        GlViewport viewport,
        AssetDocumentViewModel doc,
        bool useWorldModelPreset,
        bool preserveCamera)
    {
        if (_meshDb == null || _brickBuilder == null || _brickDb == null)
        {
            return false;
        }

        var meshId = useWorldModelPreset
            ? doc.ItemWorldModelMeshField?.Value
            : doc.ItemViewModelMeshField?.Value;
        if (!string.IsNullOrWhiteSpace(meshId))
        {
            var meshRes = _meshDb.Get(meshId);
            if (meshRes.Success)
            {
                string? materialId = useWorldModelPreset
                    ? (!string.IsNullOrWhiteSpace(doc.ItemWorldModelMaterialField?.Value)
                        ? doc.ItemWorldModelMaterialField.Value
                        : doc.ItemViewModelMaterialField?.Value)
                    : doc.ItemViewModelMaterialField?.Value;

                if (TryResolveMaterialTexture(
                    materialId,
                    out StandaloneTextureData standaloneTexture,
                    out BrickAlpha alphaMode,
                    out float alphaCutoff))
                {
                    bool doubleSided = alphaMode != BrickAlpha.Opaque;
                    viewport.SetItemPreview(
                        new PreviewSceneMesh(
                            meshRes.Value,
                            Matrix4x4.Identity,
                            alphaMode,
                            alphaCutoff,
                            doubleSided,
                            useStandaloneTexture: true),
                        useWorldModelPreset,
                        standaloneTexture,
                        preserveCamera);
                    return true;
                }

                viewport.SetItemPreview(
                    new PreviewSceneMesh(meshRes.Value, Matrix4x4.Identity),
                    useWorldModelPreset,
                    standaloneTexture: null,
                    preserveCamera);
                return true;
            }
        }

        var placeableType = doc.ItemPlaceableTypeField?.Value;
        var placeableId = doc.ItemPlaceableIdField?.Value;
        if (string.Equals(placeableType, "blueprint", StringComparison.OrdinalIgnoreCase)
            && TryBuildBlueprintPreviewMesh(placeableId, out VoxelMesh blueprintMesh, out BrickAlpha blueprintAlpha, out _))
        {
            bool doubleSided = blueprintAlpha != BrickAlpha.Opaque;
            viewport.SetItemPreview(
                new PreviewSceneMesh(blueprintMesh, Matrix4x4.Identity, blueprintAlpha, 0.5f, doubleSided),
                useWorldModelPreset,
                standaloneTexture: null,
                preserveCamera);
            return true;
        }

        if (string.Equals(placeableType, "brick", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(placeableId))
        {
            var brickRes = _brickDb.Get(placeableId);
            if (brickRes.Success)
            {
                var shape = brickRes.Value.Shape == BrickShape.Any ? BrickShape.Block : brickRes.Value.Shape;
                var built = _brickBuilder.Build(brickRes.Value, shape, GlViewport.ToVoxelOrientation(BrickOrientation.North));
                if (built.Success)
                {
                    bool doubleSided = brickRes.Value.Alpha != BrickAlpha.Opaque;
                    viewport.SetItemPreview(
                        new PreviewSceneMesh(built.Value, Matrix4x4.Identity, brickRes.Value.Alpha, 0.5f, doubleSided),
                        useWorldModelPreset,
                        standaloneTexture: null,
                        preserveCamera);
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryBuildBlueprintPreviewMesh(
        string? blueprintId,
        out VoxelMesh mesh,
        out BrickAlpha alphaMode,
        out string resolvedLabel)
    {
        mesh = default;
        alphaMode = BrickAlpha.Opaque;
        resolvedLabel = string.Empty;

        if (_workspace == null || _regionBuilder == null || _brickDb == null || string.IsNullOrWhiteSpace(blueprintId))
        {
            return false;
        }

        string trimmed = blueprintId.Trim().Replace('\\', '/');
        string? fullPath = null;
        string canonicalId = trimmed;
        Result<string> exact = _workspace.GetBlueprintFile(trimmed);
        if (exact.Success)
        {
            fullPath = exact.Value;
        }
        else
        {
            Result<string> ci = _workspace.FindBlueprintIdCaseInsensitive(trimmed);
            if (ci.Success)
            {
                canonicalId = ci.Value;
                Result<string> resolved = _workspace.GetBlueprintFile(canonicalId);
                if (resolved.Success)
                {
                    fullPath = resolved.Value;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            var bytes = File.ReadAllBytes(fullPath);
            var data = BlueprintData.Deserialize(bytes);
            var encoded = data.ToRegion();
            var decoded = VoxelCodec.Decode(in encoded, _brickDb, _paintDb);
            Result<VoxelMesh> built = _regionBuilder.Build(in decoded);
            if (!built.Success)
            {
                return false;
            }

            mesh = RegionMeshBuilder.NormalizeToUnitCube(in built.Value);
            alphaMode = RegionMeshBuilder.ResolveAlphaMode(in decoded, _brickDb);
            resolvedLabel = canonicalId;
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed building blueprint placeable preview '{fullPath}': {ex.Message}");
            return false;
        }
    }

    private bool SyncMaterialPreview(
        GlViewport viewport,
        AssetDocumentViewModel doc,
        MaterialPreviewShape materialShape,
        bool preserveCamera)
    {
        string? firstTexture = ExtractFirstTextureEntry(doc.MaterialTexturesField?.Value);
        BrickAlpha alphaMode = Enum.TryParse(doc.MaterialAlphaModeField?.Value, ignoreCase: true, out BrickAlpha parsedAlpha)
            ? parsedAlpha
            : BrickAlpha.Opaque;
        float alphaCutoff = float.TryParse(
            doc.MaterialAlphaCutoffField?.Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float parsedCutoff)
            ? Math.Clamp(parsedCutoff, 0f, 1f)
            : 0.5f;

        StandaloneTextureData? textureData = null;
        string? resolvedPath = ResolveTextureAbsolutePath(firstTexture);
        if (resolvedPath != null && TryLoadStandaloneTextureCached(resolvedPath, out StandaloneTextureData loaded))
        {
            textureData = loaded;
        }

        viewport.SetMaterialPreview(textureData, alphaMode, alphaCutoff, preserveCamera, materialShape);
        return true;
    }

    private bool SyncBlueprintPreview(GlViewport viewport, AssetDocumentViewModel doc, bool preserveCamera)
    {
        if (_regionBuilder == null || _brickDb == null || string.IsNullOrWhiteSpace(doc.AbsoluteFilePath) || !File.Exists(doc.AbsoluteFilePath))
        {
            return false;
        }

        try
        {
            var bytes = File.ReadAllBytes(doc.AbsoluteFilePath);
            var data = BlueprintData.Deserialize(bytes);
            var encoded = data.ToRegion();
            var decoded = VoxelCodec.Decode(in encoded, _brickDb, _paintDb);
            return viewport.SetBlueprintPreview(_regionBuilder, in decoded, preserveCamera).Success;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed building blueprint 3D preview '{doc.AbsoluteFilePath}': {ex.Message}");
            return false;
        }
    }

    private bool TryResolveMaterialTexture(
        string? materialId,
        out StandaloneTextureData textureData,
        out BrickAlpha alphaMode,
        out float alphaCutoff)
    {
        textureData = default;
        alphaMode = BrickAlpha.Opaque;
        alphaCutoff = 0.5f;

        if (_workspace == null || string.IsNullOrWhiteSpace(materialId))
        {
            return false;
        }

        string normalized = materialId.Trim().Replace('\\', '/');
        if (normalized.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^".toml".Length];
        }

        ToolsWorkspace.IndexedAsset? found = null;
        Result<ToolsWorkspace.IndexedAsset> exact = _workspace.GetAsset(ToolsWorkspace.AssetKind.Material, normalized);
        if (exact.Success)
        {
            found = exact.Value;
        }
        else
        {
            IReadOnlyList<ToolsWorkspace.IndexedAsset> materials = _workspace.GetWinningAssetsByKind(ToolsWorkspace.AssetKind.Material);
            for (var i = 0; i < materials.Count; i++)
            {
                if (string.Equals(materials[i].Id, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    found = materials[i];
                    break;
                }
            }
        }

        if (!found.HasValue || found.Value.AuthoritativeDefinition is not MaterialDefinition matDef)
        {
            return false;
        }

        alphaMode = ConvertAlphaMode(matDef.AlphaMode);
        alphaCutoff = Math.Clamp(matDef.AlphaCutoff, 0f, 1f);
        if (matDef.Textures == null || matDef.Textures.Length == 0 || string.IsNullOrWhiteSpace(matDef.Textures[0]))
        {
            return false;
        }

        string? texturePath = ResolveTextureAbsolutePath(matDef.Textures[0]);
        if (texturePath == null)
        {
            return false;
        }

        return TryLoadStandaloneTextureCached(texturePath, out textureData);
    }

    private bool TryLoadStandaloneTextureCached(string absolutePath, out StandaloneTextureData data)
    {
        data = default;
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            return false;
        }

        DateTime lastWriteUtc = File.GetLastWriteTimeUtc(absolutePath);
        if (_standaloneTextureCache.TryGetValue(absolutePath, out CachedStandaloneTexture cached)
            && cached.LastWriteUtc == lastWriteUtc
            && cached.Data.IsValid)
        {
            data = cached.Data;
            return true;
        }

        Result<StandaloneTextureData> loaded = StandaloneTextureLoader.LoadFromFile(absolutePath);
        if (!loaded.Success)
        {
            Trace.TraceWarning($"Failed loading standalone preview texture '{absolutePath}': {loaded.Message}");
            return false;
        }

        _standaloneTextureCache[absolutePath] = new CachedStandaloneTexture(lastWriteUtc, loaded.Value);
        data = loaded.Value;
        return true;
    }

    private static BrickAlpha ConvertAlphaMode(AlphaMode alphaMode)
    {
        return alphaMode switch
        {
            AlphaMode.Masked => BrickAlpha.Masked,
            AlphaMode.Blend => BrickAlpha.Blend,
            _ => BrickAlpha.Opaque,
        };
    }

    private static string? ExtractFirstTextureEntry(string? rawList)
    {
        if (string.IsNullOrWhiteSpace(rawList))
        {
            return null;
        }

        string[] parts = rawList.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[0].Trim('"', '\'') : null;
    }

    private static Brick BuildLiveEditedBrick(AssetDocumentViewModel doc, bool forceConnected = false)
    {
        var shape = Enum.TryParse(doc.BrickShapeField?.Value, ignoreCase: true, out BrickShape parsedShape)
            ? parsedShape
            : BrickShape.Block;
        var alpha = Enum.TryParse(doc.BrickAlphaField?.Value, ignoreCase: true, out BrickAlpha parsedAlpha)
            ? parsedAlpha
            : BrickAlpha.Opaque;
        var castShadows = !bool.TryParse(doc.BrickCastShadowsField?.Value, out var cs) || cs;
        var passable = bool.TryParse(doc.BrickPassableField?.Value, out var p) && p;
        var connected = forceConnected || (bool.TryParse(doc.BrickConnectedField?.Value, out var conn) && conn);
        var mesh = string.IsNullOrWhiteSpace(doc.BrickMeshField?.Value) ? null : doc.BrickMeshField.Value.Trim();

        var color = Vector3.One;
        if (!string.IsNullOrWhiteSpace(doc.BrickColorField?.Value)
            && HexColor.TryParseRgb(doc.BrickColorField.Value, out var parsedColor))
        {
            color = parsedColor;
        }

        var textures = new BrickTextures
        {
            Connected = connected,
            Default = SplitNullable(doc.BrickTexDefaultField?.Value),
            Top = SplitNullable(doc.BrickTexTopField?.Value),
            Bottom = SplitNullable(doc.BrickTexBottomField?.Value),
            Front = SplitNullable(doc.BrickTexFrontField?.Value),
            Back = SplitNullable(doc.BrickTexBackField?.Value),
            Left = SplitNullable(doc.BrickTexLeftField?.Value),
            Right = SplitNullable(doc.BrickTexRightField?.Value),
        };

        return new Brick(doc.Id, alpha, passable, mesh, shape, textures, castShadows, color: color);
    }

    private static string?[]? SplitNullable(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return null;
        }

        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts : null;
    }

    private static string NormalizeBlockAlbedoLayerName(string layerName)
    {
        string normalized = layerName.Trim().Trim('"', '\'').TrimStart('/', '\\').Replace('\\', '/');
        if (normalized.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["textures/".Length..];
        }

        if (normalized.StartsWith("block/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["block/".Length..];
        }

        if (normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return normalized;
    }

    private static List<string> BuildAlbedoLayerCandidates(string normalized, bool connected)
    {
        List<string> candidates = [];
        bool endsWithZero = normalized.EndsWith(".0", StringComparison.Ordinal);

        if (connected && !endsWithZero)
        {
            candidates.Add($"{normalized}.0");
            candidates.Add(normalized);
        }
        else
        {
            candidates.Add(normalized);
            if (!endsWithZero)
            {
                candidates.Add($"{normalized}.0");
            }
            else if (normalized.Length > 2)
            {
                candidates.Add(normalized[..^2]);
            }
        }

        return candidates;
    }

    private string? ResolveBlockAlbedoFromRoots(List<string> candidates)
    {
        for (int r = _assetRoots.Count - 1; r >= 0; r--)
        {
            string root = _assetRoots[r];
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string blockDir = Path.Combine(root, "textures", "block");
            if (!Directory.Exists(blockDir))
            {
                continue;
            }

            for (var c = 0; c < candidates.Count; c++)
            {
                string pngPath = Path.Combine(blockDir, $"{candidates[c]}.png");
                if (File.Exists(pngPath))
                {
                    return pngPath;
                }

                string bmpPath = Path.Combine(blockDir, $"{candidates[c]}.bmp");
                if (File.Exists(bmpPath))
                {
                    return bmpPath;
                }
            }

            int bestCandidateIndex = int.MaxValue;
            bool bestIsExact = false;
            string? bestPath = null;
            try
            {
                foreach (string filePath in Directory.EnumerateFiles(blockDir, "*", SearchOption.AllDirectories))
                {
                    if (!PbrAlbedoArrayLoader.IsAlbedoTextureFile(filePath))
                    {
                        continue;
                    }

                    string stem = Path.GetFileNameWithoutExtension(filePath);
                    for (var c = 0; c < candidates.Count && c <= bestCandidateIndex; c++)
                    {
                        bool isExact = string.Equals(stem, candidates[c], StringComparison.Ordinal);
                        if (!isExact && !string.Equals(stem, candidates[c], StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (c < bestCandidateIndex || (isExact && !bestIsExact))
                        {
                            bestCandidateIndex = c;
                            bestIsExact = isExact;
                            bestPath = filePath;
                        }

                        break;
                    }

                    if (bestCandidateIndex == 0 && bestIsExact)
                    {
                        return bestPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed scanning block albedo directory '{blockDir}': {ex.Message}");
            }

            if (bestPath != null)
            {
                return bestPath;
            }
        }

        return null;
    }

    private readonly struct CachedStandaloneTexture(DateTime lastWriteUtc, StandaloneTextureData data)
    {
        public readonly DateTime LastWriteUtc = lastWriteUtc;
        public readonly StandaloneTextureData Data = data;
    }
}
