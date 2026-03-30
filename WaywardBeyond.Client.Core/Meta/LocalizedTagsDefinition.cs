using System.Collections.Generic;
using Tomlet.Attributes;

namespace WaywardBeyond.Client.Core.Meta;

internal struct LocalizedTagsDefinition
{
    public readonly Dictionary<string, List<string>> Tags;

    [TomlProperty("Language")]
    public string TwoLetterISOLanguageName { get; private set; }

    public LocalizedTagsDefinition()
    {
        Tags = [];
        TwoLetterISOLanguageName = string.Empty;
    }
    
    public LocalizedTagsDefinition(string twoLetterISOLanguageName)
    {
        TwoLetterISOLanguageName = twoLetterISOLanguageName;
        Tags = [];
    }

    public LocalizedTagsDefinition(string twoLetterISOLanguageName, Dictionary<string, List<string>> tags)
    {
        TwoLetterISOLanguageName = twoLetterISOLanguageName;
        Tags = tags;
    }
}