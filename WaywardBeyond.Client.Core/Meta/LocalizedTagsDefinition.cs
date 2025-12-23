using System.Collections.Generic;
using Tomlet.Attributes;

namespace WaywardBeyond.Client.Core.Meta;

internal struct LocalizedTagsDefinition(string twoLetterISOLanguageName)
{
    public readonly Dictionary<string, List<string>> Tags = [];

    [TomlProperty("Language")]
    public string TwoLetterISOLanguageName { get; private set; } = twoLetterISOLanguageName;
}