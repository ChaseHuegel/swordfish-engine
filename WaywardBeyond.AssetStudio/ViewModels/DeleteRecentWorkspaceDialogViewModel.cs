using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     View model backing the recent workspace removal confirmation dialog with optional recursive disk deletion.
/// </summary>
public sealed partial class DeleteRecentWorkspaceDialogViewModel : ObservableObject
{
    private readonly Action<bool> _onConfirm;
    private readonly Action _onClose;

    [ObservableProperty]
    private bool _deleteFromDisk;

    /// <summary>
    ///     Initializes the removal confirmation dialog for <paramref name="entry"/>.
    /// </summary>
    public DeleteRecentWorkspaceDialogViewModel(
        RecentWorkspaceEntry entry,
        bool canDeleteFromDisk,
        string? diskDeleteDisabledReason,
        Action<bool> onConfirm,
        Action onClose)
    {
        Entry = entry;
        CanDeleteFromDisk = canDeleteFromDisk;
        DiskDeleteDisabledReason = diskDeleteDisabledReason;
        _onConfirm = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));
        _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));
    }

    /// <summary>
    ///     Recent workspace entry scheduled for removal.
    /// </summary>
    public RecentWorkspaceEntry Entry { get; }

    /// <summary>
    ///     Display name of the workspace.
    /// </summary>
    public string WorkspaceName => Entry.Name;

    /// <summary>
    ///     Mode of the workspace entry (Repo or Pack).
    /// </summary>
    public WorkspaceMode Mode => Entry.Mode;

    /// <summary>
    ///     Full root directory path of the workspace.
    /// </summary>
    public string RootPath => Entry.RootPath;

    /// <summary>
    ///     Formatted timestamp of the last recorded workspace access.
    /// </summary>
    public string LastOpenedLabel => Entry.LastOpenedLabel;

    /// <summary>
    ///     Count of quick-indexed assets found in the workspace.
    /// </summary>
    public int AssetCount => Entry.AssetCount;

    /// <summary>
    ///     Whether the user can opt in to recursively deleting the workspace folder on disk.
    /// </summary>
    public bool CanDeleteFromDisk { get; }

    /// <summary>
    ///     Explanation displayed when recursive disk deletion is disabled for safety.
    /// </summary>
    public string? DiskDeleteDisabledReason { get; }

    /// <summary>
    ///     Whether a disabled reason notice should be shown beneath the disk deletion checkbox.
    /// </summary>
    public bool HasDiskDeleteDisabledReason => !CanDeleteFromDisk && !string.IsNullOrWhiteSpace(DiskDeleteDisabledReason);

    /// <summary>
    ///     Primary confirmation action label reflecting whether disk deletion is selected.
    /// </summary>
    public string ConfirmButtonLabel => DeleteFromDisk ? "Delete from Disk" : "Remove from Recents";

    /// <summary>
    ///     Plain-language explanation of what confirming this dialog will do.
    /// </summary>
    public string ActionSummary => DeleteFromDisk
        ? "Removes this workspace from Recent Workspaces and permanently deletes the folder and all of its contents from disk."
        : "Removes this workspace from Recent Workspaces only. Files on disk will remain untouched.";

    partial void OnDeleteFromDiskChanged(bool value)
    {
        OnPropertyChanged(nameof(ConfirmButtonLabel));
        OnPropertyChanged(nameof(ActionSummary));
    }

    [RelayCommand]
    private void Confirm()
    {
        _onConfirm(DeleteFromDisk && CanDeleteFromDisk);
    }

    [RelayCommand]
    private void Cancel()
    {
        _onClose();
    }
}
