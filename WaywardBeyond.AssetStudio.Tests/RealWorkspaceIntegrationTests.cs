using System.IO.Compression;
using Avalonia.Headless.XUnit;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.AssetStudio.ViewModels;
using WaywardBeyond.AssetStudio.Views;
using ToolsSettings = WaywardBeyond.AssetTools.Settings;
using Xunit;

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>
///     Verifies full end-to-end integration between Asset Studio views, ViewModels, and the real AssetTools backend.
/// </summary>
public sealed class RealWorkspaceIntegrationTests
{
    /// <summary>
    ///     Loads the actual Wayward Beyond repository checkout and verifies all shipped kinds, clean validation, search, and diagnostics.
    /// </summary>
    [AvaloniaFact]
    public void RealRepo_LoadsAllShippedKinds_ValidatesClean_AndSearchesViaCommandPalette()
    {
        var repoRoot = FindRepoRoot();
        var tempDataRoot = CreateTempDirectory("real-repo-int");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(tempDataRoot);
            using var logSession = new StudioLogSession(Path.Combine(tempDataRoot, "logs"), ToolsSettings.ToolLogLevel.Information);
            var service = new AssetToolsStudioWorkspaceService(dataPaths, logSession);
            var previewController = new StudioGlPreviewController();

            using var vm = new MainWindowViewModel(
                service,
                previewController,
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: repoRoot);

            Assert.Equal(WorkspaceMode.Repo, vm.WorkspaceMode);
            Assert.False(vm.IsStartScreenVisible);
            Assert.Equal(0, vm.FatalCount);
            Assert.Equal(0, vm.BrokenCount);

            // Verify shipped assets across every kind exist and select cleanly
            Assert.True(vm.SelectAssetById("wb:core"));
            Assert.Equal(AssetKind.Brick, vm.SelectedAsset!.Kind);
            Assert.Equal("core", vm.SelectedAsset.BrickTexDefaultField!.Value);
            Assert.NotEmpty(vm.SelectedAsset.InboundReferences);

            Assert.True(vm.SelectAssetById("wb:thruster"));
            Assert.Equal("thruster.obj", vm.SelectedAsset.BrickMeshField!.Value);
            Assert.Equal(2, vm.SelectedAsset.Colliders.Count);

            Assert.True(vm.SelectAssetById("wb:drill"));
            Assert.Equal(AssetKind.Item, vm.SelectedAsset.Kind);

            Assert.True(vm.SelectAssetById("wb:red"));
            Assert.Equal(AssetKind.Paint, vm.SelectedAsset.Kind);

            Assert.True(vm.SelectAssetById("wb:wearables"));
            Assert.Equal(AssetKind.SlotGroup, vm.SelectedAsset.Kind);
            Assert.Equal(6, vm.SelectedAsset.SlotGroupCapacityValue);
            Assert.Single(vm.SelectedAsset.SlotGroupAllowedTags);
            Assert.Equal(6, vm.SelectedAsset.SlotGroupSlots.Count);
            vm.NavigateTagTargetCommand.Execute("item.wearable.helmet");
            Assert.Equal(AssetKind.Tag, vm.SelectedAsset.Kind);
            Assert.Equal("item.wearable.helmet", vm.SelectedAsset.Id);
            Assert.Contains(vm.SelectedAsset.InboundReferences, static r => r.SourceKind == AssetKind.SlotGroup && r.SourceAssetId == "wb:wearables");

            Assert.True(vm.SelectAssetById("building"));
            Assert.Equal(AssetKind.Skill, vm.SelectedAsset.Kind);
            Assert.Equal(100, vm.SelectedAsset.SkillLevels.Count);
            Assert.NotEmpty(vm.SelectedAsset.SkillSources);

            Assert.True(vm.SelectAssetById("wb:human_m"));
            Assert.Equal(AssetKind.Body, vm.SelectedAsset.Kind);
            Assert.False(string.IsNullOrWhiteSpace(vm.SelectedAsset.ResolvedBodyFrontTexturePath));
            Assert.True(File.Exists(vm.SelectedAsset.ResolvedBodyFrontTexturePath));

            Assert.True(vm.SelectAssetById("bricks/glass"));
            Assert.Equal(AssetKind.Material, vm.SelectedAsset.Kind);

            Assert.Contains(vm.AllDocuments, static d => d.Kind == AssetKind.Tag);
            Assert.Contains(vm.AllDocuments, static d => d.Kind == AssetKind.Localization);

            // Verify NavigationHistory back/forward across real assets
            Assert.True(vm.CanNavigateBack);
            vm.NavigateBackCommand.Execute(null);
            Assert.Equal("wb:human_m", vm.SelectedAsset.Id);
            Assert.True(vm.CanNavigateForward);
            vm.NavigateForwardCommand.Execute(null);
            Assert.Equal("bricks/glass", vm.SelectedAsset.Id);

            // Verify real CommandPaletteSearchIndex ranking on real repo assets
            vm.OpenCommandPaletteCommand.Execute(null);
            Assert.NotNull(vm.ActiveCommandPalette);
            vm.ActiveCommandPalette!.Query = "thruster";
            Assert.NotEmpty(vm.ActiveCommandPalette.Results);
            Assert.Contains(vm.ActiveCommandPalette.Results, static r => string.Equals(r.Title, "wb:thruster", StringComparison.OrdinalIgnoreCase));

            vm.ActiveCommandPalette.Query = "building";
            Assert.Contains(vm.ActiveCommandPalette.Results, static r => string.Equals(r.Title, "building", StringComparison.OrdinalIgnoreCase));
            vm.CloseAllOverlaysCommand.Execute(null);

            // Verify DiagnosticsBundleWriter excludes content files and writes sanitized zip
            vm.OpenAboutDialogCommand.Execute(null);
            Assert.NotNull(vm.ActiveAboutDialog);
            var diagResult = service.CopyDiagnosticsZip(vm.ActiveAboutDialog!.Info);
            Assert.True(diagResult.Success, diagResult.Message);
            Assert.False(string.IsNullOrWhiteSpace(diagResult.Detail));
            Assert.True(File.Exists(diagResult.Detail));

            using (var zip = ZipFile.OpenRead(diagResult.Detail!))
            {
                var names = zip.Entries.Select(static e => e.FullName).ToHashSet(StringComparer.Ordinal);
                Assert.Contains("about.json", names);
                Assert.Contains("log-tail.txt", names);
                Assert.Contains("validation-summary.txt", names);
                Assert.Contains("recent-errors.txt", names);
                Assert.DoesNotContain(names, static n => n.EndsWith(".toml", StringComparison.OrdinalIgnoreCase));
            }

            service.MarkCleanShutdown();
            Assert.False(File.Exists(service.DraftStore.SessionMarkerPath));
        }
        finally
        {
            CleanupTempDirectory(tempDataRoot);
        }
    }

    /// <summary>
    ///     Verifies editing and atomically saving a temp pack copy preserves untouched comments and formatting byte-for-byte.
    /// </summary>
    [AvaloniaFact]
    public void TempPackCopy_EditAndSave_PreservesCommentsAndUntouchedLinesByteForByte()
    {
        var tempRoot = CreateTempDirectory("lossless-save");
        try
        {
            var dataDir = Path.Combine(tempRoot, "tool-data");
            var packDir = Path.Combine(tempRoot, "my_test_pack");
            var dataPaths = CreateIsolatedDataPaths(dataDir);
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            var preview = new StudioGlPreviewController();

            var created = service.CreatePackWorkspace(packDir, "my_test_pack", "testpack", "My Test Pack", "Tester");
            Assert.True(created.Success, created.Message);

            var brickPath = Path.Combine(packDir, "assets", "bricks", "core.toml");
            const string originalContent =
                "# Preserve this header comment byte-for-byte\n" +
                "[[Bricks]]\n" +
                "ID          = \"testpack:core\"\n" +
                "Shape       = \"block\" # inline shape comment\n" +
                "CastShadows = true\n" +
                "\n" +
                "# Sound bindings comment\n" +
                "[Bricks.Sounds]\n" +
                "Place = [\"metal_place\"]\n" +
                "Break = [\"metal_break\"]\n" +
                "\n" +
                "[Bricks.Textures]\n" +
                "Default = [\"core\"]\n";
            File.WriteAllText(brickPath, originalContent);

            using var vm = new MainWindowViewModel(
                service,
                preview,
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: packDir);

            Assert.True(vm.SelectAssetById("testpack:core"));
            Assert.Equal(0, vm.DirtyCount);

            // Edit a single scalar field -> draft is written immediately and TOML preserves comments
            vm.SelectedAsset!.BrickCastShadowsField!.BoolValue = false;
            Assert.Equal(1, vm.DirtyCount);
            Assert.Contains("# Preserve this header comment byte-for-byte", vm.SelectedAsset.TomlText, StringComparison.Ordinal);
            Assert.Contains("# inline shape comment", vm.SelectedAsset.TomlText, StringComparison.Ordinal);
            Assert.True(service.DraftStore.LoadDraft(brickPath).Success);

            // Save All commits atomically and clears the draft
            vm.SaveAllCommand.Execute(null);
            Assert.Equal(0, vm.DirtyCount);
            Assert.False(service.DraftStore.LoadDraft(brickPath).Success);

            var savedText = File.ReadAllText(brickPath);
            var expectedText = originalContent.Replace("CastShadows = true", "CastShadows = false", StringComparison.Ordinal);
            Assert.Equal(expectedText, savedText);

            // Verify RenamePlanner preview finds real occurrences in writable pack
            var renameSpans = service.PreviewRename("testpack:core", "testpack:core_v2", AssetKind.Brick);
            Assert.NotEmpty(renameSpans);
            Assert.Contains(renameSpans, static s => s.BeforeText.Contains("testpack:core", StringComparison.Ordinal));

            // Verify PackExporter blocks when standalone pack has unresolved resource references
            var blockedExport = service.ExportPack(new ExportPackRequest("my_test_pack", 1, ".tar.gz", AcknowledgeWarnings: true));
            Assert.False(blockedExport.Success);
            Assert.Contains("blocked", blockedExport.Message, StringComparison.OrdinalIgnoreCase);

            // Re-open the same pack layered over the real repo reference roots -> 0 broken -> export succeeds
            var layered = service.TryOpenAuto(packDir, FindRepoRoot());
            Assert.NotNull(layered);
            var exportResult = service.ExportPack(new ExportPackRequest("my_test_pack", 1, ".tar.gz", AcknowledgeWarnings: true));
            Assert.True(exportResult.Success, exportResult.Message);
            Assert.False(string.IsNullOrWhiteSpace(exportResult.Detail));
            Assert.True(File.Exists(exportResult.Detail));

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Renders 1280x800 visual verification PNGs of real shipped Wayward Beyond repository assets in Dark and Light themes.
    /// </summary>
    [AvaloniaFact]
    public void CaptureRealRepoWorkbenches_DarkCoreAndLightBuilding_ProduceCrispScreenshots()
    {
        var repoRoot = FindRepoRoot();
        var tempDataRoot = CreateTempDirectory("real-repo-capture");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(tempDataRoot);
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            var preview = new StudioGlPreviewController();

            // 11. Dark theme — real repo wb:core selected
            using var darkVm = new MainWindowViewModel(
                service,
                preview,
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: repoRoot);
            darkVm.IsDarkTheme = true;
            Assert.True(darkVm.SelectAssetById("wb:core"));

            var darkWindow = new MainWindow { DataContext = darkVm };
            try
            {
                var p11 = HeadlessRenderHelper.CaptureWindowPng(darkWindow, "11-real-repo-brick-core.png");
                Assert.True(File.Exists(p11));
            }
            finally
            {
                darkWindow.Close();
            }

            // 12. Light theme — real repo building skill (100-level sparkline + XP sources)
            using var lightVm = new MainWindowViewModel(
                service,
                preview,
                startOnWelcomeScreen: false,
                enableLiveGlViewport: false,
                initialWorkspacePath: repoRoot);
            lightVm.IsDarkTheme = false;
            Assert.True(lightVm.SelectAssetById("building"));

            var lightWindow = new MainWindow { DataContext = lightVm };
            try
            {
                var p12 = HeadlessRenderHelper.CaptureWindowPng(lightWindow, "12-real-repo-workbench-light.png");
                Assert.True(File.Exists(p12));
            }
            finally
            {
                lightWindow.Close();
                MainWindowViewModel.ApplyThemeVariant(true);
            }

            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempDataRoot);
        }
    }

    private static ToolsSettings.ToolDataPaths CreateIsolatedDataPaths(string rootDir)
    {
        Directory.CreateDirectory(rootDir);
        File.WriteAllText(Path.Combine(rootDir, ToolsSettings.ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);
        return ToolsSettings.ToolDataPaths.Resolve(executableDirectory: rootDir);
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

    private static string CreateTempDirectory(string label)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wb-asset-studio-{label}-{Guid.NewGuid():N}");
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
            // Ignore transient file locks during temp cleanup.
        }
    }
}
