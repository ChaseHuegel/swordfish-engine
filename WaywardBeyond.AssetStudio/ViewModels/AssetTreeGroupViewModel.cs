using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents a collapsible asset category node inside the workspace explorer tree.
/// </summary>
public sealed partial class AssetTreeGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>
    ///     Initializes a tree group with its display title and asset kind.
    /// </summary>
    public AssetTreeGroupViewModel(string title, AssetKind kind)
    {
        Title = title;
        Kind = kind;
    }

    /// <summary>
    ///     Group heading displayed in the explorer tree.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Asset kind represented by entries in this group.
    /// </summary>
    public AssetKind Kind { get; }

    /// <summary>
    ///     Child asset documents visible under the current filter.
    /// </summary>
    public ObservableCollection<AssetDocumentViewModel> Items { get; } = [];

    /// <summary>
    ///     Compact count string rendered on the group row.
    /// </summary>
    public string CountText => Items.Count.ToString();

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
