using Avalonia.Controls;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.AssetStudio.ViewModels;

namespace WaywardBeyond.AssetStudio.Views;

/// <summary>
///     Right inspector sidebar hosting the live preview viewport and clickable inbound references.
/// </summary>
public sealed partial class InspectorPaneView : UserControl
{
    private MainWindowViewModel? _boundViewModel;

    /// <summary>
    ///     Initializes the inspector pane view and wires <see cref="GlViewport"/> lifecycle events.
    /// </summary>
    public InspectorPaneView()
    {
        InitializeComponent();
        LiveGlViewport.GlInitialized += HandleGlInitialized;
        LiveGlViewport.GlErrorOccurred += HandleGlError;
        DataContextChanged += HandleDataContextChanged;
    }

    private void HandleDataContextChanged(object? sender, EventArgs e)
    {
        if (_boundViewModel != null)
        {
            _boundViewModel.PreviewSceneSyncRequested -= HandlePreviewSyncRequested;
            _boundViewModel.PreviewCameraResetRequested -= HandlePreviewCameraResetRequested;
        }

        _boundViewModel = DataContext as MainWindowViewModel;
        if (_boundViewModel != null)
        {
            _boundViewModel.PreviewSceneSyncRequested += HandlePreviewSyncRequested;
            _boundViewModel.PreviewCameraResetRequested += HandlePreviewCameraResetRequested;
            _boundViewModel.SyncLiveGlViewport(LiveGlViewport);
        }
    }

    private void HandleGlInitialized(GlInfo info)
    {
        _boundViewModel?.HandleGlViewportInitialized(info);
    }

    private void HandleGlError(string errorMessage)
    {
        _boundViewModel?.HandleGlViewportError(errorMessage);
    }

    private void HandlePreviewSyncRequested()
    {
        _boundViewModel?.SyncLiveGlViewport(LiveGlViewport);
    }

    private void HandlePreviewCameraResetRequested()
    {
        LiveGlViewport.ResetCamera();
        LiveGlViewport.SetAutoOrbit(_boundViewModel != null && _boundViewModel.IsPreviewOrbiting && _boundViewModel.IsLiveGlViewportActive);
        FallbackPreviewViewport.ResetInteractiveView();
    }
}
