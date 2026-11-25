using System;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.AssetStudio.ViewModels;

namespace WaywardBeyond.AssetStudio.Views;

/// <summary>Full-workbench visual editor view wiring live GL and fallback 3D viewports to <see cref="ItemModelEditorViewModel"/>.</summary>
public sealed partial class ItemModelEditorView : UserControl
{
    private ItemModelEditorViewModel? _boundVm;

    /// <summary>Initializes the visual model editor view and subscribes viewport gizmo events.</summary>
    public ItemModelEditorView()
    {
        InitializeComponent();

        LiveModelViewport.GlInitialized += HandleGlInitialized;
        LiveModelViewport.GlErrorOccurred += HandleGlError;
        LiveModelViewport.FrameRendered += HandleGlFrameRendered;
        LiveModelViewport.GizmoDragStarted += HandleGizmoDragStarted;
        LiveModelViewport.TransformDragged += HandleTransformDragged;
        LiveModelViewport.GizmoDragEnded += HandleGizmoDragEnded;

        FallbackModelViewport.GizmoDragStarted += HandleGizmoDragStarted;
        FallbackModelViewport.TransformDragged += HandleTransformDragged;
        FallbackModelViewport.GizmoDragEnded += HandleGizmoDragEnded;

        DataContextChanged += HandleDataContextChanged;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible && _boundVm != null)
        {
            if (LiveModelViewport.IsGlInitialized && _boundVm.ShowLiveGlViewport)
            {
                _boundVm.ReportGlInitialized();
            }

            LiveModelViewport.EnsureRenderedForVisibleSession();
        }
    }

    private void HandleDataContextChanged(object? sender, EventArgs e)
    {
        if (_boundVm != null)
        {
            _boundVm.RenderStateChanged -= SyncViewports;
            _boundVm.ResetCameraRequested -= HandleResetCameraRequested;
        }

        _boundVm = DataContext as ItemModelEditorViewModel;
        if (_boundVm != null)
        {
            _boundVm.RenderStateChanged += SyncViewports;
            _boundVm.ResetCameraRequested += HandleResetCameraRequested;
            LiveModelViewport.ResetCameraForNewSession();
            FallbackModelViewport.ResetCameraForNewSession();
            if (LiveModelViewport.IsGlInitialized && _boundVm.ShowLiveGlViewport)
            {
                _boundVm.ReportGlInitialized();
            }

            LiveModelViewport.SetAlbedoArray(_boundVm.ActiveAlbedoArray);
            SyncViewports();
            LiveModelViewport.EnsureRenderedForVisibleSession();
        }
    }

    private void HandleGlInitialized(GlInfo _)
    {
        if (_boundVm != null)
        {
            _boundVm.ReportGlInitialized();
            LiveModelViewport.SetAlbedoArray(_boundVm.ActiveAlbedoArray);
            SyncViewports();
        }
    }

    private void HandleGlFrameRendered()
    {
        if (_boundVm != null && _boundVm.ShowLiveGlViewport && !_boundVm.IsLiveGlViewportActive)
        {
            _boundVm.ReportGlInitialized();
        }
    }

    private void HandleGlError(string error)
    {
        Trace.TraceWarning($"ItemModelEditorView OpenGL error: {error}");
        _boundVm?.ReportGlFailed();
    }

    private void HandleGizmoDragStarted()
    {
        _boundVm?.BeginGizmoDrag();
    }

    private void HandleTransformDragged(Vector3 pos, Vector3 rot, Vector3 scale, string? readout)
    {
        _boundVm?.ApplyGizmoDragTransform(pos, rot, scale, readout);
    }

    private void HandleGizmoDragEnded(Vector3 pos, Vector3 rot, Vector3 scale)
    {
        _boundVm?.EndGizmoDrag(pos, rot, scale);
    }

    private void HandleResetCameraRequested()
    {
        LiveModelViewport.ResetOrbitCamera();
        FallbackModelViewport.ResetOrbitCamera();
    }

    private void SyncViewports()
    {
        if (_boundVm == null)
        {
            return;
        }

        ModelEditorRenderState state = _boundVm.CurrentRenderState;
        LiveModelViewport.UpdateEditorState(in state);
        FallbackModelViewport.UpdateEditorState(in state);
    }
}
