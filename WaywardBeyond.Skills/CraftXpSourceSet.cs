using System;
using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Skills;

/// <summary>Resolved crafting and processing XP grants keyed by recipe, output item, tag, and station tag.</summary>
internal sealed class CraftXpSourceSet
{
    private readonly Dictionary<string, int> _recipeOverrides;
    private readonly Dictionary<string, int> _directTargets;
    private readonly Dictionary<string, int> _outputTagItems;
    private readonly Dictionary<string, int> _stationTags;

    /// <summary>Constructs a resolved crafting and processing XP lookup set.</summary>
    public CraftXpSourceSet(
        Dictionary<string, int> recipeOverrides,
        Dictionary<string, int> directTargets,
        Dictionary<string, int> outputTagItems,
        Dictionary<string, int> stationTags
    )
    {
        _recipeOverrides = recipeOverrides;
        _directTargets = directTargets;
        _outputTagItems = outputTagItems;
        _stationTags = stationTags;
    }

    /// <summary>Whether no crafting or processing XP grants were resolved.</summary>
    public bool IsEmpty =>
        _recipeOverrides.Count == 0 &&
        _directTargets.Count == 0 &&
        _outputTagItems.Count == 0 &&
        _stationTags.Count == 0;

    /// <summary>Resolves XP for a single optional output item identifier.</summary>
    public Result<int> Evaluate(string recipeId, string stationTag, string? outputItemId)
    {
        if (string.IsNullOrEmpty(outputItemId))
        {
            return Evaluate(recipeId, stationTag, ReadOnlySpan<string>.Empty);
        }

        var single = new ReadOnlySpan<string>(in outputItemId);
        return Evaluate(recipeId, stationTag, single);
    }

    /// <summary>Resolves XP by precedence: explicit recipe, direct recipe/output id, output item tag, then station tag.</summary>
    public Result<int> Evaluate(string recipeId, string stationTag, ReadOnlySpan<string> outputItemIds)
    {
        if (!string.IsNullOrEmpty(recipeId))
        {
            if (_recipeOverrides.TryGetValue(recipeId, out int recipeXp))
            {
                return Result<int>.FromSuccess(recipeXp);
            }

            if (_directTargets.TryGetValue(recipeId, out int directRecipeXp))
            {
                return Result<int>.FromSuccess(directRecipeXp);
            }
        }

        if (TryResolveOutputMax(_directTargets, outputItemIds, out int directOutputXp))
        {
            return Result<int>.FromSuccess(directOutputXp);
        }

        if (TryResolveOutputMax(_outputTagItems, outputItemIds, out int tagOutputXp))
        {
            return Result<int>.FromSuccess(tagOutputXp);
        }

        if (!string.IsNullOrEmpty(stationTag) && _stationTags.TryGetValue(stationTag, out int stationXp))
        {
            return Result<int>.FromSuccess(stationXp);
        }

        return default;
    }

    private static bool TryResolveOutputMax(Dictionary<string, int> map, ReadOnlySpan<string> outputItemIds, out int bestXp)
    {
        bestXp = 0;
        if (map.Count == 0 || outputItemIds.Length == 0)
        {
            return false;
        }

        bool matched = false;
        for (var i = 0; i < outputItemIds.Length; i++)
        {
            string itemId = outputItemIds[i];
            if (string.IsNullOrEmpty(itemId) || !map.TryGetValue(itemId, out int xp))
            {
                continue;
            }

            if (!matched || xp > bestXp)
            {
                bestXp = xp;
                matched = true;
            }
        }

        return matched;
    }
}
