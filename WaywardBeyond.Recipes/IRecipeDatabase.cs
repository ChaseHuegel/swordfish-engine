using System;
using System.Collections.Generic;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Recipes;

/// <summary>Provides access to loaded <see cref="Recipe"/> definitions and station matching queries.</summary>
public interface IRecipeDatabase : IAssetDatabase<Recipe>
{
    /// <summary>Gets all loaded recipes in deterministic order.</summary>
    IReadOnlyList<Recipe> All { get; }

    /// <summary>Gets all recipes for <paramref name="stationTag"/> ordered by <see cref="Recipe.Order"/> then <see cref="Recipe.ID"/>.</summary>
    IReadOnlyList<Recipe> GetByStationTag(string stationTag);

    /// <summary>Finds the first automatic recipe for <paramref name="stationTag"/> satisfied by <paramref name="inputs"/>.</summary>
    Result<Recipe> FindAutoMatch(string stationTag, in InventoryComponent inputs)
    {
        ReadOnlySpan<ItemData> span = inputs.Contents != null ? inputs.Contents.AsSpan() : ReadOnlySpan<ItemData>.Empty;
        return FindAutoMatch(stationTag, span);
    }

    /// <summary>Finds the first automatic recipe for <paramref name="stationTag"/> satisfied by <paramref name="inputs"/>.</summary>
    Result<Recipe> FindAutoMatch(string stationTag, ReadOnlySpan<ItemData> inputs);

    /// <summary>Returns whether any recipe for <paramref name="stationTag"/> accepts <paramref name="itemId"/> as an input.</summary>
    bool IsValidInputForStation(string stationTag, string itemId);
}
