using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Groups validation file buckets under their providing workspace root.
/// </summary>
public sealed partial class ValidationRootGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>
    ///     Initializes a root group inside a validation severity bucket.
    /// </summary>
    public ValidationRootGroupViewModel(string rootName)
    {
        RootName = rootName;
    }

    /// <summary>
    ///     Display name of the workspace root containing these files.
    /// </summary>
    public string RootName { get; }

    /// <summary>
    ///     Child file groups under this root.
    /// </summary>
    public ObservableCollection<ValidationFileGroupViewModel> Files { get; } = [];

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
