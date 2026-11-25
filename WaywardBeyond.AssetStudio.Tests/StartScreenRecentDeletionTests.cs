using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.AssetStudio.ViewModels;
using WaywardBeyond.AssetStudio.Views;
using ToolsSettings = WaywardBeyond.AssetTools.Settings;
using Xunit;

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>
///     Verifies start screen recent workspace deletion prompts, safe recursive disk deletion, and previews.
/// </summary>
public sealed class StartScreenRecentDeletionTests
{
    /// <summary>
    ///     Verifies removing a recent workspace keeps files on disk when the confirmation checkbox stays unchecked.
    /// </summary>
    [Fact]
    public void RemoveRecent_DefaultUnchecked_RemovesFromRecentsOnlyAndPreservesFilesOnDisk()
    {
        var tempRoot = CreateTempDirectory("keep-disk");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "studio-data"));
            var packDir = Path.Combine(tempRoot, "pack_keep");
            var nestedFile = Path.Combine(packDir, "notes", "readme.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(nestedFile)!);
            File.WriteAllText(nestedFile, "keep me");

            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "pack_keep", "keep", "Keep Pack", "Tester").Success);
            service.OpenWorkspace(Models.WorkspaceMode.Pack, packDir);

            using var vm = new MainWindowViewModel(service, new StudioGlPreviewController(), startOnWelcomeScreen: true);
            Assert.Single(vm.StartScreen.RecentWorkspaces);
            var entry = vm.StartScreen.RecentWorkspaces[0];

            vm.StartScreen.RequestDeleteRecentCommand.Execute(entry);
            Assert.NotNull(vm.ActiveDeleteRecentWorkspaceDialog);
            Assert.True(vm.IsAnyOverlayOpen);
            Assert.False(vm.ActiveDeleteRecentWorkspaceDialog!.DeleteFromDisk);
            Assert.Equal("Remove from Recents", vm.ActiveDeleteRecentWorkspaceDialog.ConfirmButtonLabel);

            vm.ActiveDeleteRecentWorkspaceDialog.CancelCommand.Execute(null);
            Assert.Null(vm.ActiveDeleteRecentWorkspaceDialog);
            Assert.Single(vm.StartScreen.RecentWorkspaces);
            Assert.True(File.Exists(nestedFile));

            vm.StartScreen.RequestDeleteRecentCommand.Execute(entry);
            vm.ActiveDeleteRecentWorkspaceDialog!.ConfirmCommand.Execute(null);

            Assert.Null(vm.ActiveDeleteRecentWorkspaceDialog);
            Assert.Empty(vm.StartScreen.RecentWorkspaces);
            Assert.True(vm.StartScreen.HasNoRecentWorkspaces);
            Assert.True(Directory.Exists(packDir));
            Assert.True(File.Exists(nestedFile));

            var reloadedService = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.Empty(reloadedService.GetRecentWorkspaces());
            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Verifies checking disk deletion recursively removes subfolders and read-only files alongside the recent entry.
    /// </summary>
    [Fact]
    public void RemoveRecent_DeleteFromDiskChecked_RecursivelyDeletesFolderReadOnlyFilesAndSettingsEntry()
    {
        var tempRoot = CreateTempDirectory("delete-disk");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "studio-data"));
            var packDir = Path.Combine(tempRoot, "pack_wipe");
            var outsideDir = Path.Combine(tempRoot, "outside_keep");
            var outsideFile = Path.Combine(outsideDir, "important.txt");
            Directory.CreateDirectory(outsideDir);
            File.WriteAllText(outsideFile, "must stay intact");

            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.True(service.CreatePackWorkspace(packDir, "pack_wipe", "wipe", "Wipe Pack", "Tester").Success);

            var readOnlySubDir = Path.Combine(packDir, "nested", "locked");
            Directory.CreateDirectory(readOnlySubDir);
            var readOnlyFile = Path.Combine(readOnlySubDir, "locked.txt");
            File.WriteAllText(readOnlyFile, "readonly content");
            File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

            var linkedDirPath = Path.Combine(packDir, "external_link");
            Directory.CreateSymbolicLink(linkedDirPath, outsideDir);

            using var vm = new MainWindowViewModel(
                service,
                new StudioGlPreviewController(),
                startOnWelcomeScreen: true,
                initialWorkspacePath: packDir);
            vm.ShowStartScreenCommand.Execute(null);
            Assert.Single(vm.StartScreen.RecentWorkspaces);
            var entry = vm.StartScreen.RecentWorkspaces[0];

            vm.StartScreen.RequestDeleteRecentCommand.Execute(entry);
            var dialog = vm.ActiveDeleteRecentWorkspaceDialog;
            Assert.NotNull(dialog);
            Assert.False(dialog!.DeleteFromDisk);

            dialog.DeleteFromDisk = true;
            Assert.Equal("Delete from Disk", dialog.ConfirmButtonLabel);
            Assert.Contains("permanently deletes", dialog.ActionSummary, StringComparison.OrdinalIgnoreCase);

            dialog.ConfirmCommand.Execute(null);

            Assert.Null(vm.ActiveDeleteRecentWorkspaceDialog);
            Assert.Empty(vm.StartScreen.RecentWorkspaces);
            Assert.True(vm.StartScreen.HasNoRecentWorkspaces);
            Assert.False(Directory.Exists(packDir));
            Assert.True(File.Exists(outsideFile));
            Assert.Equal(string.Empty, vm.CurrentWorkspaceRootPath);

            var reloadedService = new AssetToolsStudioWorkspaceService(dataPaths);
            Assert.Empty(reloadedService.GetRecentWorkspaces());
            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Verifies protected user/home/root/system paths cannot be recursively deleted from disk.
    /// </summary>
    [Fact]
    public void RemoveRecent_UnsafeProtectedTarget_DisablesDiskDeleteAndRefusesDeletion()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.True(AssetToolsStudioWorkspaceService.IsUnsafeDiskDeleteTarget(homeDir));
        Assert.True(AssetToolsStudioWorkspaceService.IsUnsafeDiskDeleteTarget(Path.GetPathRoot(Environment.CurrentDirectory)));
        Assert.True(AssetToolsStudioWorkspaceService.IsUnsafeDiskDeleteTarget(Path.GetTempPath()));

        var tempRoot = CreateTempDirectory("unsafe-guard");
        try
        {
            var dataPaths = CreateIsolatedDataPaths(Path.Combine(tempRoot, "studio-data"));
            var service = new AssetToolsStudioWorkspaceService(dataPaths);
            var blocked = service.RemoveRecentWorkspace(homeDir, deleteFromDisk: true);
            Assert.False(blocked.Success);
            Assert.True(Directory.Exists(homeDir));
            service.MarkCleanShutdown();
        }
        finally
        {
            CleanupTempDirectory(tempRoot);
        }
    }

    /// <summary>
    ///     Renders and verifies dark/light start screen delete buttons and both dialog states as PNG previews.
    /// </summary>
    [AvaloniaFact]
    public void CaptureStartScreenRecentDeleteAndDialogStates_DarkAndLight()
    {
        using var vm = new MainWindowViewModel(
            new SampleStudioWorkspaceService(),
            new SamplePreviewSurfaceProvider(),
            startOnWelcomeScreen: true);

        var window = new MainWindow { DataContext = vm };
        try
        {
            vm.IsDarkTheme = true;
            vm.IsStartScreenVisible = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(File.Exists(HeadlessRenderHelper.CaptureWindowPng(window, "33-start-screen-recent-delete.png")));

            vm.StartScreen.RequestDeleteRecentCommand.Execute(vm.StartScreen.RecentWorkspaces[0]);
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(vm.ActiveDeleteRecentWorkspaceDialog);
            Assert.False(vm.ActiveDeleteRecentWorkspaceDialog!.DeleteFromDisk);
            Assert.True(File.Exists(HeadlessRenderHelper.CaptureWindowPng(window, "33b-delete-recent-dialog-default.png")));

            vm.ActiveDeleteRecentWorkspaceDialog.DeleteFromDisk = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(File.Exists(HeadlessRenderHelper.CaptureWindowPng(window, "33c-delete-recent-dialog-disk-warning.png")));

            vm.IsDarkTheme = false;
            Dispatcher.UIThread.RunJobs();
            Assert.True(File.Exists(HeadlessRenderHelper.CaptureWindowPng(window, "33d-delete-recent-dialog-light.png")));

            vm.ActiveDeleteRecentWorkspaceDialog.ConfirmCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, vm.StartScreen.RecentWorkspaces.Count);
        }
        finally
        {
            window.Close();
        }
    }

    private static ToolsSettings.ToolDataPaths CreateIsolatedDataPaths(string rootDir)
    {
        Directory.CreateDirectory(rootDir);
        File.WriteAllText(Path.Combine(rootDir, ToolsSettings.ToolDataPaths.PORTABLE_MARKER_FILE), string.Empty);
        return ToolsSettings.ToolDataPaths.Resolve(executableDirectory: rootDir);
    }

    private static string CreateTempDirectory(string label)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wb-recent-del-{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Ignore transient cleanup locks.
        }
    }
}
