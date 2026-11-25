using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.AssetStudio.ViewModels;
using WaywardBeyond.AssetStudio.Views;

namespace WaywardBeyond.AssetStudio;

/// <summary>
///     Avalonia application entry configuring themes, resources, real backend services, and the main window.
/// </summary>
public sealed class App : Application
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var startup = AppStartupContext.Current;
            var workspaceService = new AssetToolsStudioWorkspaceService(startup?.DataPaths, startup?.LogSession);
            var glPreviewController = new StudioGlPreviewController();

            MainWindow? mainWindow = null;
            var pathPicker = new DesktopWorkspacePathPicker(() => mainWindow != null ? TopLevel.GetTopLevel(mainWindow) : null);

            var startOnWelcome = string.IsNullOrWhiteSpace(startup?.InitialWorkspacePath);
            var viewModel = new MainWindowViewModel(
                workspaceService,
                glPreviewController,
                startOnWelcomeScreen: startOnWelcome,
                pathPicker: pathPicker,
                enableLiveGlViewport: true,
                initialWorkspacePath: startup?.InitialWorkspacePath);

            mainWindow = new MainWindow
            {
                DataContext = viewModel,
                WindowState = WindowState.Maximized,
            };

            desktop.MainWindow = mainWindow;
            desktop.ShutdownRequested += (_, _) => workspaceService.MarkCleanShutdown();
            desktop.Exit += (_, _) =>
            {
                viewModel.Dispose();
                workspaceService.MarkCleanShutdown();
                startup?.LogSession?.Flush();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
