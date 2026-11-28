using System.Collections.Generic;

namespace WaywardBeyond.Data.Localization;

/// <summary>A language's tag lists.</summary>
public sealed class LocalizedTags
{
    internal readonly Dictionary<string, LocalizedTagValues> Tags = [];

    /// <summary>Constructs a language's tag lists.</summary>
    public LocalizedTags(in Dictionary<string, List<string>> tags)
    {
        Merge(tags);
    }

    /// <summary>All tag keys present in this table.</summary>
    public IReadOnlyCollection<string> Keys => Tags.Keys;

    /// <summary>Gets the values for a tag in first-occurrence order, or null when the tag has no entries.</summary>
    public IReadOnlyList<string>? GetValues(string tag)
    {
        return Tags.GetValueOrDefault(tag);
    }

    /// <summary>Returns whether a tag contains the specified value.</summary>
    public bool Contains(string tag, string value)
    {
        return Tags.TryGetValue(tag, out LocalizedTagValues? values) && values.Contains(value);
    }

    /// <summary>Appends tag values that are not already present.</summary>
    internal void Merge(Dictionary<string, List<string>> source)
    {
        foreach ((string tag, List<string> values) in source)
        {
            LocalizedTagValues target = GetOrAdd(tag);
            for (var i = 0; i < values.Count; i++)
            {
                target.Add(values[i]);
            }
        }
    }

    /// <summary>Appends tag values from another instance that are not already present.</summary>
    internal void Merge(LocalizedTags source)
    {
        foreach ((string tag, LocalizedTagValues values) in source.Tags)
        {
            LocalizedTagValues target = GetOrAdd(tag);
            for (var i = 0; i < values.Count; i++)
            {
                target.Add(values[i]);
            }
        }
    }

    private LocalizedTagValues GetOrAdd(string tag)
    {
        if (Tags.TryGetValue(tag, out LocalizedTagValues? values))
        {
            return values;
        }

        values = new LocalizedTagValues();
        Tags[tag] = values;
        return values;
    }
}
