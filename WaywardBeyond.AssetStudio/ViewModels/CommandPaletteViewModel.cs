using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     View model backing the F1 command palette with ranked search and scope/kind filters.
/// </summary>
public sealed partial class CommandPaletteViewModel : ObservableObject
{
    private readonly IReadOnlyList<CommandPaletteEntry> _allEntries;
    private readonly Action<CommandPaletteEntry, bool> _onExecute;
    private readonly Action _onClose;
    private readonly Func<string, string, string, IReadOnlyList<CommandPaletteEntry>>? _backendSearch;
    private string _query = string.Empty;

    [ObservableProperty]
    private string _selectedScope = "All";

    [ObservableProperty]
    private string _selectedKindFilter = "Any Kind";

    [ObservableProperty]
    private int _selectedIndex;

    /// <summary>
    ///     Initializes the command palette with indexed workspace items, resources, and actions.
    /// </summary>
    public CommandPaletteViewModel(
        IReadOnlyList<CommandPaletteEntry> allEntries,
        Action<CommandPaletteEntry, bool> onExecute,
        Action onClose,
        Func<string, string, string, IReadOnlyList<CommandPaletteEntry>>? backendSearch = null)
    {
        _allEntries = allEntries;
        _onExecute = onExecute;
        _onClose = onClose;
        _backendSearch = backendSearch;

        for (var i = 0; i < SCOPE_FILTERS.Count; i++)
        {
            var scope = SCOPE_FILTERS[i];
            ScopeChips.Add(new FilterChipViewModel(scope, i == 0, chip => SelectedScope = chip.Label));
        }

        for (var i = 0; i < KIND_FILTERS.Count; i++)
        {
            var kind = KIND_FILTERS[i];
            KindChips.Add(new FilterChipViewModel(kind, i == 0, chip => SelectedKindFilter = chip.Label));
        }

        RebuildResults();
    }

    /// <summary>
    ///     Available scope filter labels.
    /// </summary>
    public static IReadOnlyList<string> SCOPE_FILTERS { get; } = ["All", "Assets", "Resources", "Actions"];

    /// <summary>
    ///     Available asset kind filter labels.
    /// </summary>
    public static IReadOnlyList<string> KIND_FILTERS { get; } =
    [
        "Any Kind",
        "Brick",
        "Item",
        "Paint",
        "Skill",
        "Body",
        "Material",
        "Tag",
        "Localization",
        "Blueprint",
    ];

    /// <summary>
    ///     Selectable scope filter chips highlighting the active scope.
    /// </summary>
    public ObservableCollection<FilterChipViewModel> ScopeChips { get; } = [];

    /// <summary>
    ///     Selectable kind filter chips highlighting the active kind.
    /// </summary>
    public ObservableCollection<FilterChipViewModel> KindChips { get; } = [];

    /// <summary>
    ///     Ranked raw entries matching the current query and filter chips.
    /// </summary>
    public ObservableCollection<CommandPaletteEntry> Results { get; } = [];

    /// <summary>
    ///     Presentation rows with keyboard selection and status styling flags.
    /// </summary>
    public ObservableCollection<CommandPaletteRowViewModel> ResultRows { get; } = [];

    /// <summary>
    ///     Search text entered in the command palette input box.
    /// </summary>
    public string Query
    {
        get => _query;
        set
        {
            var safe = value ?? string.Empty;
            if (SetProperty(ref _query, safe))
            {
                RebuildResults();
            }
        }
    }

    /// <summary>
    ///     Summary count of matching entries.
    /// </summary>
    public string ResultCountLabel => Results.Count == 1 ? "1 match" : $"{Results.Count} matches";

    partial void OnSelectedScopeChanged(string value)
    {
        for (var i = 0; i < ScopeChips.Count; i++)
        {
            ScopeChips[i].IsSelected = string.Equals(ScopeChips[i].Label, value, StringComparison.Ordinal);
        }

        RebuildResults();
    }

    partial void OnSelectedKindFilterChanged(string value)
    {
        for (var i = 0; i < KindChips.Count; i++)
        {
            KindChips[i].IsSelected = string.Equals(KindChips[i].Label, value, StringComparison.Ordinal);
        }

        RebuildResults();
    }

    partial void OnSelectedIndexChanged(int value)
    {
        for (var i = 0; i < ResultRows.Count; i++)
        {
            ResultRows[i].IsSelected = i == value;
        }
    }

    [RelayCommand]
    private void SetScope(string scope)
    {
        SelectedScope = scope;
    }

    [RelayCommand]
    private void SetKindFilter(string kindFilter)
    {
        SelectedKindFilter = kindFilter;
    }

    [RelayCommand]
    private void ExecuteWinning(CommandPaletteEntry entry)
    {
        _onExecute(entry, false);
    }

    [RelayCommand]
    private void ExecuteShadowed(CommandPaletteEntry entry)
    {
        _onExecute(entry, true);
    }

    [RelayCommand]
    private void ExecuteRowWinning(CommandPaletteRowViewModel? row)
    {
        if (row != null)
        {
            _onExecute(row.Entry, false);
        }
    }

    [RelayCommand]
    private void ExecuteSelectedWinning()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Results.Count)
        {
            _onExecute(Results[SelectedIndex], false);
        }
    }

    [RelayCommand]
    private void ExecuteSelectedShadowed()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Results.Count)
        {
            _onExecute(Results[SelectedIndex], true);
        }
    }

    [RelayCommand]
    private void Close()
    {
        _onClose();
    }

    private void RebuildResults()
    {
        var q = Query.Trim();
        if (_backendSearch != null)
        {
            var backendHits = _backendSearch(q, SelectedScope, "All");
            Results.Clear();
            ResultRows.Clear();
            var idx = 0;
            for (var i = 0; i < backendHits.Count; i++)
            {
                var hit = backendHits[i];
                if (!MatchesKind(hit))
                {
                    continue;
                }

                Results.Add(hit);
                ResultRows.Add(new CommandPaletteRowViewModel(hit, idx == 0));
                idx++;
            }

            SelectedIndex = Results.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(ResultCountLabel));
            return;
        }

        var scored = new List<(CommandPaletteEntry Entry, int Rank)>();

        for (var i = 0; i < _allEntries.Count; i++)
        {
            var entry = _allEntries[i];
            if (!MatchesScope(entry) || !MatchesKind(entry))
            {
                continue;
            }

            var rank = ScoreMatch(entry, q);
            if (rank < 0)
            {
                continue;
            }

            scored.Add((entry, rank));
        }

        scored.Sort(static (a, b) =>
        {
            var cmp = b.Rank.CompareTo(a.Rank);
            return cmp != 0 ? cmp : string.Compare(a.Entry.Title, b.Entry.Title, StringComparison.OrdinalIgnoreCase);
        });

        Results.Clear();
        ResultRows.Clear();
        for (var i = 0; i < scored.Count; i++)
        {
            Results.Add(scored[i].Entry);
            ResultRows.Add(new CommandPaletteRowViewModel(scored[i].Entry, i == 0));
        }

        SelectedIndex = Results.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(ResultCountLabel));
    }

    private bool MatchesScope(CommandPaletteEntry entry)
    {
        if (string.Equals(SelectedScope, "All", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(entry.Category, SelectedScope, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesKind(CommandPaletteEntry entry)
    {
        if (string.Equals(SelectedKindFilter, "Any Kind", StringComparison.Ordinal))
        {
            return true;
        }

        return entry.Kind != null
            && string.Equals(entry.Kind.Value.ToString(), SelectedKindFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static int ScoreMatch(CommandPaletteEntry entry, string query)
    {
        if (query.Length == 0)
        {
            return entry.Score;
        }

        if (string.Equals(entry.Title, query, StringComparison.OrdinalIgnoreCase))
        {
            return 1000;
        }

        var colonIndex = entry.Title.IndexOf(':');
        if (colonIndex >= 0 && colonIndex + 1 < entry.Title.Length)
        {
            var stem = entry.Title[(colonIndex + 1)..];
            if (string.Equals(stem, query, StringComparison.OrdinalIgnoreCase))
            {
                return 920;
            }

            if (stem.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 820;
            }
        }

        if (entry.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 750;
        }

        if (entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 500;
        }

        if (entry.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 350;
        }

        var qIdx = 0;
        for (var i = 0; i < entry.Title.Length && qIdx < query.Length; i++)
        {
            if (char.ToLowerInvariant(entry.Title[i]) == char.ToLowerInvariant(query[qIdx]))
            {
                qIdx++;
            }
        }

        return qIdx == query.Length ? 200 : -1;
    }
}
