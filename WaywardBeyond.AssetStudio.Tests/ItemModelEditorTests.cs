using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Swordfish.Library.Util;
using WaywardBeyond.AssetStudio.Controls;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.AssetStudio.ViewModels;
using WaywardBeyond.AssetStudio.Views;
using WaywardBeyond.Bricks;
using WaywardBeyond.Items;
using ToolsSettings = WaywardBeyond.AssetTools.Settings;
using ToolsWorkspace = WaywardBeyond.AssetTools.Workspace;
using Xunit;

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>Covers transform math parity, 3D gizmo math, deterministic body reference, TOML round-trip, and UI flows.</summary>
public sealed class ItemModelEditorTests
{
    private const float DEGREES_TO_RADIANS = MathF.PI / 180f;

    /// <summary>Verifies <see cref="ModelTransformMath.CreateModelMatrix"/> matches engine YawPitchRoll row-vector multiplication.</summary>
    [Fact]
    public void ModelTransformMath_MatchesGameEngineTransformAndRoundTripsEuler()
    {
        var pos = new Vector3(0.45f, -0.35f, -0.52f);
        var rot = new Vector3(14f, -60f, 22f);
        var scale = new Vector3(0.25f, 0.30f, 0.25f);

        Matrix4x4 actual = ModelTransformMath.CreateModelMatrix(pos, rot, scale);
        Quaternion q = Quaternion.CreateFromYawPitchRoll(
            rot.Y * DEGREES_TO_RADIANS,
            rot.X * DEGREES_TO_RADIANS,
            rot.Z * DEGREES_TO_RADIANS);
        Matrix4x4 expected = Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateFromQuaternion(q)
            * Matrix4x4.CreateTranslation(pos);

        Assert.True(Vector4DistanceMax(actual, expected) < 1e-5f);

        Matrix4x4 rotMat = ModelTransformMath.CreateRotationMatrix(rot);
        Vector3 extracted = ModelTransformMath.ExtractYawPitchRollDegrees(in rotMat);
        Assert.InRange(extracted.X, rot.X - 0.05f, rot.X + 0.05f);
        Assert.InRange(extracted.Y, rot.Y - 0.05f, rot.Y + 0.05f);
        Assert.InRange(extracted.Z, rot.Z - 0.05f, rot.Z + 0.05f);

        // Gimbal-lock pitch = +90 deg still preserves exact rotation matrix after extraction.
        var gimbalRot = new Vector3(90f, 37f, 0f);
        Matrix4x4 gimbalMat = ModelTransformMath.CreateRotationMatrix(gimbalRot);
        Vector3 gimbalExtracted = ModelTransformMath.ExtractYawPitchRollDegrees(in gimbalMat);
        Matrix4x4 reconstructed = ModelTransformMath.CreateRotationMatrix(gimbalExtracted);
        Assert.True(Vector4DistanceMax(gimbalMat, reconstructed) < 1e-3f);
    }

    /// <summary>Verifies 3D Translate, Rotate, and Scale gizmos support ray picking, dragging, snap, and uniform lock.</summary>
    [Fact]
    public void TransformGizmoController_HitTestsAndDragsAllAxesWithSnap()
    {
        const float w = 800f;
        const float h = 600f;

        var camera = new PreviewCamera();
        camera.ConfigureFirstPersonViewModel(90f);

        var pos = new Vector3(0.2f, -0.2f, -1.2f);
        var rot = Vector3.Zero;
        var scale = new Vector3(0.5f, 0.5f, 0.5f);
        var gizmo = new TransformGizmoController();

        float s = TransformGizmoMeshBuilder.ComputeScreenConstantScale(pos, camera);
        Assert.True(camera.TryProjectToScreen(pos + (Vector3.UnitX * (0.7f * s)), w, h, out Vector2 xHandleScreen, out _));

        // 1. Translate X handle pick + drag with snap = true (0.05 m steps)
        TransformGizmoAxis hitX = gizmo.HitTest(
            xHandleScreen,
            w,
            h,
            camera,
            TransformGizmoMode.Translate,
            TransformGizmoSpace.World,
            pos,
            rot);
        Assert.Equal(TransformGizmoAxis.X, hitX);
        Assert.True(gizmo.BeginDrag(hitX, xHandleScreen, w, h, camera, TransformGizmoMode.Translate, TransformGizmoSpace.World, pos, rot, scale));

        Assert.True(gizmo.UpdateDrag(
            new Vector2(xHandleScreen.X + 48f, xHandleScreen.Y),
            w,
            h,
            camera,
            TransformGizmoMode.Translate,
            snapEnabled: true,
            uniformScale: true,
            out Vector3 newPos,
            out _,
            out _,
            out string? moveReadout));
        Assert.True(newPos.X > pos.X);
        Assert.Equal(pos.Y, newPos.Y, 3);
        Assert.Equal(pos.Z, newPos.Z, 3);
        Assert.Equal(0f, MathF.Abs((newPos.X - pos.X) % 0.05f), 3);
        Assert.NotNull(moveReadout);
        gizmo.EndDrag();

        // 2. Scale Uniform center cube pick + drag
        Assert.True(camera.TryProjectToScreen(pos, w, h, out Vector2 centerScreen, out _));
        TransformGizmoAxis hitUniform = gizmo.HitTest(
            centerScreen,
            w,
            h,
            camera,
            TransformGizmoMode.Scale,
            TransformGizmoSpace.Local,
            pos,
            rot);
        Assert.Equal(TransformGizmoAxis.Uniform, hitUniform);
        Assert.True(gizmo.BeginDrag(hitUniform, centerScreen, w, h, camera, TransformGizmoMode.Scale, TransformGizmoSpace.Local, pos, rot, scale));
        Assert.True(gizmo.UpdateDrag(
            new Vector2(centerScreen.X + 40f, centerScreen.Y),
            w,
            h,
            camera,
            TransformGizmoMode.Scale,
            snapEnabled: false,
            uniformScale: true,
            out _,
            out _,
            out Vector3 scaled,
            out _));
        Assert.True(scaled.X > scale.X);
        Assert.Equal(scaled.X, scaled.Y, 4);
        Assert.Equal(scaled.X, scaled.Z, 4);
        gizmo.EndDrag();

        // 3. Rotate Z ring pick in WorldModel orbit camera + 5-degree snap
        camera.ApplyPreset(PreviewCameraPreset.ItemWorldModelEditor);
        var worldPos = new Vector3(0f, 0.5f, 0f);
        float ws = TransformGizmoMeshBuilder.ComputeScreenConstantScale(worldPos, camera);
        Vector3 ringPoint = worldPos + (Vector3.UnitX * (0.92f * ws));
        Assert.True(camera.TryProjectToScreen(ringPoint, w, h, out Vector2 ringScreen, out _));
        TransformGizmoAxis hitRing = gizmo.HitTest(
            ringScreen,
            w,
            h,
            camera,
            TransformGizmoMode.Rotate,
            TransformGizmoSpace.World,
            worldPos,
            Vector3.Zero);
        Assert.NotEqual(TransformGizmoAxis.None, hitRing);
        Assert.True(gizmo.BeginDrag(hitRing, ringScreen, w, h, camera, TransformGizmoMode.Rotate, TransformGizmoSpace.World, worldPos, Vector3.Zero, Vector3.One));
        Assert.True(gizmo.UpdateDrag(
            new Vector2(ringScreen.X + 24f, ringScreen.Y - 36f),
            w,
            h,
            camera,
            TransformGizmoMode.Rotate,
            snapEnabled: true,
            uniformScale: true,
            out _,
            out Vector3 rotated,
            out _,
            out string? rotReadout));
        Assert.NotEqual(Vector3.Zero, rotated);
        Assert.NotNull(rotReadout);
        gizmo.EndDrag();
    }

    /// <summary>Verifies deterministic Body selection picks ordinal first Body with an existing sprite regardless of state.</summary>
    [Fact]
    public void WorkspaceModelReferenceResolver_SkipsMissingSpritesAndSelectsDeterministicFirstValidBody()
    {
        string repoRoot = FindRepoRoot();
        string realSprite = Path.Combine(repoRoot, "WaywardBeyond.Bodies", "assets", "textures", "characters", "human_m_standing.png");
        Assert.True(File.Exists(realSprite), $"Expected sprite '{realSprite}'.");

        var missingDoc = CreateBodyDoc("aa:missing", "standing", "front", "characters/does_not_exist.png", absPath: null);
        var sittingDoc = CreateBodyDoc("ab:sitting_only", "sitting", "back", "characters/human_m_standing.png", realSprite);
        var laterDoc = CreateBodyDoc("zz:standing", "standing", "front", "characters/human_m_standing.png", realSprite);

        BodyReferenceSpriteInfo? resolved = WorkspaceModelReferenceResolver.ResolveDeterministicBodySprite(
            workspace: null,
            [laterDoc, missingDoc, sittingDoc],
            _ => null);

        Assert.True(resolved.HasValue);
        Assert.Equal("ab:sitting_only", resolved.Value.BodyId);
        Assert.Equal("sitting", resolved.Value.StateName);
        Assert.Equal("back", resolved.Value.Direction);
        Assert.Equal(Path.GetFullPath(realSprite), Path.GetFullPath(resolved.Value.AbsolutePath));
        Assert.True(resolved.Value.Texture.HasValue && resolved.Value.Texture.Value.IsValid);
    }

    /// <summary>Verifies real ViewModel and WorldModel transform edits save losslessly and round-trip through ItemDefinitions.</summary>
    [AvaloniaFact]
    public void ItemModelEditor_SavesBothExistingAndMissingTransformSectionsLosslessly()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"wb-item-model-save-{Guid.NewGuid():N}");
        var dataRoot = Path.Combine(tempRoot, "data");
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(dataRoot);

        try
        {
            File.WriteAllText(Path.Combine(dataRoot, ToolsSettings.ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);
            var dataPaths = ToolsSettings.ToolDataPaths.Resolve(executableDirectory: dataRoot);
            var service = new AssetToolsStudioWorkspaceService(dataPaths);

            var packDir = Path.Combine(tempRoot, "test_pack");
            Assert.True(service.CreatePackWorkspace(packDir, "test_pack", "tp", "Test Pack", "Author").Success);

            var itemsDir = Path.Combine(packDir, "assets", "items");
            Directory.CreateDirectory(itemsDir);
            var itemsFile = Path.Combine(itemsDir, "custom_items.toml");
            const string initialToml = """
                # Preserved top comment
                [[Items]]
                ID          = "tp:existing_tool"
                Name        = "item.tp.tool"
                MaxStack    = 1

                [Items.ViewModel]
                Mesh        = "items/drill.obj"
                Material    = "items/drill"
                Position.X  = 0.5
                Position.Y  = -0.5
                Position.Z  = -0.3

                [[Items]]
                ID          = "tp:bare_block"
                Name        = "item.tp.block"
                MaxStack    = 64
                """;
            File.WriteAllText(itemsFile, initialToml);

            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            // 1. Edit existing tool ViewModel + WorldModel (WorldModel section doesn't exist yet on entry 0!)
            Assert.True(vm.SelectAsset(AssetKind.Item, "tp:existing_tool"));
            vm.OpenViewModelEditorCommand.Execute(null);
            Assert.NotNull(vm.ActiveModelEditor);
            vm.ActiveModelEditor!.PosX = 0.65;
            vm.ActiveModelEditor.RotYText = "-45";
            vm.ActiveModelEditor.ScaleXText = "0.35";
            vm.ActiveModelEditor.SelectWorldModelModeCommand.Execute(null);
            vm.ActiveModelEditor.PosY = 0.25;
            vm.ActiveModelEditor.RotZText = "15";

            // 2. Edit bare_block (neither ViewModel nor WorldModel exists yet on entry 1!)
            Assert.True(vm.SelectAsset(AssetKind.Item, "tp:bare_block"));
            Assert.Null(vm.ActiveModelEditor); // Switching documents auto-exits visual editor
            vm.OpenWorldModelEditorCommand.Execute(null);
            Assert.NotNull(vm.ActiveModelEditor);
            vm.ActiveModelEditor!.PosZ = -0.15;
            vm.ActiveModelEditor.ExitCommand.Execute(null);
            Assert.Null(vm.ActiveModelEditor);

            vm.SaveAllCommand.Execute(null);
            Assert.Equal(0, vm.DirtyCount);

            string savedText = File.ReadAllText(itemsFile);
            Assert.Contains("# Preserved top comment", savedText, StringComparison.Ordinal);

            ToolsWorkspace.DualTomlParseResult<ItemDefinitions> reparsed = ToolsWorkspace.DualTomlParser.Parse<ItemDefinitions>(savedText);
            Assert.True(reparsed.AuthoritativeSuccess, reparsed.AuthoritativeError);
            Assert.NotNull(reparsed.AuthoritativeValue.Items);
            Assert.Equal(2, reparsed.AuthoritativeValue.Items!.Count);

            ItemDefinition existingTool = reparsed.AuthoritativeValue.Items[0];
            Assert.Equal("tp:existing_tool", existingTool.ID);
            Assert.True(existingTool.ViewModel.HasValue);
            Assert.Equal(0.65f, existingTool.ViewModel.Value.Position.X, 3);
            Assert.Equal(-0.5f, existingTool.ViewModel.Value.Position.Y, 3);
            Assert.Equal(-45f, existingTool.ViewModel.Value.Rotation.Y, 2);
            Assert.Equal(0.35f, existingTool.ViewModel.Value.Scale.X, 3);
            Assert.Equal(0.35f, existingTool.ViewModel.Value.Scale.Y, 3);
            Assert.Equal(0.35f, existingTool.ViewModel.Value.Scale.Z, 3);

            Assert.True(existingTool.WorldModel.HasValue);
            Assert.Equal(0.25f, existingTool.WorldModel.Value.Position.Y, 3);
            Assert.Equal(15f, existingTool.WorldModel.Value.Rotation.Z, 2);

            ItemDefinition bareBlock = reparsed.AuthoritativeValue.Items[1];
            Assert.Equal("tp:bare_block", bareBlock.ID);
            Assert.True(bareBlock.WorldModel.HasValue);
            Assert.Equal(-0.15f, bareBlock.WorldModel.Value.Position.Z, 3);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupDir(tempRoot);
        }
    }

    /// <summary>Verifies full-workbench takeover entry, FOV/aspect/crosshair controls, atomic drag Undo/Redo, and Escape exit.</summary>
    [AvaloniaFact]
    public void VisualEditor_SupportsFovAspectCrosshairGizmoDragUndoRedoAndEscapeExit()
    {
        using var vm = new MainWindowViewModel(
            new SampleStudioWorkspaceService(),
            new SamplePreviewSurfaceProvider(),
            startOnWelcomeScreen: false);

        Assert.True(vm.SelectAsset(AssetKind.Item, "wb:thruster_item"));
        vm.SaveCurrentAssetCommand.Execute(null);
        Assert.False(vm.SelectedAsset!.IsDirty);
        Assert.False(vm.IsModelEditorVisible);
        Assert.True(vm.IsStandardWorkbenchVisible);

        vm.OpenViewModelEditorCommand.Execute(null);
        Assert.True(vm.IsModelEditorVisible);
        Assert.False(vm.IsStandardWorkbenchVisible);

        ItemModelEditorViewModel editor = vm.ActiveModelEditor!;
        Assert.Equal(ItemModelEditorMode.ViewModel, editor.Mode);
        Assert.Equal(90.0, editor.FovDegrees);
        Assert.Equal(ItemModelAspectPreset.Ratio16x9, editor.AspectPreset);
        Assert.True(editor.ShowCrosshairOverlay);

        Assert.True(editor.SetFovPresetCommand.CanExecute("75"));
        editor.SetFovPresetCommand.Execute("75");
        Assert.Equal(75.0, editor.FovDegrees);
        Assert.Equal("75", editor.FovText);
        editor.SetAspectPresetCommand.Execute(ItemModelAspectPreset.Ratio4x3);
        Assert.InRange(editor.ActiveViewportAspectRatio, 1.32, 1.35);
        editor.ToggleCrosshairCommand.Execute(null);
        Assert.False(editor.ShowCrosshairOverlay);

        // Simulate one multi-step gizmo drag -> should produce ONE atomic undo action
        Vector3 initialPos = ModelTransformMath.ParseFloat3(vm.SelectedAsset!.ItemViewModelPositionField!.Value, ModelTransformMath.DefaultViewModelPosition);
        editor.BeginGizmoDrag();
        editor.ApplyGizmoDragTransform(new Vector3(initialPos.X + 0.1f, initialPos.Y, initialPos.Z), ModelTransformMath.DefaultViewModelRotation, ModelTransformMath.DefaultViewModelScale, "X +0.10 m");
        editor.ApplyGizmoDragTransform(new Vector3(initialPos.X + 0.25f, initialPos.Y, initialPos.Z), ModelTransformMath.DefaultViewModelRotation, ModelTransformMath.DefaultViewModelScale, "X +0.25 m");
        editor.EndGizmoDrag(new Vector3(initialPos.X + 0.25f, initialPos.Y, initialPos.Z), ModelTransformMath.DefaultViewModelRotation, ModelTransformMath.DefaultViewModelScale);

        Assert.True(vm.SelectedAsset.IsDirty);
        Assert.True(vm.CanUndo);
        Assert.Equal(initialPos.X + 0.25, editor.PosX, 3);

        vm.UndoCommand.Execute(null);
        Assert.Equal(initialPos.X, editor.PosX, 3);
        Assert.False(vm.SelectedAsset.IsDirty);

        vm.RedoCommand.Execute(null);
        Assert.Equal(initialPos.X + 0.25, editor.PosX, 3);
        Assert.True(vm.SelectedAsset.IsDirty);

        // Switch to WorldModel mode & toggle reference aids
        editor.SelectWorldModelModeCommand.Execute(null);
        Assert.True(editor.IsWorldModelMode);
        Assert.Equal(0.0, editor.ActiveViewportAspectRatio);
        editor.ToggleFloorPlaneCommand.Execute(null);
        Assert.False(editor.ShowFloorPlane);

        // Escape key closes editor and preserves live draft edits on item
        vm.HandleEscapeKeyCommand.Execute(null);
        Assert.False(vm.IsModelEditorVisible);
        Assert.True(vm.IsStandardWorkbenchVisible);
        Assert.True(vm.SelectedAsset.IsDirty);
        Assert.Contains(ModelTransformMath.FormatComponent(initialPos.X + 0.25f), vm.SelectedAsset.ItemViewModelPositionField.Value, StringComparison.Ordinal);
    }

    /// <summary>Verifies the fallback body scale reference uses an exact projective transform fixed on the world z = -1 plane across orbit and pan angles.</summary>
    [Fact]
    public void ModelEditorFallbackControl_BodyReferencePerspectiveMatrixMatchesFixedWorldPlane()
    {
        const float w = 800f;
        const float h = 600f;
        const float bw = 32f;
        const float bh = 64f;
        float height = ModelTransformMath.PLAYER_STANDING_HEIGHT_METERS;
        float halfW = (height * (bw / bh)) * 0.5f;
        const float z = -1.0f;

        Vector3 tl = new(-halfW, height, z);
        Vector3 tr = new(halfW, height, z);
        Vector3 br = new(halfW, 0f, z);
        Vector3 bl = new(-halfW, 0f, z);

        (float Yaw, float Pitch, bool Pan)[] views =
        [
            (32f, 16f, false),
            (72f, 42f, false),
            (180f, 10f, false),
            (48f, 22f, true),
        ];

        for (var i = 0; i < views.Length; i++)
        {
            var camera = new PreviewCamera();
            camera.ApplyPreset(PreviewCameraPreset.ItemWorldModelEditor);
            camera.YawDegrees = views[i].Yaw;
            camera.PitchDegrees = views[i].Pitch;
            if (views[i].Pan)
            {
                camera.Pan(120f, -45f);
            }

            Matrix4x4 viewProj = camera.GetViewMatrix() * camera.GetProjectionMatrix(w / h);
            Assert.True(ModelEditorFallbackControl.TryBuildWorldQuadPerspectiveMatrix(
                tl,
                tr,
                bl,
                bw,
                bh,
                in viewProj,
                w,
                h,
                out Matrix mat));

            Assert.True(camera.TryProjectToScreen(tl, w, h, out Vector2 expTl, out _));
            Assert.True(camera.TryProjectToScreen(tr, w, h, out Vector2 expTr, out _));
            Assert.True(camera.TryProjectToScreen(br, w, h, out Vector2 expBr, out _));
            Assert.True(camera.TryProjectToScreen(bl, w, h, out Vector2 expBl, out _));

            Vector2 actTl = TransformProjective(in mat, 0.0, 0.0);
            Vector2 actTr = TransformProjective(in mat, bw, 0.0);
            Vector2 actBr = TransformProjective(in mat, bw, bh);
            Vector2 actBl = TransformProjective(in mat, 0.0, bh);

            Assert.True(Vector2.Distance(expTl, actTl) < 0.05f, $"View {i} TL mismatch: {expTl} vs {actTl}");
            Assert.True(Vector2.Distance(expTr, actTr) < 0.05f, $"View {i} TR mismatch: {expTr} vs {actTr}");
            Assert.True(Vector2.Distance(expBr, actBr) < 0.05f, $"View {i} BR mismatch: {expBr} vs {actBr}");
            Assert.True(Vector2.Distance(expBl, actBl) < 0.05f, $"View {i} BL mismatch: {expBl} vs {actBl}");

            if (views[i].Yaw > 170f)
            {
                // From behind (-Z looking toward +Z), left/right corners of the fixed plane flip on screen.
                Assert.True(actTr.X < actTl.X, "Back view must mirror horizontally instead of turning to face the camera.");
            }
        }

        static Vector2 TransformProjective(in Matrix m, double u, double v)
        {
            double x = (u * m.M11) + (v * m.M21) + m.M31;
            double y = (u * m.M12) + (v * m.M22) + m.M32;
            double wCoord = (u * m.M13) + (v * m.M23) + m.M33;
            return new Vector2((float)(x / wCoord), (float)(y / wCoord));
        }
    }

    /// <summary>Renders the fallback viewport at steep oblique and edge-on angles to confirm rigid fixed-plane body rendering in Skia.</summary>
    [AvaloniaFact]
    public void ModelEditorFallbackControl_RendersFixedWorldPlaneWithoutFacingCameraAcrossOrbitAngles()
    {
        string repoRoot = FindRepoRoot();
        string realSprite = Path.Combine(repoRoot, "WaywardBeyond.Bodies", "assets", "textures", "characters", "alien2_f_standing.png");
        Assert.True(File.Exists(realSprite), $"Expected sprite '{realSprite}'.");

        float aspect;
        using (var probeBmp = new Bitmap(realSprite))
        {
            aspect = probeBmp.PixelSize.Width / (float)Math.Max(1, probeBmp.PixelSize.Height);
        }

        var control = new ModelEditorFallbackControl();
        var window = new Window
        {
            Width = 640,
            Height = 480,
            Content = control,
        };

        try
        {
            var state = new ModelEditorRenderState
            {
                ModePreset = PreviewCameraPreset.ItemWorldModelEditor,
                IsWorldModel = true,
                Position = Vector3.Zero,
                RotationDegrees = Vector3.Zero,
                Scale = Vector3.One,
                ShowFloorPlane = true,
                ShowBodyReference = true,
                IsReadOnly = true,
                MeshGeometry = PreviewGeometryFactory.CreateTexturedCube(0.4f),
                TextureMode = PreviewTextureMode.VertexColorOnly,
                AlphaMode = BrickAlpha.Opaque,
                ReferenceBodyId = "wb:alien2_f",
                ReferenceBodySpritePath = realSprite,
                ReferenceBodyAspect = aspect,
            };

            window.Show();
            control.UpdateEditorState(in state);

            // 1. Steep oblique angle (yaw = 70 deg, pitch = 28 deg)
            control.Camera.YawDegrees = 70f;
            control.Camera.PitchDegrees = 28f;
            string obliquePath = RenderFallbackControlPng(window, control, "24c-worldmodel-body-oblique.png");

            // 2. Side edge-on angle (yaw = 90 deg centered at z = -1) -> collapses to edge line
            control.Camera.Target = new Vector3(0f, 0.82f, -1.0f);
            control.Camera.YawDegrees = 90f;
            control.Camera.PitchDegrees = 10f;
            string edgeOnPath = RenderFallbackControlPng(window, control, "24d-worldmodel-body-edge-on.png");

            Assert.True(new FileInfo(obliquePath).Length > 4096);
            Assert.True(new FileInfo(edgeOnPath).Length > 4096);
        }
        finally
        {
            window.Close();
        }

        static string RenderFallbackControlPng(Window host, ModelEditorFallbackControl ctrl, string fileName)
        {
            ctrl.InvalidateVisual();
            host.Measure(new Size(640, 480));
            host.Arrange(new Rect(0, 0, 640, 480));
            host.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            Dispatcher.UIThread.RunJobs();

            Directory.CreateDirectory(HeadlessRenderHelper.COORDINATOR_PREVIEW_DIR);
            string path = Path.Combine(HeadlessRenderHelper.COORDINATOR_PREVIEW_DIR, fileName);
            using var rtb = new RenderTargetBitmap(new PixelSize(640, 480), new Avalonia.Vector(96, 96));
            rtb.Render(host);
            rtb.Save(path);
            return path;
        }
    }

    /// <summary>Verifies Model Source Mesh and Material pickers use the full stacked card width when expanded.</summary>
    [AvaloniaFact]
    public void ItemModelEditorView_ModelSourceDropdownsUseFullSidebarWidth()
    {
        string repoRoot = FindRepoRoot();
        string tempDataRoot = Path.Combine(Path.GetTempPath(), $"wb-model-source-width-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDataRoot);

        try
        {
            File.WriteAllText(Path.Combine(tempDataRoot, ToolsSettings.ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);
            ToolsSettings.ToolDataPaths dataPaths = ToolsSettings.ToolDataPaths.Resolve(executableDirectory: tempDataRoot);
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            var preview = new StudioGlPreviewController();

            using var vm = new MainWindowViewModel(
                service,
                preview,
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: repoRoot);
            vm.IsDarkTheme = true;

            Assert.True(vm.SelectAsset(AssetKind.Item, "wb:drill"));
            vm.OpenViewModelEditorCommand.Execute(null);
            Assert.NotNull(vm.ActiveModelEditor);

            var window = new MainWindow { DataContext = vm };
            try
            {
                window.Width = 1280;
                window.Height = 800;
                window.Show();
                Dispatcher.UIThread.RunJobs();

                ItemModelEditorView editorView = window.GetVisualDescendants()
                    .OfType<ItemModelEditorView>()
                    .First();
                CollapsibleSectionCard? modelSourceCard = editorView.FindControl<CollapsibleSectionCard>("ModelSourceCard");
                Assert.NotNull(modelSourceCard);
                modelSourceCard!.IsExpanded = true;

                vm.ActiveModelEditor!.ActiveMaterialField!.IsPickerOpen = true;
                window.Measure(new Size(1280, 800));
                window.Arrange(new Rect(0, 0, 1280, 800));
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();

                ScrollViewer? inspectorScroll = editorView.FindControl<ScrollViewer>("InspectorScrollViewer");
                Assert.NotNull(inspectorScroll);
                inspectorScroll!.ScrollToEnd();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();

                var pickers = modelSourceCard.GetVisualDescendants()
                    .OfType<ReferencePickerControl>()
                    .ToList();
                Assert.Equal(2, pickers.Count);
                for (var i = 0; i < pickers.Count; i++)
                {
                    Assert.True(pickers[i].Bounds.Width >= 240.0, $"Picker {i} width was {pickers[i].Bounds.Width:F1}, expected >= 240.");
                }

                Border? dropdownBorder = pickers[1].GetVisualDescendants()
                    .OfType<Border>()
                    .FirstOrDefault(b => b.IsVisible && b.GetVisualDescendants().OfType<ScrollViewer>().Any());
                Assert.NotNull(dropdownBorder);
                Assert.True(dropdownBorder!.Bounds.Width >= 240.0, $"Expanded dropdown width was {dropdownBorder.Bounds.Width:F1}, expected >= 240.");

                string pngPath = HeadlessRenderHelper.CaptureWindowPng(window, "24e-item-model-source-dropdown.png");
                Assert.True(File.Exists(pngPath));
                Assert.True(new FileInfo(pngPath).Length > 4096);
            }
            finally
            {
                vm.ActiveModelEditor!.ActiveMaterialField!.IsPickerOpen = false;
                window.Close();
            }

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupDir(tempDataRoot);
        }
    }

    /// <summary>Verifies closing and reopening the visual model editor keeps live GL active, fallback hidden when GL initializes, and textures preserved.</summary>
    [AvaloniaFact]
    public void ItemModelEditorView_CloseAndReopenKeepsLiveGlEnabledAndFallbackHiddenWhenGlInitialized()
    {
        string repoRoot = FindRepoRoot();
        string tempDataRoot = Path.Combine(Path.GetTempPath(), $"wb-model-reopen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDataRoot);

        try
        {
            File.WriteAllText(Path.Combine(tempDataRoot, ToolsSettings.ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);
            ToolsSettings.ToolDataPaths dataPaths = ToolsSettings.ToolDataPaths.Resolve(executableDirectory: tempDataRoot);
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            var preview = new StudioGlPreviewController();

            using var vm = new MainWindowViewModel(
                service,
                preview,
                startOnWelcomeScreen: false,
                enableLiveGlViewport: true,
                initialWorkspacePath: repoRoot);

            Assert.True(vm.SelectAsset(AssetKind.Item, "laser"));

            var window = new MainWindow { DataContext = vm };
            try
            {
                window.Width = 1280;
                window.Height = 800;
                window.Show();
                Dispatcher.UIThread.RunJobs();

                ItemModelEditorView editorView = window.GetVisualDescendants()
                    .OfType<ItemModelEditorView>()
                    .First();
                ModelEditorFallbackControl? fallbackViewport = editorView.FindControl<ModelEditorFallbackControl>("FallbackModelViewport");
                Assert.NotNull(fallbackViewport);

                // Closed state (DataContext == null): FallbackValue=False prevents fallback flash.
                Assert.False(fallbackViewport!.IsVisible);

                // Inspector GL failure must never disable ModelEditorViewport for future openings.
                vm.HandleGlViewportError("simulated inspector GL error");
                vm.OpenViewModelEditorCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();

                Assert.NotNull(vm.ActiveModelEditor);
                Assert.True(vm.ActiveModelEditor!.ShowLiveGlViewport);
                Assert.Equal(PreviewTextureMode.StandaloneTexture, vm.ActiveModelEditor.CurrentRenderState.TextureMode);
                Assert.True(vm.ActiveModelEditor.CurrentRenderState.ItemStandaloneTexture is { IsValid: true });

                // Clean init hides fallback; transient error shows fallback; subsequent clean frame recovers.
                vm.ActiveModelEditor.ReportGlInitialized();
                Dispatcher.UIThread.RunJobs();
                Assert.False(vm.ActiveModelEditor.ShowFallbackViewport);
                Assert.False(fallbackViewport.IsVisible);

                vm.ActiveModelEditor.ReportGlFailed();
                Dispatcher.UIThread.RunJobs();
                Assert.True(vm.ActiveModelEditor.ShowFallbackViewport);
                Assert.True(fallbackViewport.IsVisible);

                vm.ActiveModelEditor.ReportGlInitialized();
                Dispatcher.UIThread.RunJobs();
                Assert.False(vm.ActiveModelEditor.ShowFallbackViewport);
                Assert.False(fallbackViewport.IsVisible);

                // Close -> Reopen ViewModel -> Close -> Open WorldModel preserves live GL + standalone texture.
                vm.CloseModelEditorCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Null(vm.ActiveModelEditor);
                Assert.False(fallbackViewport.IsVisible);

                vm.OpenViewModelEditorCommand.Execute(null);
                Assert.NotNull(vm.ActiveModelEditor);
                Assert.True(vm.ActiveModelEditor!.ShowLiveGlViewport);
                Assert.Equal(PreviewTextureMode.StandaloneTexture, vm.ActiveModelEditor.CurrentRenderState.TextureMode);
                Assert.True(vm.ActiveModelEditor.CurrentRenderState.ItemStandaloneTexture is { IsValid: true });
                vm.ActiveModelEditor.ReportGlInitialized();
                Dispatcher.UIThread.RunJobs();
                Assert.False(vm.ActiveModelEditor.ShowFallbackViewport);
                Assert.False(fallbackViewport.IsVisible);

                vm.CloseModelEditorCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.False(fallbackViewport.IsVisible);

                vm.OpenWorldModelEditorCommand.Execute(null);
                Assert.NotNull(vm.ActiveModelEditor);
                Assert.True(vm.ActiveModelEditor!.IsWorldModelMode);
                Assert.True(vm.ActiveModelEditor.ShowLiveGlViewport);
                Assert.Equal(PreviewTextureMode.StandaloneTexture, vm.ActiveModelEditor.CurrentRenderState.TextureMode);
                Assert.True(vm.ActiveModelEditor.CurrentRenderState.ItemStandaloneTexture is { IsValid: true });
                vm.ActiveModelEditor.ReportGlInitialized();
                Dispatcher.UIThread.RunJobs();
                Assert.False(vm.ActiveModelEditor.ShowFallbackViewport);
                Assert.False(fallbackViewport.IsVisible);
            }
            finally
            {
                window.Close();
            }

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupDir(tempDataRoot);
        }
    }

    private static AssetDocumentViewModel CreateBodyDoc(string id, string state, string dir, string relTexPath, string? absPath)
    {
        var seed = new StudioAssetSeed
        {
            Id = id,
            Kind = AssetKind.Body,
            GroupTitle = "Bodies",
            RelativeFile = $"assets/bodies/{id.Replace(':', '_')}.toml",
            RootName = "test",
            Provenance = AssetProvenance.Pack,
            BodySprites = [(state, dir, relTexPath, "#5282B8")],
            BodySpriteAbsolutePaths = absPath != null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [$"{state}:{dir}"] = absPath }
                : [],
        };
        return new AssetDocumentViewModel(seed, WorkspaceMode.Pack, []);
    }

    private static float Vector4DistanceMax(in Matrix4x4 a, in Matrix4x4 b)
    {
        float[] diffs =
        [
            MathF.Abs(a.M11 - b.M11), MathF.Abs(a.M12 - b.M12), MathF.Abs(a.M13 - b.M13), MathF.Abs(a.M14 - b.M14),
            MathF.Abs(a.M21 - b.M21), MathF.Abs(a.M22 - b.M22), MathF.Abs(a.M23 - b.M23), MathF.Abs(a.M24 - b.M24),
            MathF.Abs(a.M31 - b.M31), MathF.Abs(a.M32 - b.M32), MathF.Abs(a.M33 - b.M33), MathF.Abs(a.M34 - b.M34),
            MathF.Abs(a.M41 - b.M41), MathF.Abs(a.M42 - b.M42), MathF.Abs(a.M43 - b.M43), MathF.Abs(a.M44 - b.M44),
        ];
        float max = 0f;
        for (var i = 0; i < diffs.Length; i++)
        {
            if (diffs[i] > max)
            {
                max = diffs[i];
            }
        }

        return max;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
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

    private static void CleanupDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Ignore transient cleanup locks.
        }
    }
}
