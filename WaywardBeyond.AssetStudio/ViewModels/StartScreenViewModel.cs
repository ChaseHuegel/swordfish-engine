using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Services;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     View model backing the welcoming start screen with recents and workspace entry actions.
/// </summary>
public sealed partial class StartScreenViewModel : ObservableObject
{
    private readonly Action<WorkspaceMode, string, string?> _openWorkspace;
    private readonly IStudioWorkspaceService? _workspaceService;
    private readonly IWorkspacePathPicker? _pathPicker;
    private readonly Action<string>? _notify;
    private readonly Action<RecentWorkspaceEntry>? _requestDeleteRecent;
    private string _defaultNamespace = string.Empty;

    /// <summary>
    ///     Initializes the start screen with recent workspace records and open callbacks.
    /// </summary>
    public StartScreenViewModel(
        IReadOnlyList<RecentWorkspaceEntry> recents,
        Action<WorkspaceMode, string, string?> openWorkspace,
        IStudioWorkspaceService? workspaceService = null,
        IWorkspacePathPicker? pathPicker = null,
        Action<string>? notify = null,
        Action<RecentWorkspaceEntry>? requestDeleteRecent = null)
    {
        _openWorkspace = openWorkspace;
        _workspaceService = workspaceService;
        _pathPicker = pathPicker;
        _notify = notify;
        _requestDeleteRecent = requestDeleteRecent;
        _defaultNamespace = workspaceService?.GetSettings().DefaultNamespace ?? string.Empty;
        RefreshRecents(recents);
    }

    /// <summary>
    ///     Optional default namespace prefix applied when opening or scaffolding a workspace from the start screen.
    /// </summary>
    public string DefaultNamespace
    {
        get => _defaultNamespace;
        set => SetProperty(ref _defaultNamespace, value ?? string.Empty);
    }

    /// <summary>
    ///     Recently opened workspaces sorted by recency.
    /// </summary>
    public ObservableCollection<RecentWorkspaceEntry> RecentWorkspaces { get; } = [];

    /// <summary>
    ///     Whether at least one recent workspace entry is listed.
    /// </summary>
    public bool HasRecentWorkspaces => RecentWorkspaces.Count > 0;

    /// <summary>
    ///     Whether no recent workspace entries remain.
    /// </summary>
    public bool HasNoRecentWorkspaces => RecentWorkspaces.Count == 0;

    /// <summary>
    ///     Replaces the displayed list of recently opened workspaces.
    /// </summary>
    public void RefreshRecents(IReadOnlyList<RecentWorkspaceEntry> recents)
    {
        RecentWorkspaces.Clear();
        for (var i = 0; i < recents.Count; i++)
        {
            RecentWorkspaces.Add(recents[i]);
        }

        OnPropertyChanged(nameof(HasRecentWorkspaces));
        OnPropertyChanged(nameof(HasNoRecentWorkspaces));
    }

    [RelayCommand]
    private void OpenRecent(RecentWorkspaceEntry entry)
    {
        ApplyDefaultNamespaceOverride(entry.RootPath);
        _openWorkspace(entry.Mode, entry.RootPath, entry.Name);
    }

    [RelayCommand]
    private void RequestDeleteRecent(RecentWorkspaceEntry entry)
    {
        _requestDeleteRecent?.Invoke(entry);
    }

    [RelayCommand]
    private async Task OpenRepoAsync()
    {
        if (_pathPicker != null)
        {
            var folder = await _pathPicker.PickFolderAsync("Open Wayward Beyond Repository Checkout");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                ApplyDefaultNamespaceOverride(folder);
                _openWorkspace(WorkspaceMode.Repo, folder, null);
            }

            return;
        }

        const string sampleRepo = "~/workspace/swordfish-engine";
        ApplyDefaultNamespaceOverride(sampleRepo);
        _openWorkspace(WorkspaceMode.Repo, sampleRepo, "swordfish-engine");
    }

    [RelayCommand]
    private async Task OpenPackAsync()
    {
        if (_pathPicker != null)
        {
            var folder = await _pathPicker.PickFolderAsync("Open Content Pack Folder");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                ApplyDefaultNamespaceOverride(folder);
                _openWorkspace(WorkspaceMode.Pack, folder, null);
            }

            return;
        }

        const string samplePack = "~/packs/my_colony";
        ApplyDefaultNamespaceOverride(samplePack);
        _openWorkspace(WorkspaceMode.Pack, samplePack, "my_colony");
    }

    [RelayCommand]
    private async Task OpenInstallAsync()
    {
        if (_workspaceService != null && _workspaceService.UsesRealBackend)
        {
            var installs = _workspaceService.LocateGameInstalls();
            if (installs.Count == 1)
            {
                ApplyDefaultNamespaceOverride(installs[0]);
                _openWorkspace(WorkspaceMode.Pack, installs[0], "Wayward Beyond Install");
                return;
            }

            if (installs.Count > 1)
            {
                _notify?.Invoke($"Found {installs.Count} game installs; please choose one explicitly.");
            }
        }

        if (_pathPicker != null)
        {
            var folder = await _pathPicker.PickFolderAsync("Select Wayward Beyond Game Install Folder");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                ApplyDefaultNamespaceOverride(folder);
                _openWorkspace(WorkspaceMode.Pack, folder, null);
            }

            return;
        }

        const string sampleInstall = "/games/WaywardBeyond/install";
        ApplyDefaultNamespaceOverride(sampleInstall);
        _openWorkspace(WorkspaceMode.Pack, sampleInstall, "Wayward Beyond Install");
    }

    [RelayCommand]
    private async Task NewPackAsync()
    {
        var configuredNamespace = DefaultNamespace.Trim().TrimEnd(':').Trim();
        if (_pathPicker != null && _workspaceService != null && _workspaceService.UsesRealBackend)
        {
            var folder = await _pathPicker.PickFolderAsync("Choose Empty Folder for New Content Pack");
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            var slug = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = "new_pack";
            }

            var packNamespace = string.IsNullOrWhiteSpace(configuredNamespace) ? slug : configuredNamespace;
            var created = _workspaceService.CreatePackWorkspace(folder, slug, packNamespace, slug, Environment.UserName);
            _notify?.Invoke(created.Message);
            if (created.Success)
            {
                if (!string.IsNullOrWhiteSpace(configuredNamespace))
                {
                    _workspaceService.SetWorkspaceDefaultNamespace(folder, configuredNamespace);
                }

                _openWorkspace(WorkspaceMode.Pack, folder, slug);
            }

            return;
        }

        const string sampleNewPack = "~/packs/new_frontier_pack";
        ApplyDefaultNamespaceOverride(sampleNewPack);
        _openWorkspace(WorkspaceMode.Pack, sampleNewPack, "new_frontier_pack");
    }

    private void ApplyDefaultNamespaceOverride(string rootPath)
    {
        var cleaned = DefaultNamespace.Trim().TrimEnd(':').Trim();
        if (_workspaceService != null && !string.IsNullOrWhiteSpace(cleaned))
        {
            _workspaceService.SetWorkspaceDefaultNamespace(rootPath, cleaned);
        }
    }
}
