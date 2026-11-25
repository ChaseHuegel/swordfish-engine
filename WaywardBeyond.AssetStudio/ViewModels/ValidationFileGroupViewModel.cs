using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Groups validation findings for one relative file inside the validation dock.
/// </summary>
public sealed partial class ValidationFileGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    ///     Initializes a file group collapsed by default.
    /// </summary>
    public ValidationFileGroupViewModel(string relativeFile, bool startExpanded = false)
    {
        RelativeFile = relativeFile;
        _isExpanded = startExpanded;
    }

    /// <summary>
    ///     Workspace-relative path of the file owning these findings.
    /// </summary>
    public string RelativeFile { get; }

    /// <summary>
    ///     Individual findings recorded against this file.
    /// </summary>
    public ObservableCollection<ValidationFinding> Findings { get; } = [];

    /// <summary>
    ///     Compact count label for the collapsed file header.
    /// </summary>
    public string CountLabel => Findings.Count == 1 ? "1 issue" : $"{Findings.Count} issues";

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
