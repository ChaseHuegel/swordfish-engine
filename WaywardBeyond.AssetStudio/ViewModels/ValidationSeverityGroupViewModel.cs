using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Top-level severity bucket displayed in the grouped validation dock tree.
/// </summary>
public sealed partial class ValidationSeverityGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>
    ///     Initializes a severity group with its finding count.
    /// </summary>
    public ValidationSeverityGroupViewModel(ValidationSeverity severity, int totalCount)
    {
        Severity = severity;
        TotalCount = totalCount;
    }

    /// <summary>
    ///     Severity category represented by this group.
    /// </summary>
    public ValidationSeverity Severity { get; }

    /// <summary>
    ///     Total number of findings across all roots and files in this severity bucket.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    ///     Display heading for the severity row.
    /// </summary>
    public string Title => Severity switch
    {
        ValidationSeverity.Fatal => "Fatal",
        ValidationSeverity.Broken => "Broken",
        ValidationSeverity.CrossPlatform => "Cross-platform",
        ValidationSeverity.Style => "Style",
        _ => Severity.ToString(),
    };

    /// <summary>
    ///     Workspace root sub-groups inside this severity bucket.
    /// </summary>
    public ObservableCollection<ValidationRootGroupViewModel> Roots { get; } = [];

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
