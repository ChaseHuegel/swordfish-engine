using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Services;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     View model backing the explicit cross-file ID rename preview dialog.
/// </summary>
public sealed partial class RenamePreviewDialogViewModel : ObservableObject
{
    private readonly IStudioWorkspaceService _workspaceService;
    private readonly Action<string> _onApplyRename;
    private readonly Action _onClose;
    private string _newId;

    /// <summary>
    ///     Initializes the rename preview dialog for a target asset.
    /// </summary>
    public RenamePreviewDialogViewModel(
        string currentId,
        AssetKind kind,
        IStudioWorkspaceService workspaceService,
        Action<string> onApplyRename,
        Action onClose)
    {
        CurrentId = currentId;
        Kind = kind;
        _workspaceService = workspaceService;
        _onApplyRename = onApplyRename;
        _onClose = onClose;
        _newId = $"{currentId}_mk2";
        RebuildAffectedSpans();
    }

    /// <summary>
    ///     Existing asset ID before rename.
    /// </summary>
    public string CurrentId { get; }

    /// <summary>
    ///     Asset kind being renamed.
    /// </summary>
    public AssetKind Kind { get; }

    /// <summary>
    ///     Proposed replacement identifier for this asset.
    /// </summary>
    public string NewId
    {
        get => _newId;
        set
        {
            var safe = value ?? string.Empty;
            if (SetProperty(ref _newId, safe))
            {
                RebuildAffectedSpans();
            }
        }
    }

    /// <summary>
    ///     Whether to show the brick legacy save hash reverse-map warning banner.
    /// </summary>
    public bool ShowBrickLegacyHashWarning => Kind == AssetKind.Brick;

    /// <summary>
    ///     Files and source spans affected by this rename operation.
    /// </summary>
    public ObservableCollection<RenameAffectedSpan> AffectedSpans { get; } = [];

    /// <summary>
    ///     Summary text describing the rename impact.
    /// </summary>
    public string ImpactSummary => $"{AffectedSpans.Count} references across workspace files";

    [RelayCommand]
    private void ApplyRename()
    {
        if (string.IsNullOrWhiteSpace(NewId))
        {
            return;
        }

        _onApplyRename(NewId.Trim());
    }

    [RelayCommand]
    private void Cancel()
    {
        _onClose();
    }

    private void RebuildAffectedSpans()
    {
        AffectedSpans.Clear();
        var spans = _workspaceService.PreviewRename(CurrentId, NewId, Kind);
        for (var i = 0; i < spans.Count; i++)
        {
            AffectedSpans.Add(spans[i]);
        }

        OnPropertyChanged(nameof(ImpactSummary));
    }
}
