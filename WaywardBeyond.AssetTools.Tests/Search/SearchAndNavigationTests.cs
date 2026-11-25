using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Search;

namespace WaywardBeyond.AssetTools.Tests.Search;

/// <summary>
///     Verifies deterministic command palette ranking tiers, filters, provenance ordering, and navigation history.
/// </summary>
public sealed class SearchAndNavigationTests
{
    [Fact]
    public void RankingOrdersExactBeforePrefixBeforeTokenBeforeFuzzy()
    {
        var writable = new SearchProvenance("MyPack", IsWritable: true, IsShadowed: false);
        var index = new CommandPaletteSearchIndex();
        index.SetEntries(
        [
            new SearchEntry("wb:kore", "Kore", SearchEntryKind.Asset, writable, SearchResolutionState.Resolved, AssetKind: "brick"),
            new SearchEntry("wb:reactor_core", "Reactor Core", SearchEntryKind.Asset, writable, SearchResolutionState.Resolved, AssetKind: "brick"),
            new SearchEntry("core_panel", "Core Panel", SearchEntryKind.Asset, writable, SearchResolutionState.Resolved, AssetKind: "brick"),
            new SearchEntry("core", "Core", SearchEntryKind.Asset, writable, SearchResolutionState.Resolved, AssetKind: "brick")
        ]);

        IReadOnlyList<SearchResult> results = index.Search(new SearchQuery("core"));

        Assert.Equal(4, results.Count);
        Assert.Equal("core", results[0].Entry.Id);
        Assert.Equal(SearchMatchTier.Exact, results[0].Tier);
        Assert.Equal("core_panel", results[1].Entry.Id);
        Assert.Equal(SearchMatchTier.Prefix, results[1].Tier);
        Assert.Equal("wb:reactor_core", results[2].Entry.Id);
        Assert.Equal(SearchMatchTier.Token, results[2].Tier);
        Assert.Equal("wb:kore", results[3].Entry.Id);
        Assert.Equal(SearchMatchTier.Fuzzy, results[3].Tier);
    }

    [Fact]
    public void FuzzySharpMatchesTyposAndResourcesMatchByFileStem()
    {
        var reference = new SearchProvenance("BaseGame", IsWritable: false, IsShadowed: false);
        var index = new CommandPaletteSearchIndex();
        index.SetEntries(
        [
            new SearchEntry("wb:control_panel", "Control Panel", SearchEntryKind.Asset, reference, SearchResolutionState.Resolved, AssetKind: "brick", LocalizationKey: "brick.base.control_panel"),
            new SearchEntry("items/drill.png", "drill.png", SearchEntryKind.Resource, reference, SearchResolutionState.Resolved, ResourcePath: "items/drill.png")
        ]);

        IReadOnlyList<SearchResult> typoResults = index.Search(new SearchQuery("contorl panle"));
        Assert.Single(typoResults);
        Assert.Equal("wb:control_panel", typoResults[0].Entry.Id);
        Assert.Equal(SearchMatchTier.Fuzzy, typoResults[0].Tier);

        IReadOnlyList<SearchResult> stemResults = index.Search(new SearchQuery("drill", KindFilter: SearchEntryKind.Resource));
        Assert.Single(stemResults);
        Assert.Equal("items/drill.png", stemResults[0].Entry.Id);
        Assert.Equal(SearchMatchTier.Exact, stemResults[0].Tier);
    }

    [Fact]
    public void ScopeFiltersAndShadowedProvenanceOrderingAreDeterministic()
    {
        var winning = new SearchProvenance("ModPack", IsWritable: true, IsShadowed: false, FilePath: "/pack/core.toml");
        var shadowed = new SearchProvenance("BaseGame", IsWritable: false, IsShadowed: true, ShadowedByRoot: "ModPack", FilePath: "/game/core.toml");
        var actionProv = new SearchProvenance("Studio", IsWritable: true, IsShadowed: false);

        var index = new CommandPaletteSearchIndex();
        index.SetEntries(
        [
            new SearchEntry("wb:core", "Core", SearchEntryKind.Asset, shadowed, SearchResolutionState.Shadowed, AssetKind: "brick"),
            new SearchEntry("wb:core", "Core", SearchEntryKind.Asset, winning, SearchResolutionState.Resolved, AssetKind: "brick"),
            new SearchEntry("action:validate", "Validate Workspace", SearchEntryKind.Action, actionProv, SearchResolutionState.Resolved)
        ]);

        IReadOnlyList<SearchResult> allCore = index.Search(new SearchQuery("wb:core"));
        Assert.Equal(2, allCore.Count);
        Assert.Equal("ModPack", allCore[0].Entry.Provenance.RootName);
        Assert.False(allCore[0].Entry.Provenance.IsShadowed);
        Assert.Equal("BaseGame", allCore[1].Entry.Provenance.RootName);
        Assert.True(allCore[1].Entry.Provenance.IsShadowed);

        IReadOnlyList<SearchResult> writableOnly = index.Search(new SearchQuery("wb:core", ScopeFilter: SearchScope.Writable));
        Assert.Single(writableOnly);
        Assert.Equal("ModPack", writableOnly[0].Entry.Provenance.RootName);

        IReadOnlyList<SearchResult> overriddenOnly = index.Search(new SearchQuery("wb:core", ScopeFilter: SearchScope.Overridden));
        Assert.Single(overriddenOnly);
        Assert.Equal("BaseGame", overriddenOnly[0].Entry.Provenance.RootName);

        IReadOnlyList<SearchResult> actionsOnly = index.Search(new SearchQuery("validate", KindFilter: SearchEntryKind.Action));
        Assert.Single(actionsOnly);
        Assert.Equal("action:validate", actionsOnly[0].Entry.Id);
    }

    [Fact]
    public void NavigationHistorySupportsBackForwardBranchResetAndCapacity()
    {
        var history = new NavigationHistory(maxCapacity: 2);
        var a = new NavigationTarget("wb:a", "/a.toml", Line: 1);
        var b = new NavigationTarget("wb:b", "/b.toml", Line: 2);
        var c = new NavigationTarget("wb:c", "/c.toml", Line: 3);
        var d = new NavigationTarget("wb:d", "/d.toml", Line: 4);

        Assert.False(history.CanGoBack);
        Assert.False(history.CanGoForward);
        Assert.False(history.GoBack().Success);

        history.Navigate(in a);
        history.Navigate(in a);
        Assert.Equal(0, history.BackCount);

        history.Navigate(in b);
        history.Navigate(in c);
        history.Navigate(in d);
        Assert.Equal(2, history.BackCount);
        Assert.Equal(d, history.Current);

        Result<NavigationTarget> back1 = history.GoBack();
        Assert.True(back1.Success);
        Assert.Equal(c, back1.Value);

        Result<NavigationTarget> back2 = history.GoBack();
        Assert.True(back2.Success);
        Assert.Equal(b, back2.Value);
        Assert.False(history.CanGoBack);

        Result<NavigationTarget> forward1 = history.GoForward();
        Assert.True(forward1.Success);
        Assert.Equal(c, forward1.Value);
        Assert.True(history.CanGoForward);

        history.Navigate(in a);
        Assert.False(history.CanGoForward);
        Assert.Equal(a, history.Current);
    }
}
