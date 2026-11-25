using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     View model backing the unclean-exit draft recovery dialog.
/// </summary>
public sealed partial class DraftRecoveryDialogViewModel : ObservableObject
{
    private readonly Action<string> _onDecisionToast;
    private readonly Action _onClose;
    private readonly Func<DraftRecoveryEntry, StudioOperationResult>? _onRecoverEntry;
    private readonly Func<DraftRecoveryEntry, StudioOperationResult>? _onDiscardEntry;

    /// <summary>
    ///     Initializes the draft recovery dialog with all detected crash drafts.
    /// </summary>
    public DraftRecoveryDialogViewModel(
        IReadOnlyList<DraftRecoveryEntry> entries,
        Action<string> onDecisionToast,
        Action onClose,
        Func<DraftRecoveryEntry, StudioOperationResult>? onRecoverEntry = null,
        Func<DraftRecoveryEntry, StudioOperationResult>? onDiscardEntry = null)
    {
        _onDecisionToast = onDecisionToast;
        _onClose = onClose;
        _onRecoverEntry = onRecoverEntry;
        _onDiscardEntry = onDiscardEntry;

        for (var i = 0; i < entries.Count; i++)
        {
            Items.Add(new DraftRecoveryItemViewModel(entries[i]));
        }
    }

    /// <summary>
    ///     Unclean-exit draft items awaiting per-file recover or discard decisions.
    /// </summary>
    public ObservableCollection<DraftRecoveryItemViewModel> Items { get; } = [];

    [RelayCommand]
    private void RecoverItem(DraftRecoveryItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        var result = _onRecoverEntry?.Invoke(item.Entry)
            ?? new StudioOperationResult(true, $"Recovered draft for {item.Entry.RelativeFile}");
        item.ResolutionStatus = result.Success ? "Recovered" : "Failed";
        item.IsResolved = result.Success;
        _onDecisionToast(result.Message);
    }

    [RelayCommand]
    private void DiscardItem(DraftRecoveryItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        var result = _onDiscardEntry?.Invoke(item.Entry)
            ?? new StudioOperationResult(true, $"Discarded draft for {item.Entry.RelativeFile}");
        item.ResolutionStatus = result.Success ? "Discarded" : "Failed";
        item.IsResolved = result.Success;
        _onDecisionToast(result.Message);
    }

    [RelayCommand]
    private void RecoverAll()
    {
        var recoveredCount = 0;
        for (var i = 0; i < Items.Count; i++)
        {
            var result = _onRecoverEntry?.Invoke(Items[i].Entry)
                ?? new StudioOperationResult(true, string.Empty);
            Items[i].ResolutionStatus = result.Success ? "Recovered" : "Failed";
            Items[i].IsResolved = result.Success;
            if (result.Success)
            {
                recoveredCount++;
            }
        }

        _onDecisionToast($"Recovered {recoveredCount} draft file(s).");
        _onClose();
    }

    [RelayCommand]
    private void DiscardAll()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            _onDiscardEntry?.Invoke(Items[i].Entry);
            Items[i].ResolutionStatus = "Discarded";
            Items[i].IsResolved = true;
        }

        _onDecisionToast("Discarded pending recovery drafts.");
        _onClose();
    }

    [RelayCommand]
    private void Close()
    {
        _onClose();
    }
}
