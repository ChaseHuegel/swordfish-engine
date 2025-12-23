using System.Collections.Generic;

namespace WaywardBeyond.Client.Core.Meta;

public sealed class LocalizedTags(in Dictionary<string, List<string>> tags)
{
    internal readonly Dictionary<string, List<string>> Tags = tags;

    public IReadOnlyList<string>? GetValues(string tag)
    {
        return !Tags.TryGetValue(tag, out List<string>? values) ? null : values;
    }
}