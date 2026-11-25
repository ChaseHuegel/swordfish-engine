using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.Controls;

/// <summary>
///     Reusable body sprite path input pairing a workspace thumbnail grid dropdown with a disk file picker button.
/// </summary>
public sealed partial class BodySpritePickerControl : UserControl
{
    /// <summary>
    ///     Styled property binding the workspace-relative sprite texture path.
    /// </summary>
    public static readonly StyledProperty<string> TexturePathProperty =
        AvaloniaProperty.Register<BodySpritePickerControl, string>(
            nameof(TexturePath),
            defaultValue: string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    ///     Styled property supplying the available workspace image entries for the thumbnail dropdown grid.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<WorkspaceImageEntry>?> ImagesProperty =
        AvaloniaProperty.Register<BodySpritePickerControl, IReadOnlyList<WorkspaceImageEntry>?>(nameof(Images));

    /// <summary>
    ///     Styled property disabling text edits, dropdown picking, and disk browsing when true.
    /// </summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<BodySpritePickerControl, bool>(nameof(IsReadOnly));

    /// <summary>
    ///     Styled property configuring the placeholder hint shown when <see cref="TexturePath"/> is empty.
    /// </summary>
    public static readonly StyledProperty<string> WatermarkProperty =
        AvaloniaProperty.Register<BodySpritePickerControl, string>(
            nameof(Watermark),
            defaultValue: "characters/...");

    /// <summary>
    ///     Styled property binding the command invoked when the user clicks the disk file picker button.
    /// </summary>
    public static readonly StyledProperty<ICommand?> BrowseFileCommandProperty =
        AvaloniaProperty.Register<BodySpritePickerControl, ICommand?>(nameof(BrowseFileCommand));

    /// <summary>
    ///     Styled property binding the parameter passed to <see cref="BrowseFileCommand"/>.
    /// </summary>
    public static readonly StyledProperty<object?> BrowseFileCommandParameterProperty =
        AvaloniaProperty.Register<BodySpritePickerControl, object?>(nameof(BrowseFileCommandParameter));

    private readonly ObservableCollection<WorkspaceImageEntry> _filteredImages = [];
    private bool _isSyncing;

    /// <summary>
    ///     Initializes the body sprite picker control and wires its dropdown popup and filter behavior.
    /// </summary>
    public BodySpritePickerControl()
    {
        InitializeComponent();

        PART_PickerPopup.PlacementTarget = PART_InputRow;
        PART_ImageItems.ItemsSource = _filteredImages;

        PART_PathInput.TextChanged += HandlePathTextChanged;
        PART_DropdownButton.Click += HandleDropdownButtonClicked;
        PART_BrowseButton.Click += HandleBrowseButtonClicked;
        PART_FilterInput.TextChanged += HandleFilterTextChanged;
        PART_FilterInput.KeyDown += HandleFilterKeyDown;
        PART_PickerPopup.Opened += HandlePopupOpened;
        PART_PickerPopup.Closed += HandlePopupClosed;

        SyncInputFromProperties();
        RefreshFilteredImages();
    }

    /// <summary>
    ///     Workspace-relative sprite texture path edited by this control.
    /// </summary>
    public string TexturePath
    {
        get => GetValue(TexturePathProperty);
        set => SetValue(TexturePathProperty, value);
    }

    /// <summary>
    ///     Workspace image entries offered in the searchable thumbnail dropdown grid.
    /// </summary>
    public IReadOnlyList<WorkspaceImageEntry>? Images
    {
        get => GetValue(ImagesProperty);
        set => SetValue(ImagesProperty, value);
    }

    /// <summary>
    ///     True when the control is read-only and cannot change <see cref="TexturePath"/>.
    /// </summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>
    ///     Placeholder text displayed when the path input is empty.
    /// </summary>
    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    /// <summary>
    ///     Command executed when the user clicks the disk browse button.
    /// </summary>
    public ICommand? BrowseFileCommand
    {
        get => GetValue(BrowseFileCommandProperty);
        set => SetValue(BrowseFileCommandProperty, value);
    }

    /// <summary>
    ///     Command parameter forwarded to <see cref="BrowseFileCommand"/>.
    /// </summary>
    public object? BrowseFileCommandParameter
    {
        get => GetValue(BrowseFileCommandParameterProperty);
        set => SetValue(BrowseFileCommandParameterProperty, value);
    }

    /// <summary>
    ///     Gets or sets whether the workspace thumbnail grid dropdown popup is currently open.
    /// </summary>
    public bool IsPickerOpen
    {
        get => PART_PickerPopup.IsOpen;
        set
        {
            if (value && IsReadOnly)
            {
                return;
            }

            PART_PickerPopup.IsOpen = value;
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TexturePathProperty)
        {
            if (!_isSyncing)
            {
                SyncPathTextBox();
            }
        }
        else if (change.Property == ImagesProperty)
        {
            RefreshFilteredImages();
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            SyncReadOnlyState();
        }
        else if (change.Property == WatermarkProperty)
        {
            PART_PathInput.Watermark = Watermark;
        }
        else if (change.Property == BrowseFileCommandProperty)
        {
            PART_BrowseButton.Command = BrowseFileCommand;
            PART_BrowseButton.IsEnabled = !IsReadOnly;
        }
        else if (change.Property == BrowseFileCommandParameterProperty)
        {
            PART_BrowseButton.CommandParameter = BrowseFileCommandParameter;
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PART_PickerPopup.IsOpen = false;
    }

    private void SyncInputFromProperties()
    {
        SyncPathTextBox();
        PART_PathInput.Watermark = Watermark;
        PART_BrowseButton.Command = BrowseFileCommand;
        PART_BrowseButton.CommandParameter = BrowseFileCommandParameter;
        SyncReadOnlyState();
    }

    private void SyncPathTextBox()
    {
        var next = TexturePath ?? string.Empty;
        if (string.Equals(PART_PathInput.Text ?? string.Empty, next, StringComparison.Ordinal))
        {
            return;
        }

        _isSyncing = true;
        try
        {
            PART_PathInput.Text = next;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void SyncReadOnlyState()
    {
        var readOnly = IsReadOnly;
        PART_PathInput.IsReadOnly = readOnly;
        PART_DropdownButton.IsEnabled = !readOnly;
        PART_BrowseButton.IsEnabled = !readOnly;
        if (readOnly && PART_PickerPopup.IsOpen)
        {
            PART_PickerPopup.IsOpen = false;
        }
    }

    private void HandlePathTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        var next = PART_PathInput.Text ?? string.Empty;
        if (string.Equals(TexturePath ?? string.Empty, next, StringComparison.Ordinal))
        {
            return;
        }

        _isSyncing = true;
        try
        {
            SetCurrentValue(TexturePathProperty, next);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void HandleDropdownButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (IsReadOnly)
        {
            return;
        }

        PART_PickerPopup.IsOpen = !PART_PickerPopup.IsOpen;
    }

    private void HandleBrowseButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (PART_PickerPopup.IsOpen)
        {
            PART_PickerPopup.IsOpen = false;
        }
    }

    private void HandlePopupOpened(object? sender, EventArgs e)
    {
        PART_DropdownButton.Classes.Set("active", true);
        RefreshFilteredImages();
        PART_FilterInput.Focus();
    }

    private void HandlePopupClosed(object? sender, EventArgs e)
    {
        PART_DropdownButton.Classes.Set("active", false);
    }

    private void HandleFilterTextChanged(object? sender, TextChangedEventArgs e)
    {
        RefreshFilteredImages();
    }

    private void HandleFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            PART_PickerPopup.IsOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _filteredImages.Count == 1)
        {
            SelectImageEntry(_filteredImages[0]);
            e.Handled = true;
        }
    }

    private void HandleImageTileClicked(object? sender, RoutedEventArgs e)
    {
        if (IsReadOnly || sender is not Button { DataContext: WorkspaceImageEntry entry })
        {
            return;
        }

        SelectImageEntry(entry);
    }

    private void SelectImageEntry(WorkspaceImageEntry entry)
    {
        _isSyncing = true;
        try
        {
            SetCurrentValue(TexturePathProperty, entry.RelativePath);
            PART_PathInput.Text = entry.RelativePath;
        }
        finally
        {
            _isSyncing = false;
        }

        PART_PickerPopup.IsOpen = false;
    }

    private void RefreshFilteredImages()
    {
        _filteredImages.Clear();

        IReadOnlyList<WorkspaceImageEntry>? source = Images;
        var total = source?.Count ?? 0;
        var query = PART_FilterInput.Text?.Trim() ?? string.Empty;
        var hasFilter = query.Length > 0;

        if (source != null)
        {
            for (var i = 0; i < source.Count; i++)
            {
                WorkspaceImageEntry entry = source[i];
                if (!hasFilter || MatchesFilter(in entry, query))
                {
                    _filteredImages.Add(entry);
                }
            }
        }

        var count = _filteredImages.Count;
        if (hasFilter && total > 0)
        {
            PART_CountLabel.Text = $"{count} of {total}";
        }
        else
        {
            PART_CountLabel.Text = count == 1 ? "1 image" : $"{count} images";
        }

        var isEmpty = count == 0;
        PART_EmptyState.IsVisible = isEmpty;
        PART_ScrollViewer.IsVisible = !isEmpty;
        PART_EmptyLabel.Text = total == 0
            ? "No images found in workspace"
            : "No images match filter";
    }

    private static bool MatchesFilter(in WorkspaceImageEntry entry, string query)
    {
        return entry.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.FolderHint.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
