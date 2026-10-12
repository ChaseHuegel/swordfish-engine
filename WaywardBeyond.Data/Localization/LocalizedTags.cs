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
}