using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Meshing;
using WaywardBeyond.Voxels;
using ToolsWorkspace = WaywardBeyond.AssetTools.Workspace;
using Xunit;
using Xunit.Abstractions;

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>Verifies real OpenGL 3.3 Core software rendering under Xvfb and Mesa llvmpipe using shipped Wayward Beyond assets.</summary>
public sealed class GlSceneRendererTests(ITestOutputHelper output)
{
    private const int RENDER_SIZE = 512;
    private const string PREVIEW_OUT_DIRS_ENV = "WB_GL_PREVIEW_OUT_DIRS";

    private static readonly Vector4 _clearColor = new(0.085f, 0.095f, 0.115f, 1f);
    private readonly ITestOutputHelper _output = output;

    /// <summary>Verifies albedo layer discovery excludes PBR suffix maps and preserves natural sort order.</summary>
    [Fact]
    public void PbrAlbedoArrayLoader_MatchesGameNaturalSortAndExcludesAuxMaps()
    {
        string repoRoot = FindRepoRoot();
        string clientAssets = Path.Combine(repoRoot, "WaywardBeyond.Client", "assets");

        Result<AlbedoTextureArrayData> loaded = PbrAlbedoArrayLoader.LoadFromAssetRoots([clientAssets]);
        Assert.True(loaded.Success, loaded.Message);

        AlbedoTextureArrayData data = loaded.Value;
        Assert.Equal("missing", data.LayerNames[0]);
        Assert.True(data.LayerCount > 20);
        Assert.True(data.IndexOf("core") > 0);
        Assert.True(data.IndexOf("display_console") > 0);
        Assert.True(data.IndexOf("panel.2") > 0);
        Assert.True(data.IndexOf("panel.10") > 0);
        Assert.True(data.IndexOf("panel.2") < data.IndexOf("panel.10"));
        Assert.Equal(-1, data.IndexOf("core.e"));
        Assert.Equal(-1, data.IndexOf("core.t"));
        Assert.Equal(-1, data.IndexOf("does_not_exist"));

        Assert.True(PbrAlbedoArrayLoader.IsAlbedoTextureFile("core.png"));
        Assert.True(PbrAlbedoArrayLoader.IsAlbedoTextureFile("core.BMP"));
        Assert.False(PbrAlbedoArrayLoader.IsAlbedoTextureFile("core.n.png"));
        Assert.False(PbrAlbedoArrayLoader.IsAlbedoTextureFile("core.M.BMP"));
        Assert.False(PbrAlbedoArrayLoader.IsAlbedoTextureFile("core.E.PNG"));
    }

    /// <summary>Verifies camera orbit clamping, zoom bounds, presets, and bounding-box framing.</summary>
    [Fact]
    public void PreviewCamera_SupportsOrbitZoomResetPresetsAndBoundsFraming()
    {
        var camera = new PreviewCamera();
        float initialYaw = camera.YawDegrees;
        float initialDistance = camera.Distance;

        camera.Orbit(35f, 120f);
        Assert.Equal(85f, camera.PitchDegrees);

        camera.Zoom(3f);
        Assert.True(camera.Distance < initialDistance);

        camera.Reset();
        Assert.Equal(initialYaw, camera.YawDegrees);
        Assert.Equal(initialDistance, camera.Distance);

        camera.ApplyPreset(PreviewCameraPreset.ItemViewModel);
        float viewModelFov = camera.FovDegrees;
        camera.ApplyPreset(PreviewCameraPreset.ItemWorldModel);
        Assert.NotEqual(viewModelFov, camera.FovDegrees);

        camera.FrameBounds(new Vector3(-3f, -2f, -3f), new Vector3(5f, 4f, 5f));
        Assert.Equal(new Vector3(1f, 1f, 1f), camera.Target);
        Assert.True(camera.Distance > 5f);
    }

    /// <summary>Verifies desktop OpenGL 330 core and OpenGL ES 300 es shader preambles and orientation mapping.</summary>
    [Fact]
    public void PreviewShaderSource_AndOrientationMapping_MatchTargetProfiles()
    {
        Assert.StartsWith("#version 330 core\n", PreviewShaderSource.GetVertexShader(isOpenGlEs: false), StringComparison.Ordinal);
        Assert.StartsWith("#version 330 core\n", PreviewShaderSource.GetFragmentShader(isOpenGlEs: false), StringComparison.Ordinal);
        Assert.StartsWith("#version 300 es\n", PreviewShaderSource.GetVertexShader(isOpenGlEs: true), StringComparison.Ordinal);
        Assert.Contains("precision highp sampler2DArray;", PreviewShaderSource.GetFragmentShader(isOpenGlEs: true), StringComparison.Ordinal);

        Assert.Equal(Orientation.Identity, GlViewport.ToVoxelOrientation(BrickOrientation.North));
        Assert.Equal(new Orientation(0, 1, 0), GlViewport.ToVoxelOrientation(BrickOrientation.East));
        Assert.Equal(new Orientation(0, 2, 0), GlViewport.ToVoxelOrientation(BrickOrientation.South));
        Assert.Equal(new Orientation(0, 3, 0), GlViewport.ToVoxelOrientation(BrickOrientation.West));
    }

    /// <summary>Verifies uninitialized renderer operations return failed results instead of throwing.</summary>
    [Fact]
    public void SceneRenderer_ReturnsFailureWhenUninitialized()
    {
        using var renderer = new SceneRenderer();
        var camera = new PreviewCamera();

        Assert.False(renderer.IsInitialized);
        Assert.False(renderer.UploadAlbedoArray(PbrAlbedoArrayLoader.CreateMissingOnly()).Success);
        Assert.False(renderer.SetSceneMeshes([]).Success);
        Assert.False(renderer.RenderToRgba(64, 64, camera, _clearColor).Success);
    }

    /// <summary>Renders real shipped bricks, custom OBJ, missing checkerboard, Shape=Any grid, and blueprint region under software OpenGL.</summary>
    [Fact]
    public void SceneRenderer_RendersRealShippedAssetsAndMatchesDeterministicPixelHashes()
    {
        string repoRoot = FindRepoRoot();
        string clientAssets = Path.Combine(repoRoot, "WaywardBeyond.Client", "assets");
        string bricksAssets = Path.Combine(repoRoot, "WaywardBeyond.Bricks", "assets");

        Result<AlbedoTextureArrayData> albedoResult = PbrAlbedoArrayLoader.LoadFromAssetRoots([clientAssets]);
        Assert.True(albedoResult.Success, albedoResult.Message);
        AlbedoTextureArrayData albedo = albedoResult.Value;

        var bricks = SimpleBrickDatabase.LoadFromDirectory(Path.Combine(bricksAssets, "bricks"));
        var meshes = new ObjPreviewMeshDatabase(Path.Combine(clientAssets, "meshes"));
        var brickBuilder = new BrickPreviewMeshBuilder(bricks, bricks, albedo, meshes);
        var regionBuilder = new RegionMeshBuilder(bricks, albedo, meshes);

        using var glContext = HeadlessEglContext.Create();
        using var renderer = new SceneRenderer();

        Result<GlInfo> initResult = renderer.Initialize(glContext.GetProcAddress, isOpenGlEs: false);
        Assert.True(initResult.Success, initResult.Message);
        Assert.False(string.IsNullOrWhiteSpace(initResult.Value.Vendor));
        Assert.False(string.IsNullOrWhiteSpace(initResult.Value.Renderer));
        _output.WriteLine($"GL Vendor={initResult.Value.Vendor} | Renderer={initResult.Value.Renderer} | Version={initResult.Value.Version}");

        Result uploadResult = renderer.UploadAlbedoArray(albedo);
        Assert.True(uploadResult.Success, uploadResult.Message);

        //  1. Shipped brick: wb:core (Block)
        Brick coreBrick = bricks.Get("wb:core").Value;
        Result<VoxelMesh> coreMesh = brickBuilder.Build(coreBrick, BrickShape.Block, Orientation.Identity);
        Assert.True(coreMesh.Success, coreMesh.Message);

        var brickCamera = new PreviewCamera();
        brickCamera.ApplyPreset(PreviewCameraPreset.OrbitBrick);
        Assert.True(renderer.SetSceneMeshes([new PreviewSceneMesh(coreMesh.Value, Matrix4x4.Identity, coreBrick.Alpha)]).Success);

        Result<byte[]> coreFrame1 = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, brickCamera, _clearColor);
        Result<byte[]> coreFrame2 = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, brickCamera, _clearColor);
        Assert.True(coreFrame1.Success, coreFrame1.Message);
        Assert.True(coreFrame2.Success, coreFrame2.Message);
        Assert.Equal(HashBytes(coreFrame1.Value), HashBytes(coreFrame2.Value));
        SavePreviewPng("gl-01-brick-core.png", RENDER_SIZE, RENDER_SIZE, coreFrame1.Value);
        AssertRenderedSurface(coreFrame1.Value, minCoveredPixels: 20_000, minDistinctColors: 8);

        //  2. Shipped custom OBJ brick: wb:thruster (thruster.obj)
        Brick thrusterBrick = bricks.Get("wb:thruster").Value;
        Result<VoxelMesh> thrusterMesh = brickBuilder.Build(thrusterBrick, BrickShape.Custom, Orientation.Identity);
        Assert.True(thrusterMesh.Success, thrusterMesh.Message);

        Assert.True(renderer.SetSceneMeshes([new PreviewSceneMesh(thrusterMesh.Value, Matrix4x4.Identity, thrusterBrick.Alpha)]).Success);
        Result<byte[]> thrusterFrame1 = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, brickCamera, _clearColor);
        Result<byte[]> thrusterFrame2 = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, brickCamera, _clearColor);
        Assert.True(thrusterFrame1.Success, thrusterFrame1.Message);
        Assert.True(thrusterFrame2.Success, thrusterFrame2.Message);
        Assert.Equal(HashBytes(thrusterFrame1.Value), HashBytes(thrusterFrame2.Value));
        SavePreviewPng("gl-02-brick-thruster.png", RENDER_SIZE, RENDER_SIZE, thrusterFrame1.Value);
        AssertRenderedSurface(thrusterFrame1.Value, minCoveredPixels: 10_000, minDistinctColors: 4);

        //  3. Missing-texture brick (verifies magenta/black layer-0 checkerboard placeholder)
        Brick missingBrick = bricks.RegisterSynthetic(
            "test:missing_texture",
            BrickShape.Block,
            new BrickTextures { Default = ["nonexistent_missing_layer"] }
        );
        Result<VoxelMesh> missingMesh = brickBuilder.Build(missingBrick, BrickShape.Block, Orientation.Identity);
        Assert.True(missingMesh.Success, missingMesh.Message);

        Assert.True(renderer.SetSceneMeshes([new PreviewSceneMesh(missingMesh.Value, Matrix4x4.Identity, BrickAlpha.Opaque)]).Success);
        Result<byte[]> missingFrame = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, brickCamera, _clearColor);
        Assert.True(missingFrame.Success, missingFrame.Message);
        SavePreviewPng("gl-03-missing-magenta.png", RENDER_SIZE, RENDER_SIZE, missingFrame.Value);
        (int magentaPixels, int blackPixels) = CountMagentaAndBlackCheckerPixels(missingFrame.Value);
        Assert.True(magentaPixels > 5_000, $"Expected > 5000 magenta checkerboard pixels, got {magentaPixels}.");
        Assert.True(blackPixels > 5_000, $"Expected > 5000 black checkerboard pixels, got {blackPixels}.");

        //  4. BrickShape.Any six-shape grid (wb:display_console)
        Brick consoleBrick = bricks.Get("wb:display_console").Value;
        Orientation sharedOrientation = GlViewport.ToVoxelOrientation(BrickOrientation.North);
        Result<IReadOnlyList<PreviewSceneMesh>> gridResult = PreviewSceneMesh.BuildShapeAnyGrid(brickBuilder, consoleBrick, sharedOrientation);
        Assert.True(gridResult.Success, gridResult.Message);
        Assert.Equal(6, gridResult.Value.Count);

        var gridCamera = new PreviewCamera();
        gridCamera.ApplyPreset(PreviewCameraPreset.BrickShapeGrid);
        (Vector3 gridMin, Vector3 gridMax) = PreviewSceneMesh.ComputeBounds(gridResult.Value);
        gridCamera.FrameBounds(in gridMin, in gridMax, paddingFactor: 1.25f);

        Assert.True(renderer.SetSceneMeshes(gridResult.Value).Success);
        Result<byte[]> gridFrame = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, gridCamera, _clearColor);
        Assert.True(gridFrame.Success, gridFrame.Message);
        SavePreviewPng("gl-04-shape-any-grid.png", RENDER_SIZE, RENDER_SIZE, gridFrame.Value);
        AssertRenderedSurface(gridFrame.Value, minCoveredPixels: 18_000, minDistinctColors: 8);
        AssertGridCellsPopulated(gridFrame.Value, RENDER_SIZE, RENDER_SIZE);

        //  5. Multi-brick blueprint voxel region via RegionMeshBuilder
        VoxelRegionData region = BuildSampleBlueprintRegion(bricks);
        Result<VoxelMesh> regionMesh = regionBuilder.Build(in region);
        Assert.True(regionMesh.Success, regionMesh.Message);

        PreviewSceneMesh[] regionScene = [new PreviewSceneMesh(regionMesh.Value, Matrix4x4.Identity, BrickAlpha.Opaque)];
        var blueprintCamera = new PreviewCamera();
        blueprintCamera.ApplyPreset(PreviewCameraPreset.Blueprint);
        (Vector3 bpMin, Vector3 bpMax) = PreviewSceneMesh.ComputeBounds(regionScene);
        blueprintCamera.FrameBounds(in bpMin, in bpMax, paddingFactor: 1.3f);

        Assert.True(renderer.SetSceneMeshes(regionScene).Success);
        Result<byte[]> bpFrame1 = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, blueprintCamera, _clearColor);
        Result<byte[]> bpFrame2 = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, blueprintCamera, _clearColor);
        Assert.True(bpFrame1.Success, bpFrame1.Message);
        Assert.True(bpFrame2.Success, bpFrame2.Message);
        Assert.Equal(HashBytes(bpFrame1.Value), HashBytes(bpFrame2.Value));
        SavePreviewPng("gl-05-blueprint-region.png", RENDER_SIZE, RENDER_SIZE, bpFrame1.Value);
        AssertRenderedSurface(bpFrame1.Value, minCoveredPixels: 25_000, minDistinctColors: 12);

        //  6. Shipped item mesh with standalone texture: wb:drill (items/drill.obj + items/drill_uv.png)
        var objParser = new ObjParser();
        Mesh drillObj = objParser.Parse(new PathInfo(Path.Combine(clientAssets, "meshes", "items", "drill.obj")));
        var drillPreviewMesh = new VoxelMesh(drillObj.Triangles, drillObj.Vertices, drillObj.Colors, drillObj.Uv, drillObj.Normals);
        Result<StandaloneTextureData> drillTex = StandaloneTextureLoader.LoadFromAssetRoots([clientAssets], "items/drill_uv.png");
        Assert.True(drillTex.Success, drillTex.Message);
        Assert.True(renderer.UploadStandaloneTexture(drillTex.Value).Success);

        PreviewSceneMesh[] drillScene = [new PreviewSceneMesh(drillPreviewMesh, Matrix4x4.Identity, BrickAlpha.Masked, 0.5f, true, useStandaloneTexture: true)];
        var itemCamera = new PreviewCamera();
        itemCamera.ApplyPreset(PreviewCameraPreset.ItemViewModel);
        (Vector3 drillMin, Vector3 drillMax) = PreviewSceneMesh.ComputeBounds(drillScene);
        itemCamera.FrameBounds(in drillMin, in drillMax, paddingFactor: 1.35f);

        Assert.True(renderer.SetSceneMeshes(drillScene).Success);
        Result<byte[]> drillFrame = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, itemCamera, _clearColor);
        Assert.True(drillFrame.Success, drillFrame.Message);
        SavePreviewPng("gl-06-item-drill.png", RENDER_SIZE, RENDER_SIZE, drillFrame.Value);
        AssertRenderedSurface(drillFrame.Value, minCoveredPixels: 5_000, minDistinctColors: 8);
        (int drillMagenta, _) = CountMagentaAndBlackCheckerPixels(drillFrame.Value);
        Assert.Equal(0, drillMagenta);

        //  7. Material textured cube preview (bricks/glass texture via standalone 2D texture unit)
        Result<StandaloneTextureData> matTex = StandaloneTextureLoader.LoadFromAssetRoots([clientAssets], "block/glass.png");
        Assert.True(matTex.Success, matTex.Message);
        Assert.True(renderer.UploadStandaloneTexture(matTex.Value).Success);

        VoxelMesh cubeMesh = PreviewGeometryFactory.CreateTexturedCube();
        PreviewSceneMesh[] materialScene = [new PreviewSceneMesh(cubeMesh, Matrix4x4.Identity, BrickAlpha.Masked, 0.5f, true, useStandaloneTexture: true)];
        Assert.True(renderer.SetSceneMeshes(materialScene).Success);
        Result<byte[]> materialFrame = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, brickCamera, _clearColor);
        Assert.True(materialFrame.Success, materialFrame.Message);
        SavePreviewPng("gl-07-material-cube.png", RENDER_SIZE, RENDER_SIZE, materialFrame.Value);
        AssertRenderedSurface(materialFrame.Value, minCoveredPixels: 15_000, minDistinctColors: 6);
        (int matMagenta, _) = CountMagentaAndBlackCheckerPixels(materialFrame.Value);
        Assert.Equal(0, matMagenta);

        //  8. Connected Textures Brick Showcase (16-mask cluster for wb:panel)
        Brick panelBrick = bricks.Get("wb:panel").Value;
        Result<VoxelRegionData> connectedRegion = ConnectedBrickShowcaseBuilder.BuildRegion(panelBrick, bricks, Orientation.Identity);
        Assert.True(connectedRegion.Success, connectedRegion.Message);
        Result<VoxelMesh> connectedMesh = regionBuilder.Build(connectedRegion.Value);
        Assert.True(connectedMesh.Success, connectedMesh.Message);

        PreviewSceneMesh[] connectedScene = [new PreviewSceneMesh(connectedMesh.Value, Matrix4x4.Identity, panelBrick.Alpha)];
        var connectedCamera = new PreviewCamera();
        connectedCamera.ApplyPreset(PreviewCameraPreset.ConnectedShowcase);
        (Vector3 connMin, Vector3 connMax) = PreviewSceneMesh.ComputeBounds(connectedScene);
        connectedCamera.FrameBounds(in connMin, in connMax, paddingFactor: 1.25f);

        Assert.True(renderer.SetSceneMeshes(connectedScene).Success);
        Result<byte[]> connectedFrame = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, connectedCamera, _clearColor);
        Assert.True(connectedFrame.Success, connectedFrame.Message);
        SavePreviewPng("gl-08-connected-brick-showcase.png", RENDER_SIZE, RENDER_SIZE, connectedFrame.Value);
        AssertRenderedSurface(connectedFrame.Value, minCoveredPixels: 15_000, minDistinctColors: 8);
        (int connMagenta, _) = CountMagentaAndBlackCheckerPixels(connectedFrame.Value);
        Assert.Equal(0, connMagenta);

        //  9. Verify Deinitialize, HandleContextLost, and re-Initialize lifecycle recovery.
        renderer.Deinitialize();
        Assert.False(renderer.IsInitialized);
        Assert.Null(renderer.Info);

        Result<GlInfo> reinitAfterDeinit = renderer.Initialize(glContext.GetProcAddress, isOpenGlEs: false);
        Assert.True(reinitAfterDeinit.Success, reinitAfterDeinit.Message);
        Assert.True(renderer.IsInitialized);

        renderer.HandleContextLost();
        Assert.False(renderer.IsInitialized);
        Assert.Null(renderer.Info);

        Result<GlInfo> reinitAfterLoss = renderer.Initialize(glContext.GetProcAddress, isOpenGlEs: false);
        Assert.True(reinitAfterLoss.Success, reinitAfterLoss.Message);
        Assert.True(renderer.UploadAlbedoArray(albedo).Success);
        Assert.True(renderer.SetSceneMeshes([new PreviewSceneMesh(coreMesh.Value, Matrix4x4.Identity, coreBrick.Alpha)]).Success);
        Result<byte[]> recoveredFrame = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, brickCamera, _clearColor);
        Assert.True(recoveredFrame.Success, recoveredFrame.Message);
        Assert.Equal(HashBytes(coreFrame1.Value), HashBytes(recoveredFrame.Value));
    }

    /// <summary>Decodes real repo builtin/mainMenu.wbp via VoxelCodec + StudioPreviewPaintDatabase and renders under software OpenGL.</summary>
    [Fact]
    public void SceneRenderer_DecodesAndRendersRealMainMenuBlueprintWithBrickAndPaintPalette()
    {
        string repoRoot = FindRepoRoot();
        var loader = new ToolsWorkspace.WorkspaceLoader();
        Result<ToolsWorkspace.WorkspaceIndex> wsResult = loader.LoadRepo(repoRoot);
        Assert.True(wsResult.Success, wsResult.Message);
        ToolsWorkspace.WorkspaceIndex workspace = wsResult.Value;

        var assetRoots = workspace.Roots
            .Where(static r => r.IsEnabled && Directory.Exists(r.AssetsDirectory))
            .Select(static r => r.AssetsDirectory)
            .ToList();

        Result<AlbedoTextureArrayData> albedoResult = PbrAlbedoArrayLoader.LoadFromAssetRoots(assetRoots);
        Assert.True(albedoResult.Success, albedoResult.Message);
        AlbedoTextureArrayData albedo = albedoResult.Value;

        StudioPreviewBrickDatabase brickDb = StudioPreviewBrickDatabase.FromWorkspace(workspace);
        StudioPreviewPaintDatabase paintDb = StudioPreviewPaintDatabase.FromWorkspace(workspace);
        var meshes = new ObjPreviewMeshDatabase(Path.Combine(repoRoot, "WaywardBeyond.Client", "assets", "meshes"));
        var regionBuilder = new RegionMeshBuilder(brickDb, albedo, meshes, paintDb);

        string mainMenuPath = Path.Combine(repoRoot, "WaywardBeyond.Client", "assets", "blueprints", "builtin", "mainMenu.wbp");
        Assert.True(File.Exists(mainMenuPath), $"Missing mainMenu.wbp at '{mainMenuPath}'.");

        BlueprintData bpData = BlueprintData.Deserialize(File.ReadAllBytes(mainMenuPath));
        VoxelRegionData encoded = bpData.ToRegion();
        VoxelRegionData decoded = VoxelCodec.Decode(in encoded, brickDb, paintDb);
        Result<VoxelMesh> meshResult = regionBuilder.Build(in decoded);
        Assert.True(meshResult.Success, meshResult.Message);
        Assert.True(meshResult.Value.Vertices.Length > 0);
        Assert.True(meshResult.Value.Triangles.Length > 0);

        using var glContext = HeadlessEglContext.Create();
        using var renderer = new SceneRenderer();
        Assert.True(renderer.Initialize(glContext.GetProcAddress, isOpenGlEs: false).Success);
        Assert.True(renderer.UploadAlbedoArray(albedo).Success);

        PreviewSceneMesh[] scene =
        [
            new PreviewSceneMesh(meshResult.Value, Matrix4x4.Identity, BrickAlpha.Masked, 0.5f, doubleSided: false),
        ];
        var camera = new PreviewCamera();
        camera.ApplyPreset(PreviewCameraPreset.Blueprint);
        (Vector3 min, Vector3 max) = PreviewSceneMesh.ComputeBounds(scene);
        camera.FrameBounds(in min, in max, paddingFactor: 1.15f);

        Assert.True(renderer.SetSceneMeshes(scene).Success);
        Result<byte[]> frame1 = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, camera, _clearColor);
        Result<byte[]> frame2 = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, camera, _clearColor);
        Assert.True(frame1.Success, frame1.Message);
        Assert.True(frame2.Success, frame2.Message);
        Assert.Equal(HashBytes(frame1.Value), HashBytes(frame2.Value));

        SavePreviewPng("gl-09-blueprint-mainmenu.png", RENDER_SIZE, RENDER_SIZE, frame1.Value);
        AssertRenderedSurface(frame1.Value, minCoveredPixels: 12_000, minDistinctColors: 12);
        (int magentaPixels, _) = CountMagentaAndBlackCheckerPixels(frame1.Value);
        Assert.Equal(0, magentaPixels);
    }

    /// <summary>Decodes real repo builtin/wrecked_lifeboat.wbp via VoxelCodec + StudioPreviewPaintDatabase and renders under software OpenGL.</summary>
    [Fact]
    public void SceneRenderer_DecodesAndRendersRealWreckedLifeboatBlueprintWithBrickAndPaintPalette()
    {
        string repoRoot = FindRepoRoot();
        var loader = new ToolsWorkspace.WorkspaceLoader();
        Result<ToolsWorkspace.WorkspaceIndex> wsResult = loader.LoadRepo(repoRoot);
        Assert.True(wsResult.Success, wsResult.Message);
        ToolsWorkspace.WorkspaceIndex workspace = wsResult.Value;

        var assetRoots = workspace.Roots
            .Where(static r => r.IsEnabled && Directory.Exists(r.AssetsDirectory))
            .Select(static r => r.AssetsDirectory)
            .ToList();

        Result<AlbedoTextureArrayData> albedoResult = PbrAlbedoArrayLoader.LoadFromAssetRoots(assetRoots);
        Assert.True(albedoResult.Success, albedoResult.Message);
        AlbedoTextureArrayData albedo = albedoResult.Value;

        StudioPreviewBrickDatabase brickDb = StudioPreviewBrickDatabase.FromWorkspace(workspace);
        StudioPreviewPaintDatabase paintDb = StudioPreviewPaintDatabase.FromWorkspace(workspace);
        var meshes = new ObjPreviewMeshDatabase(Path.Combine(repoRoot, "WaywardBeyond.Client", "assets", "meshes"));
        var regionBuilder = new RegionMeshBuilder(brickDb, albedo, meshes, paintDb);

        string lifeboatPath = Path.Combine(repoRoot, "WaywardBeyond.Server", "assets", "blueprints", "builtin", "wrecked_lifeboat.wbp");
        Assert.True(File.Exists(lifeboatPath), $"Missing wrecked_lifeboat.wbp at '{lifeboatPath}'.");

        BlueprintData bpData = BlueprintData.Deserialize(File.ReadAllBytes(lifeboatPath));
        VoxelRegionData encoded = bpData.ToRegion();
        VoxelRegionData decoded = VoxelCodec.Decode(in encoded, brickDb, paintDb);
        Result<VoxelMesh> meshResult = regionBuilder.Build(in decoded);
        Assert.True(meshResult.Success, meshResult.Message);
        Assert.True(meshResult.Value.Vertices.Length > 0);
        Assert.True(meshResult.Value.Triangles.Length > 0);

        using var glContext = HeadlessEglContext.Create();
        using var renderer = new SceneRenderer();
        Assert.True(renderer.Initialize(glContext.GetProcAddress, isOpenGlEs: false).Success);
        Assert.True(renderer.UploadAlbedoArray(albedo).Success);

        PreviewSceneMesh[] scene =
        [
            new PreviewSceneMesh(meshResult.Value, Matrix4x4.Identity, BrickAlpha.Masked, 0.5f, doubleSided: false),
        ];
        var camera = new PreviewCamera();
        camera.ApplyPreset(PreviewCameraPreset.Blueprint);
        (Vector3 min, Vector3 max) = PreviewSceneMesh.ComputeBounds(scene);
        camera.FrameBounds(in min, in max, paddingFactor: 1.15f);

        Assert.True(renderer.SetSceneMeshes(scene).Success);
        Result<byte[]> frame1 = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, camera, _clearColor);
        Result<byte[]> frame2 = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, camera, _clearColor);
        Assert.True(frame1.Success, frame1.Message);
        Assert.True(frame2.Success, frame2.Message);
        Assert.Equal(HashBytes(frame1.Value), HashBytes(frame2.Value));

        SavePreviewPng("gl-09b-blueprint-wrecked-lifeboat.png", RENDER_SIZE, RENDER_SIZE, frame1.Value);
        AssertRenderedSurface(frame1.Value, minCoveredPixels: 12_000, minDistinctColors: 12);
        (int magentaPixels, _) = CountMagentaAndBlackCheckerPixels(frame1.Value);
        Assert.Equal(0, magentaPixels);
    }

    /// <summary>Renders ViewModel first-person scene and WorldModel floor+body reference scene with 3D gizmos under software OpenGL.</summary>
    [Fact]
    public void SceneRenderer_RendersViewModelAndWorldModelEditorScenesWithGizmosAndReferences()
    {
        string repoRoot = FindRepoRoot();
        var loader = new ToolsWorkspace.WorkspaceLoader();
        Result<ToolsWorkspace.WorkspaceIndex> wsResult = loader.LoadRepo(repoRoot);
        Assert.True(wsResult.Success, wsResult.Message);
        ToolsWorkspace.WorkspaceIndex workspace = wsResult.Value;

        string clientAssets = Path.Combine(repoRoot, "WaywardBeyond.Client", "assets");
        string bodiesAssets = Path.Combine(repoRoot, "WaywardBeyond.Bodies", "assets");
        string[] assetRoots = [clientAssets, bodiesAssets];
        var objParser = new ObjParser();
        Mesh drillObj = objParser.Parse(new PathInfo(Path.Combine(clientAssets, "meshes", "items", "drill.obj")));
        var drillPreviewMesh = new VoxelMesh(drillObj.Triangles, drillObj.Vertices, drillObj.Colors, drillObj.Uv, drillObj.Normals);
        Result<StandaloneTextureData> drillTex = StandaloneTextureLoader.LoadFromAssetRoots(assetRoots, "items/drill_uv.png");
        Assert.True(drillTex.Success, drillTex.Message);

        BodyReferenceSpriteInfo? bodyRef = WorkspaceModelReferenceResolver.ResolveDeterministicBodySprite(
            workspace,
            documents: null,
            rel => StandaloneTextureLoader.ResolveTexturePath(assetRoots, rel));
        Assert.True(bodyRef.HasValue && bodyRef.Value.Texture.HasValue && bodyRef.Value.Texture.Value.IsValid);

        using var glContext = HeadlessEglContext.Create();
        using var renderer = new SceneRenderer();
        Assert.True(renderer.Initialize(glContext.GetProcAddress, isOpenGlEs: false).Success);
        Assert.True(renderer.UploadStandaloneTexture(drillTex.Value).Success);
        Assert.True(renderer.UploadReferenceTexture(bodyRef!.Value.Texture!.Value).Success);

        // 1. First-person ViewModel scene (wb:drill exact ViewModel transform + 90 deg FOV + Translate overlay gizmo)
        var vmCamera = new PreviewCamera();
        vmCamera.ConfigureFirstPersonViewModel(90f);
        var vmPos = new Vector3(0.5f, -0.5f, -0.3f);
        var vmRot = new Vector3(0f, 5f, 0f);
        var vmScale = Vector3.One;
        Matrix4x4 vmModel = ModelTransformMath.CreateModelMatrix(vmPos, vmRot, vmScale);
        Matrix4x4 vmBasis = ModelTransformMath.CreateRotationMatrix(vmRot);
        Vector3 vmGizmoOrigin = TransformGizmoMeshBuilder.ResolveGizmoAnchor(vmPos, vmRot, vmScale, in drillPreviewMesh, vmCamera, 640f, 360f);
        float vmGizmoScale = TransformGizmoMeshBuilder.ComputeScreenConstantScale(vmGizmoOrigin, vmCamera);

        var vmScene = new List<PreviewSceneMesh>
        {
            new(drillPreviewMesh, vmModel, BrickAlpha.Masked, 0.5f, doubleSided: true, textureMode: PreviewTextureMode.StandaloneTexture),
        };
        vmScene.AddRange(TransformGizmoMeshBuilder.BuildMeshes(
            TransformGizmoMode.Translate,
            vmGizmoOrigin,
            in vmBasis,
            vmGizmoScale,
            TransformGizmoAxis.X,
            TransformGizmoAxis.None));

        Assert.True(renderer.SetSceneMeshes(vmScene).Success);
        Result<byte[]> vmFrame1 = renderer.RenderToRgba(640, 360, vmCamera, _clearColor);
        Result<byte[]> vmFrame2 = renderer.RenderToRgba(640, 360, vmCamera, _clearColor);
        Assert.True(vmFrame1.Success, vmFrame1.Message);
        Assert.True(vmFrame2.Success, vmFrame2.Message);
        Assert.Equal(HashBytes(vmFrame1.Value), HashBytes(vmFrame2.Value));
        SavePreviewPng("gl-10-viewmodel-editor-drill.png", 640, 360, vmFrame1.Value);
        AssertRenderedSurface(vmFrame1.Value, minCoveredPixels: 6_000, minDistinctColors: 12);
        (int vmMagenta, _) = CountMagentaAndBlackCheckerPixels(vmFrame1.Value);
        Assert.Equal(0, vmMagenta);

        // 2. WorldModel scene (1.0 m floor grid at origin + deterministic 1.8 m Body sprite at (0,0,-1) + wb:drill + Rotate gizmo)
        var wmCamera = new PreviewCamera();
        wmCamera.ApplyPreset(PreviewCameraPreset.ItemWorldModelEditor);
        var wmPos = new Vector3(0f, 0.45f, 0f);
        var wmRot = new Vector3(0f, -25f, 0f);
        var wmScale = Vector3.One;
        Matrix4x4 wmModel = ModelTransformMath.CreateModelMatrix(wmPos, wmRot, wmScale);
        Matrix4x4 wmBasis = ModelTransformMath.CreateRotationMatrix(wmRot);
        float wmGizmoScale = TransformGizmoMeshBuilder.ComputeScreenConstantScale(wmPos, wmCamera);
        StandaloneTextureData refTex = bodyRef.Value.Texture.Value;
        float bodyAspect = refTex.Width / (float)refTex.Height;

        var wmScene = new List<PreviewSceneMesh>();
        wmScene.AddRange(PreviewGeometryFactory.CreateWorldModelFloorPlane());
        wmScene.Add(PreviewGeometryFactory.CreateBodyReferencePlane(bodyAspect));
        wmScene.Add(new PreviewSceneMesh(drillPreviewMesh, wmModel, BrickAlpha.Masked, 0.5f, doubleSided: true, textureMode: PreviewTextureMode.StandaloneTexture));
        wmScene.AddRange(TransformGizmoMeshBuilder.BuildMeshes(
            TransformGizmoMode.Rotate,
            wmPos,
            in wmBasis,
            wmGizmoScale,
            TransformGizmoAxis.Y,
            TransformGizmoAxis.None));

        Assert.True(renderer.SetSceneMeshes(wmScene).Success);
        Result<byte[]> wmFrame1 = renderer.RenderToRgba(640, 480, wmCamera, _clearColor);
        Result<byte[]> wmFrame2 = renderer.RenderToRgba(640, 480, wmCamera, _clearColor);
        Assert.True(wmFrame1.Success, wmFrame1.Message);
        Assert.True(wmFrame2.Success, wmFrame2.Message);
        Assert.Equal(HashBytes(wmFrame1.Value), HashBytes(wmFrame2.Value));
        SavePreviewPng("gl-11-worldmodel-editor-drill.png", 640, 480, wmFrame1.Value);
        AssertRenderedSurface(wmFrame1.Value, minCoveredPixels: 25_000, minDistinctColors: 16);
        (int wmMagenta, _) = CountMagentaAndBlackCheckerPixels(wmFrame1.Value);
        Assert.Equal(0, wmMagenta);
    }

    /// <summary>Verifies SceneRenderer drains pre-existing GL errors and preserves item standalone textures across reopen and reinit.</summary>
    [Fact]
    public void SceneRenderer_DrainsStaleErrorsAndPreservesTexturesAcrossReopenAndReinit()
    {
        string repoRoot = FindRepoRoot();
        string clientAssets = Path.Combine(repoRoot, "WaywardBeyond.Client", "assets");

        Result<AlbedoTextureArrayData> albedoResult = PbrAlbedoArrayLoader.LoadFromAssetRoots([clientAssets]);
        Assert.True(albedoResult.Success, albedoResult.Message);
        AlbedoTextureArrayData albedo = albedoResult.Value;

        var objParser = new ObjParser();
        Mesh laserObj = objParser.Parse(new PathInfo(Path.Combine(clientAssets, "meshes", "items", "laser.obj")));
        var laserPreviewMesh = new VoxelMesh(laserObj.Triangles, laserObj.Vertices, laserObj.Colors, laserObj.Uv, laserObj.Normals);
        Result<StandaloneTextureData> laserTex = StandaloneTextureLoader.LoadFromAssetRoots([clientAssets], "items/laser_uv.png");
        Assert.True(laserTex.Success, laserTex.Message);

        PreviewSceneMesh[] laserScene =
        [
            new(laserPreviewMesh, Matrix4x4.Identity, BrickAlpha.Masked, 0.5f, doubleSided: true, textureMode: PreviewTextureMode.StandaloneTexture),
        ];
        var camera = new PreviewCamera();
        camera.ApplyPreset(PreviewCameraPreset.ItemViewModel);
        (Vector3 min, Vector3 max) = PreviewSceneMesh.ComputeBounds(laserScene);
        camera.FrameBounds(in min, in max, paddingFactor: 1.35f);

        using var glContext = HeadlessEglContext.Create();
        using var rawGl = Silk.NET.OpenGL.GL.GetApi(glContext.GetProcAddress);
        using var renderer = new SceneRenderer();

        Result<GlInfo> initResult = renderer.Initialize(glContext.GetProcAddress, isOpenGlEs: false);
        Assert.True(initResult.Success, initResult.Message);
        Assert.True(renderer.UploadAlbedoArray(albedo).Success);
        Assert.True(renderer.UploadStandaloneTexture(laserTex.Value).Success);
        Assert.True(renderer.SetSceneMeshes(laserScene).Success);

        Result<byte[]> baselineFrame = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, camera, _clearColor);
        Assert.True(baselineFrame.Success, baselineFrame.Message);
        int baselineOrangeRed = CountOrangeRedPixels(baselineFrame.Value);
        Assert.True(baselineOrangeRed > 500, $"Expected orange/red textured laser pixels > 500, got {baselineOrangeRed}.");
        (int baselineMagenta, _) = CountMagentaAndBlackCheckerPixels(baselineFrame.Value);
        Assert.Equal(0, baselineMagenta);
        string baselineHash = HashBytes(baselineFrame.Value);

        // Inject a stale GL_INVALID_ENUM before each upload and draw call to mirror host compositor/swapchain error leakage.
        rawGl.Enable((Silk.NET.OpenGL.EnableCap)0xFFFF);
        Result staleAlbedo = renderer.UploadAlbedoArray(albedo);
        Assert.True(staleAlbedo.Success, staleAlbedo.Message);

        rawGl.Enable((Silk.NET.OpenGL.EnableCap)0xFFFF);
        Result staleStandalone = renderer.UploadStandaloneTexture(laserTex.Value);
        Assert.True(staleStandalone.Success, staleStandalone.Message);

        rawGl.Enable((Silk.NET.OpenGL.EnableCap)0xFFFF);
        Result staleMeshes = renderer.SetSceneMeshes(laserScene);
        Assert.True(staleMeshes.Success, staleMeshes.Message);

        rawGl.Enable((Silk.NET.OpenGL.EnableCap)0xFFFF);
        Result<byte[]> recoveredFrame = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, camera, _clearColor);
        Assert.True(recoveredFrame.Success, recoveredFrame.Message);
        Assert.Equal(baselineHash, HashBytes(recoveredFrame.Value));

        // Simulate full context teardown and reopen initialization.
        renderer.Deinitialize();
        Result<GlInfo> reinitResult = renderer.Initialize(glContext.GetProcAddress, isOpenGlEs: false);
        Assert.True(reinitResult.Success, reinitResult.Message);
        Assert.True(renderer.UploadAlbedoArray(albedo).Success);
        Assert.True(renderer.UploadStandaloneTexture(laserTex.Value).Success);
        Assert.True(renderer.SetSceneMeshes(laserScene).Success);

        Result<byte[]> reinitFrame = renderer.RenderToRgba(RENDER_SIZE, RENDER_SIZE, camera, _clearColor);
        Assert.True(reinitFrame.Success, reinitFrame.Message);
        Assert.Equal(baselineHash, HashBytes(reinitFrame.Value));
    }

    private static int CountOrangeRedPixels(byte[] rgba)
    {
        var count = 0;
        for (var i = 0; i < rgba.Length; i += 4)
        {
            byte r = rgba[i];
            byte b = rgba[i + 2];
            if (r > 150 && b < 80)
            {
                count++;
            }
        }

        return count;
    }

    private static VoxelRegionData BuildSampleBlueprintRegion(SimpleBrickDatabase bricks)
    {
        ushort panel = bricks.Id("wb:panel");
        ushort caution = bricks.Id("wb:caution_panel");
        ushort core = bricks.Id("wb:core");
        ushort console = bricks.Id("wb:display_console");

        var voxelObject = new VoxelObject(8);
        for (var x = 0; x < 3; x++)
        {
            for (var z = 0; z < 3; z++)
            {
                ushort floorId = (x == 1 && z == 1) ? core : ((x == 0 || x == 2) && z == 2 ? caution : panel);
                voxelObject.Set(x, 0, z, new Voxel(floorId, new ShapeLight(BrickShape.Block, 15), 0));
            }
        }

        voxelObject.Set(1, 1, 1, new Voxel(console, new ShapeLight(BrickShape.Slope, 15), 0));
        voxelObject.Set(0, 1, 0, new Voxel(panel, new ShapeLight(BrickShape.Column, 15), 0));
        voxelObject.Set(2, 1, 0, new Voxel(panel, new ShapeLight(BrickShape.Column, 15), 0));
        voxelObject.Set(1, 1, 0, new Voxel(caution, new ShapeLight(BrickShape.Stair, 15), 0));

        Assert.True(voxelObject.TryGetChunk(new Short3(0, 0, 0), out ChunkData chunk));
        return new VoxelRegionData(3, 2, 3, [new ChunkInfo(0, 0, 0, chunk.Data)], null, null);
    }

    private static void AssertRenderedSurface(byte[] rgba, int minCoveredPixels, int minDistinctColors)
    {
        byte bgR = (byte)Math.Round(_clearColor.X * 255f);
        byte bgG = (byte)Math.Round(_clearColor.Y * 255f);
        byte bgB = (byte)Math.Round(_clearColor.Z * 255f);

        var covered = 0;
        var distinctColors = new HashSet<int>();

        for (var i = 0; i < rgba.Length; i += 4)
        {
            byte r = rgba[i];
            byte g = rgba[i + 1];
            byte b = rgba[i + 2];
            if (Math.Abs(r - bgR) <= 2 && Math.Abs(g - bgG) <= 2 && Math.Abs(b - bgB) <= 2)
            {
                continue;
            }

            covered++;
            int quantized = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
            distinctColors.Add(quantized);
        }

        Assert.True(covered >= minCoveredPixels, $"Expected at least {minCoveredPixels} covered pixels, got {covered}.");
        Assert.True(distinctColors.Count >= minDistinctColors, $"Expected at least {minDistinctColors} distinct colors, got {distinctColors.Count}.");
    }

    private static (int MagentaPixels, int BlackPixels) CountMagentaAndBlackCheckerPixels(byte[] rgba)
    {
        var magenta = 0;
        var black = 0;
        for (var i = 0; i < rgba.Length; i += 4)
        {
            byte r = rgba[i];
            byte g = rgba[i + 1];
            byte b = rgba[i + 2];
            if (r >= 200 && g <= 40 && b >= 200)
            {
                magenta++;
            }
            else if (r <= 12 && g <= 12 && b <= 12)
            {
                black++;
            }
        }

        return (magenta, black);
    }

    private static void AssertGridCellsPopulated(byte[] rgba, int width, int height)
    {
        byte bgR = (byte)Math.Round(_clearColor.X * 255f);
        byte bgG = (byte)Math.Round(_clearColor.Y * 255f);
        byte bgB = (byte)Math.Round(_clearColor.Z * 255f);

        int[] quadrantHits = new int[4];
        int halfW = width / 2;
        int halfH = height / 2;

        for (var y = 0; y < height; y++)
        {
            int rowBase = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                int idx = rowBase + (x * 4);
                if (Math.Abs(rgba[idx] - bgR) <= 2 && Math.Abs(rgba[idx + 1] - bgG) <= 2 && Math.Abs(rgba[idx + 2] - bgB) <= 2)
                {
                    continue;
                }

                int q = (x >= halfW ? 1 : 0) + (y >= halfH ? 2 : 0);
                quadrantHits[q]++;
            }
        }

        for (var q = 0; q < 4; q++)
        {
            Assert.True(quadrantHits[q] > 1_000, $"Expected Shape=Any grid quadrant {q} to contain geometry, got {quadrantHits[q]} pixels.");
        }
    }

    private static string HashBytes(byte[] data)
    {
        return Convert.ToHexString(SHA256.HashData(data));
    }

    private static void SavePreviewPng(string fileName, int width, int height, byte[] rgba)
    {
        byte[] png = PngEncoder.EncodeRgba(width, height, rgba);
        string? configured = Environment.GetEnvironmentVariable(PREVIEW_OUT_DIRS_ENV);
        string[] outputDirs = string.IsNullOrWhiteSpace(configured)
            ? [Path.Combine(Path.GetTempPath(), "wb-asset-studio-gl-previews")]
            : configured.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < outputDirs.Length; i++)
        {
            string dir = outputDirs[i];
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, fileName), png);
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "Swordfish.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Swordfish.sln.");
    }

    private sealed class ObjPreviewMeshDatabase : IAssetDatabase<VoxelMesh>
    {
        private readonly Dictionary<string, VoxelMesh> _meshes = new(StringComparer.Ordinal);

        public ObjPreviewMeshDatabase(string meshDirectory)
        {
            var parser = new ObjParser();
            foreach (string file in Directory.EnumerateFiles(meshDirectory, "*.obj", SearchOption.TopDirectoryOnly))
            {
                string id = Path.GetFileName(file);
                Mesh mesh = parser.Parse(new PathInfo(file));
                _meshes[id] = new VoxelMesh(mesh.Triangles, mesh.Vertices, mesh.Colors, mesh.Uv, mesh.Normals);
            }
        }

        public Result<VoxelMesh> Get(string id)
        {
            return _meshes.TryGetValue(id, out VoxelMesh mesh)
                ? Result<VoxelMesh>.FromSuccess(mesh)
                : Result<VoxelMesh>.FromFailure($"Mesh '{id}' not found.");
        }
    }

    private sealed class SimpleBrickDatabase : IBrickDatabase, IBrickRegistry
    {
        private readonly Dictionary<string, ushort> _idByName = new(StringComparer.Ordinal);
        private readonly Dictionary<ushort, Brick> _brickById = [];
        private ushort _nextId = 1;

        public static SimpleBrickDatabase LoadFromDirectory(string directory)
        {
            var db = new SimpleBrickDatabase();
            foreach (string file in Directory.EnumerateFiles(directory, "*.toml", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                BrickDefinitions defs = Toml.To<BrickDefinitions>(File.ReadAllText(file));
                if (defs.Bricks == null)
                {
                    continue;
                }

                for (var i = 0; i < defs.Bricks.Count; i++)
                {
                    BrickDefinition def = defs.Bricks[i];
                    if (string.IsNullOrWhiteSpace(def.ID))
                    {
                        continue;
                    }

                    Vector3 color = Vector3.One;
                    if (!string.IsNullOrWhiteSpace(def.Color) && HexColor.TryParseRgb(def.Color, out Vector3 parsed))
                    {
                        color = parsed;
                    }

                    var brick = new Brick(
                        def.ID,
                        def.Alpha,
                        def.Passable,
                        def.Mesh,
                        def.Shape,
                        def.Textures,
                        def.CastShadows,
                        color: color
                    );
                    db.Add(brick);
                }
            }

            return db;
        }

        public Brick RegisterSynthetic(string id, BrickShape shape, BrickTextures textures)
        {
            var brick = new Brick(id, BrickAlpha.Opaque, passable: false, meshID: null, shape, textures);
            Add(brick);
            return brick;
        }

        public ushort Id(string name) => _idByName.TryGetValue(name, out ushort id) ? id : (ushort)0;

        public string? Name(ushort id) => _brickById.TryGetValue(id, out Brick? brick) ? brick.ID : null;

        public Result<Brick> Get(string id)
        {
            return _idByName.TryGetValue(id, out ushort dataId) && _brickById.TryGetValue(dataId, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure($"Unknown brick '{id}'.");
        }

        public Result<Brick> Get(ushort id)
        {
            return _brickById.TryGetValue(id, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure($"Unknown brick '{id}'.");
        }

        public List<Brick> Get(Func<Brick, bool> predicate) => [.. _brickById.Values.Where(predicate)];

        public bool IsCuller(in Voxel voxel, BrickShape shape)
        {
            if (voxel.ID == 0 || shape != BrickShape.Block)
            {
                return false;
            }

            return _brickById.TryGetValue(voxel.ID, out Brick? brick)
                && !brick.Passable
                && brick.Alpha == BrickAlpha.Opaque
                && brick.CastShadows;
        }

        private void Add(Brick brick)
        {
            ushort id = _nextId++;
            _idByName[brick.ID] = id;
            _brickById[id] = brick;
        }
    }

    private sealed class HeadlessEglContext : IDisposable
    {
        private const int EGL_NONE = 0x3038;
        private const int EGL_RED_SIZE = 0x3024;
        private const int EGL_GREEN_SIZE = 0x3023;
        private const int EGL_BLUE_SIZE = 0x3022;
        private const int EGL_ALPHA_SIZE = 0x3021;
        private const int EGL_DEPTH_SIZE = 0x3025;
        private const int EGL_SURFACE_TYPE = 0x3033;
        private const int EGL_PBUFFER_BIT = 0x0001;
        private const int EGL_RENDERABLE_TYPE = 0x3040;
        private const int EGL_OPENGL_BIT = 0x0008;
        private const int EGL_OPENGL_API = 0x30A2;
        private const int EGL_WIDTH = 0x3057;
        private const int EGL_HEIGHT = 0x3056;
        private const int EGL_CONTEXT_MAJOR_VERSION = 0x3098;
        private const int EGL_CONTEXT_MINOR_VERSION = 0x30FB;
        private const int EGL_CONTEXT_OPENGL_PROFILE_MASK = 0x30FD;
        private const int EGL_CONTEXT_OPENGL_CORE_PROFILE_BIT = 0x0001;
        private const uint EGL_PLATFORM_SURFACELESS_MESA = 0x31DD;

        private readonly nint _display;
        private readonly nint _surface;
        private readonly nint _context;
        private readonly nint _libGl;

        private HeadlessEglContext(nint display, nint surface, nint context, nint libGl)
        {
            _display = display;
            _surface = surface;
            _context = context;
            _libGl = libGl;
        }

        public static HeadlessEglContext Create()
        {
            nint display = EglGetDisplay(0);
            if (display == 0 || !EglInitialize(display, out _, out _))
            {
                display = EglGetPlatformDisplay(EGL_PLATFORM_SURFACELESS_MESA, 0, 0);
                if (display == 0 || !EglInitialize(display, out _, out _))
                {
                    throw new InvalidOperationException($"Failed to initialize EGL display (error 0x{EglGetError():X}).");
                }
            }

            if (!EglBindAPI(EGL_OPENGL_API))
            {
                throw new InvalidOperationException($"Failed to bind EGL OpenGL API (error 0x{EglGetError():X}).");
            }

            int[] configAttribs =
            [
                EGL_SURFACE_TYPE, EGL_PBUFFER_BIT,
                EGL_RENDERABLE_TYPE, EGL_OPENGL_BIT,
                EGL_RED_SIZE, 8,
                EGL_GREEN_SIZE, 8,
                EGL_BLUE_SIZE, 8,
                EGL_ALPHA_SIZE, 8,
                EGL_DEPTH_SIZE, 24,
                EGL_NONE,
            ];

            nint[] configs = new nint[1];
            if (!EglChooseConfig(display, configAttribs, configs, 1, out int numConfigs) || numConfigs < 1)
            {
                throw new InvalidOperationException($"Failed to choose EGL OpenGL config (error 0x{EglGetError():X}).");
            }

            int[] pbufferAttribs = [EGL_WIDTH, 16, EGL_HEIGHT, 16, EGL_NONE];
            nint surface = EglCreatePbufferSurface(display, configs[0], pbufferAttribs);

            int[] ctxAttribs =
            [
                EGL_CONTEXT_MAJOR_VERSION, 3,
                EGL_CONTEXT_MINOR_VERSION, 3,
                EGL_CONTEXT_OPENGL_PROFILE_MASK, EGL_CONTEXT_OPENGL_CORE_PROFILE_BIT,
                EGL_NONE,
            ];

            nint context = EglCreateContext(display, configs[0], 0, ctxAttribs);
            if (context == 0)
            {
                throw new InvalidOperationException($"Failed to create OpenGL 3.3 Core EGL context (error 0x{EglGetError():X}).");
            }

            if (!EglMakeCurrent(display, surface, surface, context))
            {
                throw new InvalidOperationException($"Failed to make EGL context current (error 0x{EglGetError():X}).");
            }

            NativeLibrary.TryLoad("libGL.so.1", out nint libGl);
            return new HeadlessEglContext(display, surface, context, libGl);
        }

        public nint GetProcAddress(string name)
        {
            nint proc = EglGetProcAddress(name);
            if (proc != 0)
            {
                return proc;
            }

            if (_libGl != 0 && NativeLibrary.TryGetExport(_libGl, name, out nint export))
            {
                return export;
            }

            return 0;
        }

        public void Dispose()
        {
            if (_display != 0)
            {
                EglMakeCurrent(_display, 0, 0, 0);
                if (_context != 0)
                {
                    EglDestroyContext(_display, _context);
                }

                if (_surface != 0)
                {
                    EglDestroySurface(_display, _surface);
                }

                EglTerminate(_display);
            }

            if (_libGl != 0)
            {
                NativeLibrary.Free(_libGl);
            }
        }

        [DllImport("libEGL.so.1", EntryPoint = "eglGetDisplay")]
        private static extern nint EglGetDisplay(nint nativeDisplay);

        [DllImport("libEGL.so.1", EntryPoint = "eglGetPlatformDisplay")]
        private static extern nint EglGetPlatformDisplay(uint platform, nint nativeDisplay, nint attribList);

        [DllImport("libEGL.so.1", EntryPoint = "eglInitialize")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool EglInitialize(nint display, out int major, out int minor);

        [DllImport("libEGL.so.1", EntryPoint = "eglBindAPI")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool EglBindAPI(int api);

        [DllImport("libEGL.so.1", EntryPoint = "eglChooseConfig")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool EglChooseConfig(nint display, int[] attribList, [Out] nint[] configs, int configSize, out int numConfig);

        [DllImport("libEGL.so.1", EntryPoint = "eglCreatePbufferSurface")]
        private static extern nint EglCreatePbufferSurface(nint display, nint config, int[] attribList);

        [DllImport("libEGL.so.1", EntryPoint = "eglCreateContext")]
        private static extern nint EglCreateContext(nint display, nint config, nint shareContext, int[] attribList);

        [DllImport("libEGL.so.1", EntryPoint = "eglMakeCurrent")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool EglMakeCurrent(nint display, nint draw, nint read, nint context);

        [DllImport("libEGL.so.1", EntryPoint = "eglGetProcAddress", CharSet = CharSet.Ansi)]
        private static extern nint EglGetProcAddress(string procName);

        [DllImport("libEGL.so.1", EntryPoint = "eglDestroyContext")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool EglDestroyContext(nint display, nint context);

        [DllImport("libEGL.so.1", EntryPoint = "eglDestroySurface")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool EglDestroySurface(nint display, nint surface);

        [DllImport("libEGL.so.1", EntryPoint = "eglTerminate")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool EglTerminate(nint display);

        [DllImport("libEGL.so.1", EntryPoint = "eglGetError")]
        private static extern int EglGetError();
    }

    private static class PngEncoder
    {
        private static readonly byte[] _signature = [137, 80, 78, 71, 13, 10, 26, 10];
        private static readonly uint[] _crcTable = BuildCrcTable();

        public static byte[] EncodeRgba(int width, int height, byte[] rgba)
        {
            using var output = new MemoryStream();
            output.Write(_signature);

            Span<byte> ihdr = stackalloc byte[13];
            BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], width);
            BinaryPrimitives.WriteInt32BigEndian(ihdr[4..8], height);
            ihdr[8] = 8;
            ihdr[9] = 6;
            ihdr[10] = 0;
            ihdr[11] = 0;
            ihdr[12] = 0;
            WriteChunk(output, "IHDR"u8, ihdr);

            byte[] compressed;
            using (var zlibBuffer = new MemoryStream())
            {
                using (var zlib = new ZLibStream(zlibBuffer, CompressionLevel.Fastest, leaveOpen: true))
                {
                    int rowBytes = width * 4;
                    for (var y = 0; y < height; y++)
                    {
                        zlib.WriteByte(0);
                        zlib.Write(rgba, y * rowBytes, rowBytes);
                    }
                }

                compressed = zlibBuffer.ToArray();
            }

            WriteChunk(output, "IDAT"u8, compressed);
            WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
            return output.ToArray();
        }

        private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
        {
            Span<byte> lenBytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(lenBytes, data.Length);
            stream.Write(lenBytes);
            stream.Write(type);
            stream.Write(data);

            uint crc = ComputeCrc(type, data);
            Span<byte> crcBytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
            stream.Write(crcBytes);
        }

        private static uint ComputeCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFFu;
            for (var i = 0; i < type.Length; i++)
            {
                crc = _crcTable[(crc ^ type[i]) & 0xFF] ^ (crc >> 8);
            }

            for (var i = 0; i < data.Length; i++)
            {
                crc = _crcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFFu;
        }

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (var k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                }

                table[n] = c;
            }

            return table;
        }
    }
}
