using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace WaywardBeyond.Recipes;

/// <summary>A validated input requirement for a <see cref="Recipe"/>.</summary>
public readonly struct RecipeIngredient
{
    private readonly string[] _resolvedItemIds;
    private readonly IReadOnlyList<string> _resolvedReadOnly;
    private readonly FrozenSet<string>? _resolvedItemSet;

    internal RecipeIngredient(
        string itemOrTag,
        string? itemId,
        string? tag,
        int count,
        string? slotGroup,
        string[] resolvedItemIds,
        FrozenSet<string>? resolvedItemSet
    )
    {
        ItemOrTag = itemOrTag;
        ItemId = itemId;
        Tag = tag;
        Count = count;
        SlotGroup = slotGroup;
        _resolvedItemIds = resolvedItemIds;
        _resolvedReadOnly = Array.AsReadOnly(resolvedItemIds);
        _resolvedItemSet = resolvedItemSet;
    }

    /// <summary>The authored item identifier or <c>tag:&lt;name&gt;</c> selector.</summary>
    public string ItemOrTag { get; }

    /// <summary>The direct item identifier, or null when matching by tag.</summary>
    public string? ItemId { get; }

    /// <summary>The invariant tag name without the <c>tag:</c> prefix, or null when matching a direct item.</summary>
    public string? Tag { get; }

    /// <summary>The required item quantity.</summary>
    public int Count { get; }

    /// <summary>The optional target slot group identifier.</summary>
    public string? SlotGroup { get; }

    /// <summary>Whether this ingredient matches via an invariant tag rather than a single item identifier.</summary>
    public bool IsTag => Tag != null;

    /// <summary>The resolved item identifiers accepted by this ingredient.</summary>
    public IReadOnlyList<string> ResolvedItemIds => _resolvedReadOnly ?? (IReadOnlyList<string>)Array.Empty<string>();

    internal string[] ResolvedItemArray => _resolvedItemIds ?? [];

    /// <summary>Returns whether <paramref name="itemId"/> satisfies this ingredient.</summary>
    public bool Matches(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        if (ItemId != null)
        {
            return string.Equals(ItemId, itemId, StringComparison.Ordinal);
        }

        return _resolvedItemSet != null && _resolvedItemSet.Contains(itemId);
    }
}
