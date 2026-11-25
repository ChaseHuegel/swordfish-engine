using FuzzySharp;

namespace WaywardBeyond.AssetTools.Search;

/// <summary>
///     Deterministic command palette search index ranking exact, prefix, token, and FuzzySharp fuzzy matches.
/// </summary>
public sealed class CommandPaletteSearchIndex
{
    private const int FUZZY_THRESHOLD = 55;
    private readonly List<SearchEntry> _entries = [];

    /// <summary>
    ///     Gets the number of entries currently stored in the index.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    ///     Replaces all indexed entries with <paramref name="entries"/>.
    /// </summary>
    public void SetEntries(IReadOnlyList<SearchEntry> entries)
    {
        if (entries == null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        _entries.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            _entries.Add(entries[i]);
        }
    }

    /// <summary>
    ///     Appends a single entry to the search index.
    /// </summary>
    public void Add(in SearchEntry entry)
    {
        _entries.Add(entry);
    }

    /// <summary>
    ///     Removes all entries from the search index.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
    }

    /// <summary>
    ///     Executes <paramref name="query"/> with deterministic exact, prefix, token, and fuzzy ranking.
    /// </summary>
    public IReadOnlyList<SearchResult> Search(in SearchQuery query)
    {
        if (query.MaxResults <= 0)
        {
            return [];
        }

        string trimmedQuery = query.Text?.Trim() ?? string.Empty;
        string normalizedQuery = trimmedQuery.ToLowerInvariant();
        string[] queryTokens = Tokenize(normalizedQuery);

        List<SearchResult> results = [];
        for (int i = 0; i < _entries.Count; i++)
        {
            SearchEntry entry = _entries[i];
            if (!MatchesFilters(in entry, in query))
            {
                continue;
            }

            if (trimmedQuery.Length == 0)
            {
                results.Add(new SearchResult(entry, SearchMatchTier.Exact, 0));
                continue;
            }

            SearchResult? evaluated = EvaluateEntry(in entry, trimmedQuery, normalizedQuery, queryTokens);
            if (evaluated.HasValue)
            {
                results.Add(evaluated.Value);
            }
        }

        results.Sort(CompareResults);
        if (results.Count > query.MaxResults)
        {
            results.RemoveRange(query.MaxResults, results.Count - query.MaxResults);
        }

        return results;
    }

    private static bool MatchesFilters(in SearchEntry entry, in SearchQuery query)
    {
        if (query.KindFilter.HasValue && entry.Kind != query.KindFilter.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.AssetKindFilter) &&
            !string.Equals(entry.AssetKind, query.AssetKindFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        switch (query.ScopeFilter)
        {
            case SearchScope.Writable:
                return entry.Provenance.IsWritable;

            case SearchScope.Reference:
                return !entry.Provenance.IsWritable;

            case SearchScope.Overridden:
                return entry.Provenance.IsShadowed || !string.IsNullOrEmpty(entry.Provenance.ShadowedByRoot);

            default:
                return true;
        }
    }

    private static SearchResult? EvaluateEntry(
        in SearchEntry entry,
        string rawQuery,
        string normalizedQuery,
        string[] queryTokens)
    {
        List<string> candidates = CollectSearchableFields(in entry);
        bool hasMatch = false;
        SearchMatchTier bestTier = SearchMatchTier.Fuzzy;
        int bestScore = int.MinValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            int fieldPriorityBonus = i == 0 ? 20 : i == 1 ? 10 : 0;
            if (!TryScoreCandidate(candidate, rawQuery, normalizedQuery, queryTokens, out SearchMatchTier tier, out int score))
            {
                continue;
            }

            int totalScore = score + fieldPriorityBonus;
            if (!hasMatch || tier < bestTier || (tier == bestTier && totalScore > bestScore))
            {
                hasMatch = true;
                bestTier = tier;
                bestScore = totalScore;
            }
        }

        return hasMatch ? new SearchResult(entry, bestTier, bestScore) : null;
    }

    private static List<string> CollectSearchableFields(in SearchEntry entry)
    {
        List<string> fields = [];
        AddIfPresent(fields, entry.Id);
        AddIfPresent(fields, entry.DisplayName);
        AddIfPresent(fields, entry.LocalizationKey);

        if (!string.IsNullOrWhiteSpace(entry.ResourcePath))
        {
            AddIfPresent(fields, entry.ResourcePath);
            string fileName = Path.GetFileName(entry.ResourcePath);
            AddIfPresent(fields, fileName);
            string stem = Path.GetFileNameWithoutExtension(entry.ResourcePath);
            AddIfPresent(fields, stem);
        }

        if (entry.ExtraTokens != null)
        {
            for (int i = 0; i < entry.ExtraTokens.Count; i++)
            {
                AddIfPresent(fields, entry.ExtraTokens[i]);
            }
        }

        return fields;
    }

    private static void AddIfPresent(List<string> fields, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(value);
        }
    }

    private static bool TryScoreCandidate(
        string candidate,
        string rawQuery,
        string normalizedQuery,
        string[] queryTokens,
        out SearchMatchTier tier,
        out int score)
    {
        string normalizedCandidate = candidate.ToLowerInvariant();

        if (string.Equals(normalizedCandidate, normalizedQuery, StringComparison.Ordinal))
        {
            tier = SearchMatchTier.Exact;
            score = string.Equals(candidate, rawQuery, StringComparison.Ordinal) ? 1010 : 1000;
            return true;
        }

        if (normalizedCandidate.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            tier = SearchMatchTier.Prefix;
            int lengthPenalty = Math.Min(200, normalizedCandidate.Length - normalizedQuery.Length);
            score = 800 - lengthPenalty;
            return true;
        }

        if (queryTokens.Length > 0 && TryScoreTokenMatch(normalizedCandidate, queryTokens, out int tokenScore))
        {
            tier = SearchMatchTier.Token;
            score = tokenScore;
            return true;
        }

        int fuzzyScore = Fuzz.WeightedRatio(normalizedQuery, normalizedCandidate);
        if (fuzzyScore >= FUZZY_THRESHOLD)
        {
            tier = SearchMatchTier.Fuzzy;
            score = fuzzyScore;
            return true;
        }

        tier = SearchMatchTier.Fuzzy;
        score = 0;
        return false;
    }

    private static bool TryScoreTokenMatch(string normalizedCandidate, string[] queryTokens, out int score)
    {
        string[] candidateTokens = Tokenize(normalizedCandidate);
        if (candidateTokens.Length == 0)
        {
            score = 0;
            return false;
        }

        int exactTokenMatches = 0;
        for (int i = 0; i < queryTokens.Length; i++)
        {
            string qToken = queryTokens[i];
            bool matched = false;
            bool exact = false;

            for (int j = 0; j < candidateTokens.Length; j++)
            {
                string cToken = candidateTokens[j];
                if (string.Equals(cToken, qToken, StringComparison.Ordinal))
                {
                    matched = true;
                    exact = true;
                    break;
                }

                if (cToken.StartsWith(qToken, StringComparison.Ordinal))
                {
                    matched = true;
                }
            }

            if (!matched)
            {
                score = 0;
                return false;
            }

            if (exact)
            {
                exactTokenMatches++;
            }
        }

        int extraTokensPenalty = Math.Min(100, Math.Max(0, candidateTokens.Length - queryTokens.Length) * 5);
        score = 600 + (exactTokenMatches * 25) - extraTokensPenalty;
        return true;
    }

    private static string[] Tokenize(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        List<string> tokens = [];
        int start = -1;
        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            bool isSeparator = char.IsWhiteSpace(c) || c == ':' || c == '.' || c == '_' || c == '-' || c == '/' || c == '\\';
            if (isSeparator)
            {
                if (start >= 0)
                {
                    tokens.Add(normalized.Substring(start, i - start));
                    start = -1;
                }
            }
            else if (start < 0)
            {
                start = i;
            }
        }

        if (start >= 0)
        {
            tokens.Add(normalized.Substring(start));
        }

        return [.. tokens];
    }

    private static int CompareResults(SearchResult left, SearchResult right)
    {
        int tierCompare = left.Tier.CompareTo(right.Tier);
        if (tierCompare != 0)
        {
            return tierCompare;
        }

        int scoreCompare = right.Score.CompareTo(left.Score);
        if (scoreCompare != 0)
        {
            return scoreCompare;
        }

        int shadowedCompare = left.Entry.Provenance.IsShadowed.CompareTo(right.Entry.Provenance.IsShadowed);
        if (shadowedCompare != 0)
        {
            return shadowedCompare;
        }

        int kindCompare = left.Entry.Kind.CompareTo(right.Entry.Kind);
        if (kindCompare != 0)
        {
            return kindCompare;
        }

        int idCompare = StringComparer.OrdinalIgnoreCase.Compare(left.Entry.Id, right.Entry.Id);
        if (idCompare != 0)
        {
            return idCompare;
        }

        return StringComparer.OrdinalIgnoreCase.Compare(left.Entry.Provenance.RootName, right.Entry.Provenance.RootName);
    }
}
