using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using WaywardBeyond.AssetStudio.ViewModels;

namespace WaywardBeyond.AssetStudio.Views;

/// <summary>
///     Two-pane tag editor displaying editable file order with drag-and-drop reordering alongside live effective order.
/// </summary>
public sealed partial class TagEditorView : UserControl
{
    private static readonly DataFormat<string> TAG_INDEX_FORMAT = DataFormat.CreateStringApplicationFormat("AssetStudio.TagIndex");
    private const double DRAG_THRESHOLD_PX = 5.0;

    private Point _dragOrigin;
    private TagListEntryViewModel? _dragCandidate;
    private bool _isDragging;

    /// <summary>
    ///     Initializes the tag editor view.
    /// </summary>
    public TagEditorView()
    {
        InitializeComponent();
    }

    private void HandleTagRowLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        border.RemoveHandler(DragDrop.DragOverEvent, HandleTagRowDragOver);
        border.RemoveHandler(DragDrop.DragLeaveEvent, HandleTagRowDragLeave);
        border.RemoveHandler(DragDrop.DropEvent, HandleTagRowDrop);

        border.AddHandler(DragDrop.DragOverEvent, HandleTagRowDragOver);
        border.AddHandler(DragDrop.DragLeaveEvent, HandleTagRowDragLeave);
        border.AddHandler(DragDrop.DropEvent, HandleTagRowDrop);
    }

    private void HandleGripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not AssetDocumentViewModel doc || doc.IsReadOnly)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (sender is Control { DataContext: TagListEntryViewModel entry })
        {
            _dragOrigin = e.GetPosition(this);
            _dragCandidate = entry;
            _isDragging = false;
        }
    }

    private async void HandleGripPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragCandidate == null || _isDragging)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragCandidate = null;
            return;
        }

        var current = e.GetPosition(this);
        var dx = current.X - _dragOrigin.X;
        var dy = current.Y - _dragOrigin.Y;
        if ((dx * dx) + (dy * dy) < DRAG_THRESHOLD_PX * DRAG_THRESHOLD_PX)
        {
            return;
        }

        _isDragging = true;
        var dragged = _dragCandidate;
        _dragCandidate = null;

        try
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(TAG_INDEX_FORMAT, dragged.Index.ToString(CultureInfo.InvariantCulture)));
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Tag drag-and-drop operation ended with error: {ex.Message}");
        }
        finally
        {
            _isDragging = false;
        }
    }

    private void HandleGripPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragCandidate = null;
        _isDragging = false;
    }

    private void HandleTagRowDragOver(object? sender, DragEventArgs e)
    {
        if (sender is not Border border
            || DataContext is not AssetDocumentViewModel doc
            || doc.IsReadOnly
            || !e.DataTransfer.Contains(TAG_INDEX_FORMAT))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        if (this.TryFindResource("StudioAccentBrush", ActualThemeVariant, out var accent) && accent is IBrush brush)
        {
            border.BorderBrush = brush;
        }

        e.Handled = true;
    }

    private void HandleTagRowDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.ClearValue(Border.BorderBrushProperty);
        }
    }

    private void HandleTagRowDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        border.ClearValue(Border.BorderBrushProperty);

        if (DataContext is not AssetDocumentViewModel doc
            || doc.IsReadOnly
            || border.DataContext is not TagListEntryViewModel targetEntry
            || !e.DataTransfer.Contains(TAG_INDEX_FORMAT))
        {
            return;
        }

        var rawIndex = e.DataTransfer.TryGetValue(TAG_INDEX_FORMAT);
        if (int.TryParse(rawIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromIndex))
        {
            doc.MoveTag(fromIndex, targetEntry.Index);
            e.Handled = true;
        }
    }
}
