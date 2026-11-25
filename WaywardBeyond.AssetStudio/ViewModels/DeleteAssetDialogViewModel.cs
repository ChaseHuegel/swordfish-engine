using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     View model backing the asset deletion confirmation dialog showing file path and inbound references.
/// </summary>
public sealed partial class DeleteAssetDialogViewModel : ObservableObject
{
    private readonly Action _onConfirmDelete;
    private readonly Action _onClose;

    /// <summary>
    ///     Initializes the delete confirmation dialog for <paramref name="document"/>.
    /// </summary>
    public DeleteAssetDialogViewModel(
        AssetDocumentViewModel document,
        Action onConfirmDelete,
        Action onClose)
    {
        ArgumentNullException.ThrowIfNull(document);
        _onConfirmDelete = onConfirmDelete ?? throw new ArgumentNullException(nameof(onConfirmDelete));
        _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));

        AssetId = document.Id;
        Kind = document.Kind;
        RelativeFile = document.RelativeFile;
        IsMultiEntry = document.HasMultiEntry;
        InboundReferences = [.. document.InboundReferences];
    }

    /// <summary>
    ///     Identifier of the asset scheduled for deletion.
    /// </summary>
    public string AssetId { get; }

    /// <summary>
    ///     Asset kind of the target document.
    /// </summary>
    public AssetKind Kind { get; }

    /// <summary>
    ///     Relative path of the backing file inside its workspace root.
    /// </summary>
    public string RelativeFile { get; }

    /// <summary>
    ///     Whether the asset lives inside a shared multi-entry file where only its entry will be removed.
    /// </summary>
    public bool IsMultiEntry { get; }

    /// <summary>
    ///     Workspace assets currently referencing this target asset.
    /// </summary>
    public IReadOnlyList<InboundReferenceItem> InboundReferences { get; }

    /// <summary>
    ///     Whether at least one other asset references this asset.
    /// </summary>
    public bool HasInboundReferences => InboundReferences.Count > 0;

    /// <summary>
    ///     Plain-language explanation of what deleting this asset will modify on disk.
    /// </summary>
    public string ActionSummary => IsMultiEntry
        ? $"Removes only the '{AssetId}' entry from {RelativeFile}. Other entries in the file stay untouched."
        : $"Permanently deletes '{AssetId}' from {RelativeFile}.";

    /// <summary>
    ///     Warning headline shown when inbound references point at this asset.
    /// </summary>
    public string InboundWarningTitle => InboundReferences.Count == 1
        ? "1 other asset references this entry"
        : $"{InboundReferences.Count} other assets reference this entry";

    [RelayCommand]
    private void ConfirmDelete()
    {
        _onConfirmDelete();
    }

    [RelayCommand]
    private void Cancel()
    {
        _onClose();
    }
}
