using System;
using System.Collections.Generic;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Data;

/// <summary>Immutable schema describing a slot group's default capacity and tag filters.</summary>
public sealed class SlotGroupSchema
{
    private readonly IReadOnlyList<string> _allowedTags;
    private readonly Dictionary<int, IReadOnlyList<string>> _slotAllowedTags;

    /// <summary>Constructs a slot group schema with optional group-wide and per-slot filter tags.</summary>
    public SlotGroupSchema(
        string groupId,
        int defaultCapacity,
        string[]? allowedTags = null,
        IReadOnlyDictionary<int, string[]>? slotAllowedTags = null
    )
    {
        if (string.IsNullOrEmpty(groupId))
        {
            throw new ArgumentException("Group ID cannot be null or empty.", nameof(groupId));
        }

        GroupId = groupId;
        DefaultCapacity = Math.Max(0, defaultCapacity);
        _allowedTags = Array.AsReadOnly(SanitizeTags(allowedTags));
        _slotAllowedTags = [];

        if (slotAllowedTags != null)
        {
            foreach ((int index, string[] tags) in slotAllowedTags)
            {
                if (index >= 0)
                {
                    _slotAllowedTags[index] = Array.AsReadOnly(SanitizeTags(tags));
                }
            }
        }
    }

    /// <summary>The canonical namespaced slot group identifier.</summary>
    public string GroupId { get; }

    /// <summary>The default slot count for this group.</summary>
    public int DefaultCapacity { get; }

    /// <summary>Group-wide allowed invariant item tag keys; empty allows any item unless overridden per slot.</summary>
    public IReadOnlyList<string> AllowedTags => _allowedTags;

    /// <summary>Per-slot allowed invariant item tag overrides keyed by local slot index.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> SlotAllowedTags => _slotAllowedTags;

    /// <summary>Returns the effective allowed tag keys for <paramref name="localIndex"/>.</summary>
    public IReadOnlyList<string> GetAllowedTags(int localIndex)
    {
        return _slotAllowedTags.TryGetValue(localIndex, out IReadOnlyList<string>? slotTags)
            ? slotTags
            : _allowedTags;
    }

    /// <summary>Returns true when <paramref name="itemId"/> satisfies the filter tags for <paramref name="localIndex"/>.</summary>
    public bool AllowsItem(int localIndex, string? itemId, LocalizedTags? tags)
    {
        if (string.IsNullOrEmpty(itemId) || localIndex < 0)
        {
            return false;
        }

        IReadOnlyList<string> requiredTags = GetAllowedTags(localIndex);
        if (requiredTags.Count == 0)
        {
            return true;
        }

        if (tags == null)
        {
            return false;
        }

        for (var i = 0; i < requiredTags.Count; i++)
        {
            if (tags.Contains(requiredTags[i], itemId))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] SanitizeTags(string[]? tags)
    {
        if (tags == null || tags.Length == 0)
        {
            return [];
        }

        var filtered = new List<string>(tags.Length);
        for (var i = 0; i < tags.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(tags[i]))
            {
                filtered.Add(tags[i].Trim());
            }
        }

        return filtered.Count == 0 ? [] : [.. filtered];
    }
}
