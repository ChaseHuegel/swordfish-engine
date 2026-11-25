using System.Collections.Generic;

namespace WaywardBeyond.Data.Localization;

/// <summary>A language's tag lists.</summary>
public sealed class LocalizedTags(in Dictionary<string, List<string>> tags)
{
    internal readonly Dictionary<string, List<string>> Tags = tags;

    /// <summary>Gets the values for a tag, or null when the tag has no entries.</summary>
    public IReadOnlyList<string>? GetValues(string tag)
    {
        return Tags.GetValueOrDefault(tag);
    }

    /// <summary>Returns whether a tag contains the specified value.</summary>
    public bool Contains(string tag, string value)
    {
        return Tags.TryGetValue(tag, out List<string>? values) && values.Contains(value);
    }
}