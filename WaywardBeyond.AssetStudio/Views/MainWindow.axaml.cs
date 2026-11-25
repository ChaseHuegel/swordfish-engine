using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.AssetStudio.ViewModels;

namespace WaywardBeyond.AssetStudio.Views;

/// <summary>
///     Primary Avalonia desktop shell window hosting the start screen, workbench, shortcuts, and dialogs.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    ///     Initializes the main studio window and wires tunnel keyboard shortcuts.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, HandleWindowPreviewKeyDown, RoutingStrategies.Tunnel);
        Closed += HandleWindowClosed;
    }

    private void HandleWindowPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (e.Key == Key.F1 && e.KeyModifiers == KeyModifiers.None)
        {
            if (vm.IsWorkbenchVisible)
            {
                vm.OpenCommandPaletteCommand.Execute(null);
                e.Handled = true;
            }

            return;
        }

        if (vm.IsWorkbenchVisible
            && !vm.IsAnyOverlayOpen
            && vm.IsModelEditorVisible
            && vm.ActiveModelEditor is ItemModelEditorViewModel editor
            && e.KeyModifiers == KeyModifiers.None
            && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not TextBox)
        {
            switch (e.Key)
            {
                case Key.W:
                    editor.SetGizmoModeCommand.Execute(TransformGizmoMode.Translate);
                    e.Handled = true;
                    return;
                case Key.E:
                    editor.SetGizmoModeCommand.Execute(TransformGizmoMode.Rotate);
                    e.Handled = true;
                    return;
                case Key.R:
                    editor.SetGizmoModeCommand.Execute(TransformGizmoMode.Scale);
                    e.Handled = true;
                    return;
                case Key.Q:
                    editor.ToggleGizmoSpaceCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }

        var hasPrimaryModifier = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0
            && !e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (!hasPrimaryModifier)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Z:
                if (vm.IsWorkbenchVisible && !vm.IsAnyOverlayOpen)
                {
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        if (vm.CanRedo)
                        {
                            vm.RedoCommand.Execute(null);
                        }
                    }
                    else if (vm.CanUndo)
                    {
                        vm.UndoCommand.Execute(null);
                    }

                    e.Handled = true;
                }

                break;

            case Key.Y:
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift) && vm.IsWorkbenchVisible && !vm.IsAnyOverlayOpen)
                {
                    if (vm.CanRedo)
                    {
                        vm.RedoCommand.Execute(null);
                    }

                    e.Handled = true;
                }

                break;

            case Key.OemPlus:
            case Key.Add:
                vm.ZoomInCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.OemMinus:
            case Key.Subtract:
                vm.ZoomOutCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.D0:
            case Key.NumPad0:
                vm.ResetZoomCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void HandleWindowClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Dispose();
        }
    }
}
