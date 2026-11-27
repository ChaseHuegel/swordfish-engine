using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Services;
using ToolsEditing = WaywardBeyond.AssetTools.Editing;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     View model backing the new asset dialog with namespace prefill, filename auto-sync, and optional extension handling.
/// </summary>
public sealed partial class NewAssetDialogViewModel : ObservableObject
{
    private readonly IStudioWorkspaceService _workspaceService;
    private readonly Action<NewAssetRequest> _onCreate;
    private readonly Action _onClose;
    private readonly string _defaultNamespace;
    private string _assetId;
    private string _newFileName;
    private string _selectedExistingFile = string.Empty;
    private string _lastAutoAssetId;
    private string _lastAutoFileName;
    private bool _isFileNameAutoSynced = true;
    private bool _isUpdatingFileNameAuto;
    private bool _isUpdatingAssetIdAuto;

    [ObservableProperty]
    private AssetKind _selectedKind = AssetKind.Brick;

    [ObservableProperty]
    private bool _createNewFile = true;

    [ObservableProperty]
    private bool _appendToExistingFile;

    /// <summary>
    ///     Initializes the new asset dialog with workspace folder lookup, default namespace, and create callbacks.
    /// </summary>
    public NewAssetDialogViewModel(
        IStudioWorkspaceService workspaceService,
        Action<NewAssetRequest> onCreate,
        Action onClose,
        string? defaultNamespace = null)
    {
        _workspaceService = workspaceService;
        _onCreate = onCreate;
        _onClose = onClose;
        _defaultNamespace = SanitizeNamespace(defaultNamespace);

        _assetId = BuildDefaultAssetId(_defaultNamespace, _selectedKind);
        _lastAutoAssetId = _assetId;
        _newFileName = ComputeAutoFileName(_assetId, _selectedKind);
        _lastAutoFileName = _newFileName;

        RefreshExistingFiles();
    }

    /// <summary>
    ///     Authorable asset kinds supported by the creation dialog.
    /// </summary>
    public static IReadOnlyList<AssetKind> AVAILABLE_KINDS { get; } =
    [
        AssetKind.Brick,
        AssetKind.Item,
        AssetKind.Paint,
        AssetKind.Recipe,
        AssetKind.Skill,
        AssetKind.Body,
        AssetKind.Material,
        AssetKind.Tag,
        AssetKind.Localization,
    ];

    /// <summary>
    ///     Unique asset identifier pre-filled with the workspace's default namespace.
    /// </summary>
    public string AssetId
    {
        get => _assetId;
        set
        {
            var safe = value ?? string.Empty;
            if (!SetProperty(ref _assetId, safe))
            {
                return;
            }

            if (!_isUpdatingAssetIdAuto && _isFileNameAutoSynced)
            {
                SyncFileNameFromAssetId(safe);
            }

            OnPropertyChanged(nameof(ComputedTargetPath));
        }
    }

    /// <summary>
    ///     Target filename inside the selected kind's folder; extension is optional and defaults to <c>.toml</c>.
    /// </summary>
    public string NewFileName
    {
        get => _newFileName;
        set
        {
            var safe = value ?? string.Empty;
            if (!SetProperty(ref _newFileName, safe))
            {
                return;
            }

            if (!_isUpdatingFileNameAuto)
            {
                if (string.IsNullOrWhiteSpace(safe))
                {
                    _isFileNameAutoSynced = true;
                }
                else if (!string.Equals(safe, _lastAutoFileName, StringComparison.Ordinal))
                {
                    _isFileNameAutoSynced = false;
                }
            }

            OnPropertyChanged(nameof(ComputedTargetPath));
        }
    }

    /// <summary>
    ///     Selected existing file when appending to an existing multi-entry file.
    /// </summary>
    public string SelectedExistingFile
    {
        get => _selectedExistingFile;
        set
        {
            var safe = value ?? string.Empty;
            if (SetProperty(ref _selectedExistingFile, safe))
            {
                OnPropertyChanged(nameof(ComputedTargetPath));
            }
        }
    }

    /// <summary>
    ///     Existing files found inside the selected kind's folder.
    /// </summary>
    public ObservableCollection<string> ExistingFiles { get; } = [];

    /// <summary>
    ///     Computed destination path preview displayed before creation, automatically normalizing <c>.toml</c> when omitted.
    /// </summary>
    public string ComputedTargetPath
    {
        get
        {
            if (AppendToExistingFile)
            {
                return SelectedExistingFile;
            }

            var rawFile = NewFileName.Trim();
            if (rawFile.Length == 0)
            {
                rawFile = ComputeAutoFileName(AssetId, SelectedKind);
            }

            var normalizedFile = ToolsEditing.AssetScaffolder.NormalizeTargetFilePath(
                AssetSeedProjector.ToToolsAssetKind(SelectedKind),
                rawFile);
            return $"{GetKindFolder(SelectedKind)}/{normalizedFile}";
        }
    }

    partial void OnSelectedKindChanged(AssetKind value)
    {
        if (string.Equals(_assetId, _lastAutoAssetId, StringComparison.Ordinal))
        {
            var nextDefaultId = BuildDefaultAssetId(_defaultNamespace, value);
            _lastAutoAssetId = nextDefaultId;
            _isUpdatingAssetIdAuto = true;
            try
            {
                AssetId = nextDefaultId;
            }
            finally
            {
                _isUpdatingAssetIdAuto = false;
            }
        }

        if (_isFileNameAutoSynced)
        {
            SyncFileNameFromAssetId(_assetId);
        }

        RefreshExistingFiles();
        OnPropertyChanged(nameof(ComputedTargetPath));
    }

    partial void OnCreateNewFileChanged(bool value)
    {
        if (value && AppendToExistingFile)
        {
            AppendToExistingFile = false;
        }

        OnPropertyChanged(nameof(ComputedTargetPath));
    }

    partial void OnAppendToExistingFileChanged(bool value)
    {
        if (value && CreateNewFile)
        {
            CreateNewFile = false;
        }

        OnPropertyChanged(nameof(ComputedTargetPath));
    }

    [RelayCommand]
    private void Create()
    {
        if (string.IsNullOrWhiteSpace(AssetId))
        {
            return;
        }

        _onCreate(new NewAssetRequest(SelectedKind, AssetId.Trim(), AppendToExistingFile, ComputedTargetPath));
    }

    [RelayCommand]
    private void Cancel()
    {
        _onClose();
    }

    private void SyncFileNameFromAssetId(string assetId)
    {
        var computed = ComputeAutoFileName(assetId, SelectedKind);
        _lastAutoFileName = computed;
        _isUpdatingFileNameAuto = true;
        try
        {
            NewFileName = computed;
        }
        finally
        {
            _isUpdatingFileNameAuto = false;
        }
    }

    private void RefreshExistingFiles()
    {
        ExistingFiles.Clear();
        var files = _workspaceService.GetExistingFilesForKind(SelectedKind);
        for (var i = 0; i < files.Count; i++)
        {
            ExistingFiles.Add(files[i]);
        }

        SelectedExistingFile = ExistingFiles.Count > 0 ? ExistingFiles[0] : string.Empty;
    }

    private static string SanitizeNamespace(string? rawNamespace)
    {
        var trimmed = (rawNamespace ?? string.Empty).Trim().TrimEnd(':').Trim();
        return trimmed.Length > 0 ? trimmed : "wb";
    }

    private static string BuildDefaultAssetId(string defaultNamespace, AssetKind kind)
    {
        var kindStem = kind switch
        {
            AssetKind.Brick => "new_brick",
            AssetKind.Item => "new_item",
            AssetKind.Paint => "new_paint",
            AssetKind.Recipe => "new_recipe",
            AssetKind.Skill => "new_skill",
            AssetKind.Body => "new_body",
            AssetKind.Material => "new_material",
            AssetKind.Tag => "new_tag",
            AssetKind.Localization => "new_strings",
            _ => "new_asset",
        };

        return kind switch
        {
            AssetKind.Skill or AssetKind.Material or AssetKind.Tag or AssetKind.Localization => kindStem,
            _ => $"{defaultNamespace}:{kindStem}",
        };
    }

    private static string ComputeAutoFileName(string assetId, AssetKind kind)
    {
        var trimmed = (assetId ?? string.Empty).Trim();
        var colonIdx = trimmed.LastIndexOf(':');
        var rawStem = colonIdx >= 0 && colonIdx + 1 < trimmed.Length
            ? trimmed[(colonIdx + 1)..]
            : (colonIdx >= 0 ? string.Empty : trimmed);

        var sb = new StringBuilder(rawStem.Length);
        var invalidChars = Path.GetInvalidFileNameChars();
        for (var i = 0; i < rawStem.Length; i++)
        {
            var ch = rawStem[i];
            if (char.IsWhiteSpace(ch) || ch == '/' || ch == '\\')
            {
                sb.Append('_');
            }
            else if (Array.IndexOf(invalidChars, ch) < 0 && ch != ':')
            {
                sb.Append(ch);
            }
        }

        var stem = sb.ToString().Trim('_', '.');
        if (stem.Length == 0)
        {
            stem = "new_asset";
        }

        return ToolsEditing.AssetScaffolder.NormalizeTargetFilePath(
            AssetSeedProjector.ToToolsAssetKind(kind),
            stem);
    }

    private static string GetKindFolder(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.Brick => "assets/bricks",
            AssetKind.Item => "assets/items",
            AssetKind.Paint => "assets/paints",
            AssetKind.Recipe => "assets/recipes",
            AssetKind.Skill => "assets/skills",
            AssetKind.Body => "assets/bodies",
            AssetKind.Material => "assets/materials",
            AssetKind.Tag => "assets/lang/tags",
            AssetKind.Localization => "assets/lang/en",
            _ => "assets",
        };
    }
}
