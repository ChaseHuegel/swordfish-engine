using CommunityToolkit.Mvvm.ComponentModel;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one unclean-exit draft row inside the draft recovery dialog.
/// </summary>
public sealed partial class DraftRecoveryItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _resolutionStatus = "Pending";

    [ObservableProperty]
    private bool _isResolved;

    /// <summary>
    ///     Initializes a draft recovery row from a workspace draft entry.
    /// </summary>
    public DraftRecoveryItemViewModel(DraftRecoveryEntry entry)
    {
        Entry = entry;
    }

    /// <summary>
    ///     Underlying draft metadata snapshot.
    /// </summary>
    public DraftRecoveryEntry Entry { get; }

    /// <summary>
    ///     Whether the underlying base file changed on disk since the draft was recorded.
    /// </summary>
    public bool BaseHashChanged => Entry.BaseHashChanged;
}
