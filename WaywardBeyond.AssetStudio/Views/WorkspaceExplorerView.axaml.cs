using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WaywardBeyond.AssetStudio.ViewModels;

namespace WaywardBeyond.AssetStudio.Views;

/// <summary>
///     Left workspace explorer sidebar listing grouped assets with provenance, context menu, delete key, and auto-scroll.
/// </summary>
public sealed partial class WorkspaceExplorerView : UserControl
{
    private MainWindowViewModel? _boundViewModel;

    /// <summary>
    ///     Initializes the workspace explorer view and wires selection scroll and delete key handlers.
    /// </summary>
    public WorkspaceExplorerView()
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChanged;
        Loaded += HandleLoaded;
        AddHandler(KeyDownEvent, HandleExplorerKeyDown, RoutingStrategies.Tunnel);
    }

    private void HandleLoaded(object? sender, RoutedEventArgs e)
    {
        if (_boundViewModel?.SelectedAsset != null)
        {
            HandleSelectionScrollRequested(_boundViewModel.SelectedAsset);
        }
    }

    private void HandleDataContextChanged(object? sender, EventArgs e)
    {
        if (_boundViewModel != null)
        {
            _boundViewModel.SelectionScrollRequested -= HandleSelectionScrollRequested;
        }

        _boundViewModel = DataContext as MainWindowViewModel;
        if (_boundViewModel != null)
        {
            _boundViewModel.SelectionScrollRequested += HandleSelectionScrollRequested;
        }
    }

    private void HandleSelectionScrollRequested(AssetDocumentViewModel target)
    {
        if (_boundViewModel == null)
        {
            return;
        }

        for (var i = 0; i < _boundViewModel.TreeGroups.Count; i++)
        {
            var group = _boundViewModel.TreeGroups[i];
            if (group.Items.Contains(target))
            {
                group.IsExpanded = true;
                break;
            }
        }

        Dispatcher.UIThread.Post(() => BringSelectedRowIntoView(target), DispatcherPriority.Loaded);
    }

    private void BringSelectedRowIntoView(AssetDocumentViewModel target)
    {
        foreach (var visual in this.GetVisualDescendants())
        {
            if (visual is Button button && ReferenceEquals(button.DataContext, target))
            {
                button.BringIntoView();
                return;
            }
        }
    }

    private void HandleExplorerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.Source is TextBox || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel vm
            || !vm.IsWorkbenchVisible
            || vm.IsAnyOverlayOpen
            || vm.SelectedAsset == null)
        {
            return;
        }

        vm.RequestDeleteAssetCommand.Execute(vm.SelectedAsset);
        e.Handled = true;
    }
}
