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

    /// <summary>Gets all recipes for <paramref name="targetTag"/> ordered by <see cref="Recipe.Priority"/> then <see cref="Recipe.ID"/>.</summary>
    IReadOnlyList<Recipe> GetByTargetTag(string targetTag);

    /// <summary>Finds the first automatic recipe for <paramref name="targetTag"/> satisfied by <paramref name="inputs"/>.</summary>
    Result<Recipe> FindAutoMatch(string targetTag, in InventoryComponent inputs)
    {
        return FindAutoMatch(targetTag, inputs.AsSpan());
    }

    /// <summary>Finds the first automatic recipe for <paramref name="targetTag"/> satisfied by <paramref name="inputs"/>.</summary>
    Result<Recipe> FindAutoMatch(string targetTag, ReadOnlySpan<ItemData> inputs);

    /// <summary>Returns whether any recipe for <paramref name="targetTag"/> accepts <paramref name="itemId"/> as an input.</summary>
    bool IsValidInputForStation(string targetTag, string itemId);
}
