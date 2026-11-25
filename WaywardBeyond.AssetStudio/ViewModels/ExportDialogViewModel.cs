using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     View model backing the staged pack export dialog with pre-flight validation gating.
/// </summary>
public sealed partial class ExportDialogViewModel : ObservableObject
{
    private readonly Action<ExportPackRequest> _onExport;
    private readonly Action _onClose;

    [ObservableProperty]
    private string _selectedFormat;

    [ObservableProperty]
    private bool _acknowledgeWarnings;

    /// <summary>
    ///     Initializes the export dialog with validation counts and staged pack metadata preview.
    /// </summary>
    public ExportDialogViewModel(
        string packId,
        int contentVersion,
        int fatalCount,
        int brokenCount,
        int crossPlatformCount,
        int styleCount,
        string stagedPackToml,
        Action<ExportPackRequest> onExport,
        Action onClose)
    {
        PackId = packId;
        ContentVersion = contentVersion;
        FatalCount = fatalCount;
        BrokenCount = brokenCount;
        CrossPlatformCount = crossPlatformCount;
        StyleCount = styleCount;
        StagedPackToml = stagedPackToml;
        _selectedFormat = ".zip";
        _onExport = onExport;
        _onClose = onClose;
    }

    /// <summary>
    ///     Supported output archive formats.
    /// </summary>
    public static IReadOnlyList<string> FORMAT_OPTIONS { get; } = [".zip", ".tar.gz"];

    /// <summary>
    ///     Pack identifier written into the staged archive name.
    /// </summary>
    public string PackId { get; }

    /// <summary>
    ///     Content schema version written into staged pack metadata.
    /// </summary>
    public int ContentVersion { get; }

    /// <summary>
    ///     Fatal diagnostic count blocking export.
    /// </summary>
    public int FatalCount { get; }

    /// <summary>
    ///     Broken diagnostic count blocking export.
    /// </summary>
    public int BrokenCount { get; }

    /// <summary>
    ///     Cross-platform warning count requiring acknowledgement.
    /// </summary>
    public int CrossPlatformCount { get; }

    /// <summary>
    ///     Style warning count requiring acknowledgement.
    /// </summary>
    public int StyleCount { get; }

    /// <summary>
    ///     Staged pack.toml preview written only into the temporary staging folder.
    /// </summary>
    public string StagedPackToml { get; }

    /// <summary>
    ///     Whether Fatal or Broken findings block exporting.
    /// </summary>
    public bool IsBlocked => FatalCount > 0 || BrokenCount > 0;

    /// <summary>
    ///     Whether non-blocking warnings require explicit confirmation.
    /// </summary>
    public bool HasWarnings => !IsBlocked && (CrossPlatformCount > 0 || StyleCount > 0);

    /// <summary>
    ///     Whether the export button can currently execute.
    /// </summary>
    public bool CanExport => !IsBlocked && (!HasWarnings || AcknowledgeWarnings);

    /// <summary>
    ///     Computed archive filename preview.
    /// </summary>
    public string ArtifactFileName => $"{PackId}-{ContentVersion}{SelectedFormat}";

    partial void OnSelectedFormatChanged(string value)
    {
        OnPropertyChanged(nameof(ArtifactFileName));
    }

    partial void OnAcknowledgeWarningsChanged(bool value)
    {
        OnPropertyChanged(nameof(CanExport));
    }

    [RelayCommand]
    private void ConfirmExport()
    {
        if (!CanExport)
        {
            return;
        }

        _onExport(new ExportPackRequest(PackId, ContentVersion, SelectedFormat, AcknowledgeWarnings));
    }

    [RelayCommand]
    private void Cancel()
    {
        _onClose();
    }
}
