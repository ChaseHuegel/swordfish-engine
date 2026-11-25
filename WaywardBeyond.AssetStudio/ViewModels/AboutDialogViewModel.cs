using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Services;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     View model backing the about and runtime diagnostics dialog.
/// </summary>
public sealed partial class AboutDialogViewModel : ObservableObject
{
    private readonly IStudioWorkspaceService _workspaceService;
    private readonly Action _onClose;

    [ObservableProperty]
    private string? _copyStatusMessage;

    /// <summary>
    ///     Initializes the about dialog with runtime environment diagnostics.
    /// </summary>
    public AboutDialogViewModel(
        AboutDiagnosticsInfo info,
        IStudioWorkspaceService workspaceService,
        Action onClose)
    {
        Info = info;
        _workspaceService = workspaceService;
        _onClose = onClose;
    }

    /// <summary>
    ///     Version, workspace, OS, and OpenGL diagnostics snapshot.
    /// </summary>
    public AboutDiagnosticsInfo Info { get; }

    /// <summary>
    ///     Formatted content schema version badge text.
    /// </summary>
    public string ContentVersionLabel => $"ContentVersion {Info.ContentVersion}";

    /// <summary>
    ///     Whether a copy confirmation banner should display.
    /// </summary>
    public bool HasCopyStatus => !string.IsNullOrWhiteSpace(CopyStatusMessage);

    partial void OnCopyStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCopyStatus));
    }

    [RelayCommand]
    private void CopyDiagnosticsZip()
    {
        var result = _workspaceService.CopyDiagnosticsZip(Info);
        CopyStatusMessage = result.Message;
    }

    [RelayCommand]
    private void Close()
    {
        _onClose();
    }
}
