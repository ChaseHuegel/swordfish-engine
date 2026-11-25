using System.Numerics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.AssetStudio.ViewModels;
using WaywardBeyond.AssetStudio.Views;
using WaywardBeyond.AssetStudio.Views.Forms;
using WaywardBeyond.Meshing;
using Xunit;
using ToolsSettings = WaywardBeyond.AssetTools.Settings;

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>
///     Comprehensive headless integration tests covering Stage 2 Asset Studio and Asset Tools features (#1–#8).
/// </summary>
public sealed class StudioStage2IntegrationTests
{
    private const string SIMPLE_OBJ_TEXT = """
        v -0.5 -0.5  0.5
        v  0.5 -0.5  0.5
        v  0.0  0.5  0.0
        v  0.0 -0.5 -0.5
        f 1 2 3
        f 2 4 3
        f 4 1 3
        f 1 4 2
        """;

    /// <summary>
    ///     Item #1: Verifies per-axis and per-group resets plus cross-item Copy/Paste via MainWindowViewModel's persistent clipboard.
    /// </summary>
    [AvaloniaFact]
    public void ModelEditor_PerGroupAndPerAxisResets_AndCrossItemClipboard_WorkAcrossItems()
    {
        var tempRoot = CreateTempDirectory("model-clipboard");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var packDir = Path.Combine(tempRoot, "pack");
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "mc_pack", "mc", "Model Pack", "Tester").Success);

            File.WriteAllText(
                Path.Combine(packDir, "assets", "items", "item_a.toml"),
                """
                [[Items]]
                ID = "mc:item_a"
                Name = "item.mc.a"
                MaxStack = 16

                [Items.ViewModel]
                Position.X = 0.4
                Position.Y = -0.4
                Position.Z = -0.5
                Rotation.X = 0
                Rotation.Y = -60
                Rotation.Z = 0
                Scale.X = 0.25
                Scale.Y = 0.25
                Scale.Z = 0.25
                """);

            File.WriteAllText(
                Path.Combine(packDir, "assets", "items", "item_b.toml"),
                """
                [[Items]]
                ID = "mc:item_b"
                Name = "item.mc.b"
                MaxStack = 16

                [Items.ViewModel]
                Position.X = 0.4
                Position.Y = -0.4
                Position.Z = -0.5
                Rotation.X = 0
                Rotation.Y = -60
                Rotation.Z = 0
                Scale.X = 0.25
                Scale.Y = 0.25
                Scale.Z = 0.25
                """);

            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            Assert.True(vm.SelectAsset(AssetKind.Item, "mc:item_a"));
            vm.OpenViewModelEditorCommand.Execute(null);
            var editorA = vm.ActiveModelEditor;
            Assert.NotNull(editorA);

            // Modify X/Y/Z and test per-axis reset
            editorA.PosXText = "1.25";
            editorA.PosYText = "0.75";
            editorA.ResetAxisCommand.Execute("PosX");
            Assert.Equal(0.4, editorA.PosX, precision: 3);
            Assert.Equal(0.75, editorA.PosY, precision: 3);

            // Test per-group resets
            editorA.ResetPositionCommand.Execute(null);
            Assert.Equal(ModelTransformMath.DefaultViewModelPosition.X, editorA.PosX, precision: 3);
            Assert.Equal(ModelTransformMath.DefaultViewModelPosition.Y, editorA.PosY, precision: 3);
            Assert.Equal(ModelTransformMath.DefaultViewModelPosition.Z, editorA.PosZ, precision: 3);

            editorA.RotXText = "35";
            editorA.RotYText = "42";
            editorA.ResetAxisCommand.Execute("RotX");
            Assert.Equal(0.0, editorA.RotX, precision: 2);
            editorA.ResetRotationCommand.Execute(null);
            Assert.Equal(ModelTransformMath.DefaultViewModelRotation.X, editorA.RotX, precision: 2);
            Assert.Equal(ModelTransformMath.DefaultViewModelRotation.Y, editorA.RotY, precision: 2);
            Assert.Equal(ModelTransformMath.DefaultViewModelRotation.Z, editorA.RotZ, precision: 2);

            // Author custom transforms on Item A and copy groups to persistent MainWindowViewModel clipboard
            editorA.PosXText = "0.9";
            editorA.PosYText = "-0.1";
            editorA.PosZText = "-0.8";
            editorA.RotXText = "12";
            editorA.RotYText = "-30";
            editorA.RotZText = "5";
            editorA.UniformScale = false;
            editorA.ScaleXText = "0.5";
            editorA.ScaleYText = "0.6";
            editorA.ScaleZText = "0.7";

            editorA.CopyPositionCommand.Execute(null);
            editorA.CopyRotationCommand.Execute(null);
            editorA.CopyScaleCommand.Execute(null);

            // Close and switch to Item B, verifying previous editor does not stay stale and clipboard persists
            vm.CloseModelEditorCommand.Execute(null);
            Assert.Null(vm.ActiveModelEditor);
            Assert.True(vm.SelectAsset(AssetKind.Item, "mc:item_b"));
            vm.OpenViewModelEditorCommand.Execute(null);
            var editorB = vm.ActiveModelEditor;
            Assert.NotNull(editorB);
            Assert.True(editorB.CanPastePosition);
            Assert.True(editorB.CanPasteRotation);
            Assert.True(editorB.CanPasteScale);

            editorB.PastePositionCommand.Execute(null);
            editorB.PasteRotationCommand.Execute(null);
            editorB.PasteScaleCommand.Execute(null);

            Assert.Equal(0.9, editorB.PosX, precision: 3);
            Assert.Equal(-0.1, editorB.PosY, precision: 3);
            Assert.Equal(-0.8, editorB.PosZ, precision: 3);
            Assert.Equal(12.0, editorB.RotX, precision: 2);
            Assert.Equal(-30.0, editorB.RotY, precision: 2);
            Assert.Equal(5.0, editorB.RotZ, precision: 2);
            Assert.Equal(0.5, editorB.ScaleX, precision: 3);
            Assert.Equal(0.6, editorB.ScaleY, precision: 3);
            Assert.Equal(0.7, editorB.ScaleZ, precision: 3);

            // Per-group scale reset back to ViewModel defaults
            editorB.ResetScaleCommand.Execute(null);
            Assert.Equal(ModelTransformMath.DefaultViewModelScale.X, editorB.ScaleX, precision: 3);
            Assert.Equal(ModelTransformMath.DefaultViewModelScale.Y, editorB.ScaleY, precision: 3);
            Assert.Equal(ModelTransformMath.DefaultViewModelScale.Z, editorB.ScaleZ, precision: 3);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Item #2: Verifies explorer Copy, Paste, and Duplicate across real pack and sample mode with incrementing _2/_3 IDs and read-only guards.
    /// </summary>
    [AvaloniaFact]
    public void Explorer_CopyPasteAndDuplicate_AllocateIncrementingIdsAndRespectReadOnly()
    {
        // Part A: Real disk-backed pack with read-only reference root
        var tempRoot = CreateTempDirectory("explorer-copy-paste");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var baseInstallDir = Path.Combine(tempRoot, "base_install");
            var packDir = Path.Combine(tempRoot, "pack");

            var baseModuleDir = Path.Combine(baseInstallDir, "WaywardBeyond.Client");
            Directory.CreateDirectory(Path.Combine(baseModuleDir, "assets", "bricks"));
            File.WriteAllText(
                Path.Combine(baseModuleDir, "manifest.toml"),
                """
                ID = "waywardbeyond.client"
                Name = "Base Client"
                Assemblies = []
                """);
            File.WriteAllText(
                Path.Combine(baseModuleDir, "assets", "bricks", "core.toml"),
                """
                [[Bricks]]
                ID = "wb:core"
                Shape = "block"
                Color = "#8899AA"
                CastShadows = true
                Passable = false

                [Bricks.Textures]
                Default = ["core"]
                """);

            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "cp_pack", "cp", "Copy Pack", "Tester").Success);

            var bricksFile = Path.Combine(packDir, "assets", "bricks", "hull.toml");
            File.WriteAllText(
                bricksFile,
                """
                [[Bricks]]
                ID = "cp:hull"
                Shape = "block"
                Color = "#4A6B8A"
                CastShadows = true
                Passable = false

                [Bricks.Textures]
                Default = ["core"]
                """);

            Directory.CreateDirectory(Path.Combine(packDir, "assets", "materials", "bricks"));
            File.WriteAllText(
                Path.Combine(packDir, "assets", "materials", "bricks", "glossy.toml"),
                """
                Shader = "textured"
                AlphaMode = "Opaque"
                Textures = ["block/glass.png"]
                """);

            using var realVm = CreateViewModelWithReferenceRoot(service, packDir, baseInstallDir);
            Assert.False(realVm.CanPasteAsset);

            // Copy writable brick and Paste twice -> cp:hull_2, cp:hull_3
            Assert.True(realVm.SelectAsset(AssetKind.Brick, "cp:hull"));
            var hullDoc = realVm.SelectedAsset!;
            realVm.CopyAssetCommand.Execute(hullDoc);
            Assert.True(realVm.CanPasteAsset);
            Assert.NotNull(realVm.CopiedAssetClipboard);

            realVm.PasteAssetCommand.Execute(hullDoc);
            Assert.Equal("cp:hull_2", realVm.SelectedAsset?.Id);
            Assert.Contains("cp:hull_2", realVm.ToastMessage, StringComparison.Ordinal);

            realVm.PasteAssetCommand.Execute(hullDoc);
            Assert.Equal("cp:hull_3", realVm.SelectedAsset?.Id);

            // Duplicate -> cp:hull_4
            realVm.DuplicateAssetCommand.Execute(hullDoc);
            Assert.Equal("cp:hull_4", realVm.SelectedAsset?.Id);

            // Material copy/paste creates new file with _2 suffix
            Assert.True(realVm.SelectAsset(AssetKind.Material, "bricks/glossy"));
            var matDoc = realVm.SelectedAsset!;
            realVm.CopyAssetCommand.Execute(matDoc);
            realVm.PasteAssetCommand.Execute(matDoc);
            Assert.Equal("bricks/glossy_2", realVm.SelectedAsset?.Id);
            Assert.True(File.Exists(Path.Combine(packDir, "assets", "materials", "bricks", "glossy_2.toml")));

            // Read-only base brick guard: cannot Duplicate or Paste directly ONTO read-only asset,
            // but Copy FROM base + Paste onto explorer background / group pastes cleanly into pack!
            Assert.True(realVm.SelectAsset(AssetKind.Brick, "wb:core"));
            var baseDoc = realVm.SelectedAsset!;
            Assert.True(baseDoc.IsReadOnly);

            realVm.DuplicateAssetCommand.Execute(baseDoc);
            Assert.Contains("read-only", realVm.ToastMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            realVm.CopyAssetCommand.Execute(baseDoc);
            realVm.PasteAssetCommand.Execute(baseDoc);
            Assert.Contains("read-only", realVm.ToastMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            var bricksGroup = realVm.TreeGroups.First(g => g.Kind == AssetKind.Brick);
            realVm.PasteAssetCommand.Execute(bricksGroup);
            Assert.Equal("wb:core_2", realVm.SelectedAsset?.Id);
            Assert.False(realVm.SelectedAsset!.IsReadOnly);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }

        // Part B: Sample/In-memory service maintains identical _2/_3 conflict allocation
        var sampleService = new SampleStudioWorkspaceService();
        using var sampleVm = new MainWindowViewModel(
            sampleService,
            new StudioGlPreviewController(),
            startOnWelcomeScreen: false,
            enableLiveGlViewport: false);

        Assert.True(sampleVm.SelectAsset(AssetKind.Brick, "wb:thruster"));
        var thrusterDoc = sampleVm.SelectedAsset!;
        sampleVm.CopyAssetCommand.Execute(thrusterDoc);
        Assert.True(sampleVm.CanPasteAsset);

        sampleVm.PasteAssetCommand.Execute(thrusterDoc);
        Assert.Equal("wb:thruster_2", sampleVm.SelectedAsset?.Id);

        sampleVm.DuplicateAssetCommand.Execute(thrusterDoc);
        Assert.Equal("wb:thruster_3", sampleVm.SelectedAsset?.Id);
    }

    /// <summary>
    ///     Items #3 &amp; #6: Verifies Material Sphere/Plane/Cube switcher default &amp; state updates and hidden Orient button for Material/Blueprint.
    /// </summary>
    [AvaloniaFact]
    public void MaterialShapeSwitcher_AndOrientationVisibility_BehavePerAssetKind()
    {
        using var vm = new MainWindowViewModel(
            new SampleStudioWorkspaceService(),
            new StudioGlPreviewController(),
            startOnWelcomeScreen: false,
            enableLiveGlViewport: false);

        // Brick shows Orient button
        Assert.True(vm.SelectAsset(AssetKind.Brick, "wb:thruster"));
        Assert.True(vm.Has3DPreviewControls);
        Assert.True(vm.ShowOrientationButton);
        Assert.False(vm.IsMaterialPreviewSelected);

        // Item shows Orient button
        Assert.True(vm.SelectAsset(AssetKind.Item, "wb:thruster_item"));
        Assert.True(vm.Has3DPreviewControls);
        Assert.True(vm.ShowOrientationButton);
        Assert.False(vm.IsMaterialPreviewSelected);

        // Blueprint hides Orient button while keeping 3D preview controls
        Assert.True(vm.SelectAsset(AssetKind.Blueprint, "ships/scout_mk1.wbp"));
        Assert.True(vm.Has3DPreviewControls);
        Assert.False(vm.ShowOrientationButton);
        Assert.False(vm.IsMaterialPreviewSelected);

        // Material hides Orient button and enables Sphere/Plane/Cube switcher defaulting to Sphere
        Assert.True(vm.SelectAsset(AssetKind.Material, "bricks/glass"));
        Assert.True(vm.Has3DPreviewControls);
        Assert.False(vm.ShowOrientationButton);
        Assert.True(vm.IsMaterialPreviewSelected);
        Assert.Equal(MaterialPreviewShape.Sphere, vm.MaterialPreviewShape);
        Assert.Equal(MaterialPreviewShape.Sphere, vm.ActivePreviewDescriptor.MaterialShape);
        Assert.True(vm.IsMaterialShapeSphere);
        Assert.False(vm.IsMaterialShapePlane);
        Assert.False(vm.IsMaterialShapeCube);

        vm.SetMaterialPreviewShapeCommand.Execute(MaterialPreviewShape.Plane);
        Assert.Equal(MaterialPreviewShape.Plane, vm.MaterialPreviewShape);
        Assert.Equal(MaterialPreviewShape.Plane, vm.ActivePreviewDescriptor.MaterialShape);
        Assert.True(vm.IsMaterialShapePlane);

        vm.SetMaterialPreviewShapeCommand.Execute(MaterialPreviewShape.Cube);
        Assert.Equal(MaterialPreviewShape.Cube, vm.MaterialPreviewShape);
        Assert.Equal(MaterialPreviewShape.Cube, vm.ActivePreviewDescriptor.MaterialShape);
        Assert.True(vm.IsMaterialShapeCube);

        // Verify distinct procedural geometry meshes for Sphere, Plane, and Cube
        VoxelMesh sphereMesh = PreviewGeometryFactory.CreateTexturedSphere();
        VoxelMesh planeMesh = PreviewGeometryFactory.CreateTexturedPlane();
        VoxelMesh cubeMesh = PreviewGeometryFactory.CreateTexturedCube();

        Assert.True(sphereMesh.Vertices.Length > cubeMesh.Vertices.Length);
        Assert.True(planeMesh.Vertices.Length < cubeMesh.Vertices.Length);
        Assert.Equal(4, planeMesh.Vertices.Length);
        Assert.Equal(24, cubeMesh.Vertices.Length);
    }

    /// <summary>
    ///     Item #4: Verifies ContextMenu, MenuFlyoutPresenter, danger items, and explorer/inspector/validation UI mount cleanly across Dark and Light themes.
    /// </summary>
    [AvaloniaFact]
    public void ThemeContextMenuResourcesAndControls_ResolveCleanlyInDarkAndLightThemes()
    {
        using var vm = new MainWindowViewModel(
            new SampleStudioWorkspaceService(),
            new StudioGlPreviewController(),
            startOnWelcomeScreen: false,
            enableLiveGlViewport: false);

        var window = new MainWindow { DataContext = vm, Width = 1280, Height = 800 };
        try
        {
            window.Show();
            string[] requiredBrushKeys =
            [
                "StudioSurfaceElevatedBrush",
                "StudioBorderStrongBrush",
                "StudioTextPrimaryBrush",
                "StudioBorderBrush",
                "StudioDangerBrush",
                "StudioDangerSurfaceBrush",
            ];

            ThemeVariant[] variants = [ThemeVariant.Dark, ThemeVariant.Light];
            var elevatedColors = new List<Color>(2);

            for (var v = 0; v < variants.Length; v++)
            {
                vm.IsDarkTheme = variants[v] == ThemeVariant.Dark;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                for (var k = 0; k < requiredBrushKeys.Length; k++)
                {
                    Assert.True(
                        Application.Current!.TryFindResource(requiredBrushKeys[k], variants[v], out var res),
                        $"Missing theme resource '{requiredBrushKeys[k]}' in {variants[v]} theme.");
                    Assert.IsAssignableFrom<IBrush>(res);
                    if (k == 0 && res is ISolidColorBrush solid)
                    {
                        elevatedColors.Add(solid.Color);
                    }
                }
            }

            Assert.NotEqual(elevatedColors[0], elevatedColors[1]);

            // Verify ContextMenu and MenuFlyoutPresenter exist cleanly with danger menu item in explorer tree
            var explorer = window.GetVisualDescendants().OfType<WorkspaceExplorerView>().First();
            Assert.NotNull(explorer);
            var firstAssetButton = explorer.GetVisualDescendants()
                .OfType<Button>()
                .First(b => b.Classes.Contains("tree-row"));
            Assert.NotNull(firstAssetButton.ContextMenu);
            var headers = firstAssetButton.ContextMenu!.Items.OfType<MenuItem>().Select(m => m.Header?.ToString()).ToList();
            Assert.Contains("Copy", headers);
            Assert.Contains("Paste", headers);
            Assert.Contains("Duplicate", headers);
            Assert.Contains("Rename ID...", headers);
            Assert.Contains("Delete Asset...", headers);
            var deleteItem = firstAssetButton.ContextMenu.Items.OfType<MenuItem>().First(m => m.Header?.ToString() == "Delete Asset...");
            Assert.Contains("danger", deleteItem.Classes);
        }
        finally
        {
            window.Close();
            MainWindowViewModel.ApplyThemeVariant(true);
        }
    }

    /// <summary>
    ///     Item #5: Verifies copying/pasting a skill level curve across skills, single atomic Undo/Redo, and compiled SkillFormView bindings.
    /// </summary>
    [AvaloniaFact]
    public void SkillLevelCurve_CopyPaste_SupportsAtomicUndoRedoAndDiskSave()
    {
        var tempRoot = CreateTempDirectory("skill-curve-cp");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var packDir = Path.Combine(tempRoot, "pack");
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "sk_pack", "sk", "Skill Pack", "Tester").Success);

            File.WriteAllText(
                Path.Combine(packDir, "assets", "skills", "mining.toml"),
                """
                [[Skills]]
                ID = "sk:mining"
                Name = "skill.sk.mining"
                Category = "skill.category.misc"

                [Skills.Levels]
                1 = 100
                2 = 250
                3 = 500
                4 = 900
                """);

            File.WriteAllText(
                Path.Combine(packDir, "assets", "skills", "building.toml"),
                """
                [[Skills]]
                ID = "sk:building"
                Name = "skill.sk.building"
                Category = "skill.category.misc"

                [Skills.Levels]
                1 = 60
                """);

            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            // Copy 4-level curve from sk:mining
            Assert.True(vm.SelectAsset(AssetKind.Skill, "sk:mining"));
            Assert.False(vm.CanPasteSkillLevelCurve);
            vm.CopySkillLevelCurveCommand.Execute(null);
            Assert.NotNull(vm.CopiedSkillLevelCurve);
            Assert.Equal(4, vm.CopiedSkillLevelCurve!.Count);

            // Paste onto sk:building
            Assert.True(vm.SelectAsset(AssetKind.Skill, "sk:building"));
            var buildingDoc = vm.SelectedAsset!;
            Assert.True(vm.CanPasteSkillLevelCurve);
            Assert.Single(buildingDoc.SkillLevels);

            vm.PasteSkillLevelCurveCommand.Execute(null);
            Assert.Equal(4, buildingDoc.SkillLevels.Count);
            Assert.Equal([100, 250, 500, 900], buildingDoc.SkillLevels.Select(r => r.XpRequired).ToArray());
            Assert.Equal(4, buildingDoc.SkillSparklineValues.Count);
            Assert.True(buildingDoc.IsDirty);

            // Single atomic Undo restores 1-level curve and clears dirty flag
            Assert.True(vm.CanUndo);
            vm.UndoCommand.Execute(null);
            Assert.Single(buildingDoc.SkillLevels);
            Assert.Equal(60, buildingDoc.SkillLevels[0].XpRequired);
            Assert.False(buildingDoc.IsDirty);

            // Single atomic Redo restores pasted curve cleanly
            Assert.True(vm.CanRedo);
            vm.RedoCommand.Execute(null);
            Assert.Equal(4, buildingDoc.SkillLevels.Count);
            Assert.True(buildingDoc.IsDirty);

            // Save to disk persists all 4 pasted levels into [Skills.Levels]
            vm.SaveCurrentAssetCommand.Execute(null);
            Assert.False(buildingDoc.IsDirty);
            var savedToml = File.ReadAllText(Path.Combine(packDir, "assets", "skills", "building.toml"));
            Assert.Contains("2 = 250", savedToml, StringComparison.Ordinal);
            Assert.Contains("4 = 900", savedToml, StringComparison.Ordinal);

            // Verify compiled SkillFormView mounts cleanly inside MainWindow without binding errors
            var window = new MainWindow { DataContext = vm, Width = 1280, Height = 800 };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var skillForm = window.GetVisualDescendants().OfType<SkillFormView>().FirstOrDefault();
                Assert.NotNull(skillForm);
            }
            finally
            {
                window.Close();
            }

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Item #7: Verifies live scalar quick fixes with undo, structural disk quick fixes (SKILL001/TAG001/FMT002), and Fix All Safe preserving unrelated dirty tabs.
    /// </summary>
    [AvaloniaFact]
    public void ValidationQuickFixes_AndFixAllSafe_PreserveUnrelatedDirtyDocuments()
    {
        var tempRoot = CreateTempDirectory("validation-fixes");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var packDir = Path.Combine(tempRoot, "pack");
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "vf_pack", "vf", "Fix Pack", "Tester").Success);

            Directory.CreateDirectory(Path.Combine(packDir, "assets", "textures", "items"));
            File.WriteAllBytes(Path.Combine(packDir, "assets", "textures", "items", "badge.png"), [137, 80, 78, 71]);

            // Unrelated clean brick we'll dirty in memory before fixes run
            File.WriteAllText(
                Path.Combine(packDir, "assets", "bricks", "clean_edit.toml"),
                """
                [[Bricks]]
                ID = "vf:clean_edit"
                Shape = "block"
                Color = "#4A6B8A"
                CastShadows = true
                Passable = false

                [Bricks.Textures]
                Default = ["panel"]
                """);

            // Brick with COLOR001 shorthand + CRLF (FMT002)
            File.WriteAllText(
                Path.Combine(packDir, "assets", "bricks", "bad_color.toml"),
                "[[Bricks]]\r\nID = \"vf:bad_color\"\r\nShape = \"block\"\r\nColor = \"#fff\"\r\nCastShadows = true\r\nPassable = false\r\n");

            // Skill missing [Skills.Levels] (SKILL001)
            File.WriteAllText(
                Path.Combine(packDir, "assets", "skills", "missing_levels.toml"),
                "[[Skills]]\nID = \"vf:missing_levels\"\nName = \"skill.vf.missing\"\nCategory = \"skill.category.misc\"\n");

            // Tag with duplicate entry (TAG001 RemoveArrayElement safe bulk fix)
            File.WriteAllText(
                Path.Combine(packDir, "assets", "lang", "tags", "bricks.toml"),
                "[Tags]\n\"brick.paintable\" = [\"vf:clean_edit\", \"vf:clean_edit\"]\n");

            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            Assert.True(vm.SafeFixCount > 0);
            Assert.True(vm.HasSafeFixes);
            Assert.Contains("Fix All Safe (", vm.SafeFixButtonLabel, StringComparison.Ordinal);

            // Make unrelated document dirty before running structural/bulk validation fixes
            Assert.True(vm.SelectAsset(AssetKind.Brick, "vf:clean_edit"));
            var unrelatedDoc = vm.SelectedAsset!;
            unrelatedDoc.BrickColorField!.Value = "#123456";
            Assert.True(unrelatedDoc.IsDirty);

            // Single live scalar fix with Undo on COLOR001 (#fff -> #FFFFFF)
            var colorFinding = vm.AllFindings.First(f => f.Code == "COLOR001" && f.HasQuickFix);
            Assert.Equal(ValidationQuickFixKind.ReplaceValue, colorFinding.QuickFixKind);
            vm.ApplyValidationQuickFixCommand.Execute(colorFinding);
            Assert.Equal("#FFFFFF", vm.SelectedAsset!.BrickColorField!.Value);
            Assert.True(vm.CanUndo);
            vm.UndoCommand.Execute(null);
            Assert.Equal("#fff", vm.SelectedAsset.BrickColorField.Value);

            // Single structural fix SKILL001 (InsertDefaultSkillLevels) updates disk without wiping unrelated dirty tab
            var skillFinding = vm.AllFindings.First(f => f.Code == "SKILL001" && f.HasQuickFix);
            Assert.Equal(ValidationQuickFixKind.InsertDefaultSkillLevels, skillFinding.QuickFixKind);
            vm.ApplyValidationQuickFixCommand.Execute(skillFinding);
            Assert.Contains("[Skills.Levels]", File.ReadAllText(Path.Combine(packDir, "assets", "skills", "missing_levels.toml")), StringComparison.Ordinal);
            Assert.True(unrelatedDoc.IsDirty);
            Assert.Equal("#123456", unrelatedDoc.BrickColorField!.Value);

            // Fix All Safe applies remaining deterministic safe fixes (TAG001, FMT002, COLOR001) & preserves dirty tab
            vm.FixAllSafeIssuesCommand.Execute(null);
            Assert.True(unrelatedDoc.IsDirty);
            Assert.Equal("#123456", unrelatedDoc.BrickColorField.Value);
            Assert.Equal(0, vm.SafeFixCount);
            Assert.False(vm.HasSafeFixes);
            Assert.DoesNotContain("\r\n", File.ReadAllText(Path.Combine(packDir, "assets", "bricks", "bad_color.toml")), StringComparison.Ordinal);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Item #8: Verifies CPU OBJ thumbnail rendering/caching, unindexed .obj candidate discovery, and mesh file picking normalization.
    /// </summary>
    [AvaloniaFact]
    public void MeshThumbnailsAndFilePicker_RenderObjAndNormalizeWorkspacePaths()
    {
        var tempRoot = CreateTempDirectory("mesh-thumbnails");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "data"));
            var packDir = Path.Combine(tempRoot, "pack");
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "ms_pack", "ms", "Mesh Pack", "Tester").Success);

            var propsMeshesDir = Path.Combine(packDir, "assets", "meshes", "props");
            Directory.CreateDirectory(propsMeshesDir);
            var crateObj = Path.Combine(propsMeshesDir, "crate.obj");
            File.WriteAllText(crateObj, SIMPLE_OBJ_TEXT);

            var strayObj = Path.Combine(packDir, "assets", "stray_cone.obj");
            File.WriteAllText(strayObj, SIMPLE_OBJ_TEXT);

            // Verify MeshThumbnailRenderer + ThumbnailCache render and cache OBJ bitmaps cleanly without GL
            using Bitmap? rendered = MeshThumbnailRenderer.TryRenderThumbnail(crateObj, 64);
            Assert.NotNull(rendered);
            Assert.Equal(64, rendered!.PixelSize.Width);
            Assert.Equal(64, rendered.PixelSize.Height);

            Bitmap? cachedFirst = ThumbnailCache.GetOrLoad(crateObj, 64);
            Bitmap? cachedSecond = ThumbnailCache.GetOrLoad(crateObj, 64);
            Assert.NotNull(cachedFirst);
            Assert.Same(cachedFirst, cachedSecond);

            // Brick and Item TOML authoring documents
            File.WriteAllText(
                Path.Combine(packDir, "assets", "bricks", "crate_brick.toml"),
                """
                [[Bricks]]
                ID = "ms:crate_brick"
                Shape = "Custom"
                Color = "#7A8899"
                CastShadows = true
                Passable = false
                Mesh = ""
                """);

            File.WriteAllText(
                Path.Combine(packDir, "assets", "items", "crate_item.toml"),
                """
                [[Items]]
                ID = "ms:crate_item"
                Name = "item.ms.crate"
                MaxStack = 64

                [Items.ViewModel]
                Mesh = ""
                """);

            var fakePicker = new RecordingMeshPathPicker([crateObj, strayObj]);
            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: false,
                pathPicker: fakePicker,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            // BrickMeshField exposes visual mesh picker with ThumbnailPath populated for both crate.obj and stray_cone.obj
            Assert.True(vm.SelectAsset(AssetKind.Brick, "ms:crate_brick"));
            var brickMeshField = vm.SelectedAsset!.BrickMeshField!;
            Assert.True(brickMeshField.IsMeshPickerField);
            Assert.True(brickMeshField.IsVisualPickerField);
            Assert.True(brickMeshField.AllowBrowseMeshFile);
            Assert.True(brickMeshField.AllowBrowseDiskFile);

            var crateCandidate = brickMeshField.FilteredCandidates.First(c => c.Value == "props/crate.obj");
            var strayCandidate = brickMeshField.FilteredCandidates.First(c => c.Value == "stray_cone.obj");
            Assert.Equal(Path.GetFullPath(crateObj), Path.GetFullPath(crateCandidate.ThumbnailPath!));
            Assert.Equal(Path.GetFullPath(strayObj), Path.GetFullPath(strayCandidate.ThumbnailPath!));

            // Browse on BrickMeshField starts in assets/meshes and normalizes path relative to assets/meshes/
            brickMeshField.BrowseDiskFileCommand.Execute(null);
            Assert.Single(fakePicker.RequestedStartDirectories);
            Assert.Equal(
                Path.GetFullPath(Path.Combine(packDir, "assets", "meshes")),
                Path.GetFullPath(fakePicker.RequestedStartDirectories[0]!));
            Assert.Equal("props/crate.obj", brickMeshField.Value);

            // Second browse on ItemViewModelMeshField remembers propsMeshesDir and resolves assets/stray_cone.obj -> stray_cone.obj
            Assert.True(vm.SelectAsset(AssetKind.Item, "ms:crate_item"));
            var itemVmMesh = vm.SelectedAsset!.ItemViewModelMeshField!;
            Assert.True(itemVmMesh.IsMeshPickerField);
            Assert.True(itemVmMesh.AllowBrowseMeshFile);
            itemVmMesh.BrowseDiskFileCommand.Execute(null);
            Assert.Equal(2, fakePicker.RequestedStartDirectories.Count);
            Assert.Equal(Path.GetFullPath(propsMeshesDir), Path.GetFullPath(fakePicker.RequestedStartDirectories[1]!));
            Assert.Equal("stray_cone.obj", itemVmMesh.Value);

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    private static MainWindowViewModel CreateViewModelWithReferenceRoot(
        AssetToolsStudioWorkspaceService service,
        string packDir,
        string referenceInstallDir)
    {
        return new MainWindowViewModel(
            service,
            new StudioGlPreviewController(),
            startOnWelcomeScreen: false,
            enableLiveGlViewport: false,
            initialWorkspacePath: packDir,
            initialReferenceInstallPath: referenceInstallDir);
    }

    private static ToolsSettings.ToolDataPaths CreateIsolatedDataPaths(string rootDir)
    {
        Directory.CreateDirectory(rootDir);
        File.WriteAllText(Path.Combine(rootDir, ToolsSettings.ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);
        return ToolsSettings.ToolDataPaths.Resolve(executableDirectory: rootDir);
    }

    private static string CreateTempDirectory(string label)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wb-stage2-{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupTempDirectory(string path)
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
            // Ignore transient locks during test cleanup.
        }
    }

    private sealed class RecordingMeshPathPicker(IReadOnlyList<string> results) : IWorkspacePathPicker
    {
        private int _index;

        public List<string?> RequestedStartDirectories { get; } = [];

        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);

        public Task<string?> PickBlueprintFileAsync(string title) => Task.FromResult<string?>(null);

        public Task<string?> PickImageFileAsync(string title, string? suggestedStartDirectory = null) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickMeshFileAsync(string title, string? suggestedStartDirectory = null)
        {
            RequestedStartDirectories.Add(suggestedStartDirectory);
            if (_index < results.Count)
            {
                return Task.FromResult<string?>(results[_index++]);
            }

            return Task.FromResult<string?>(null);
        }
    }
}
