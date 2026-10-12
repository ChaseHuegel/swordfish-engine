using System.Collections.Generic;
using Tomlet.Attributes;

namespace WaywardBeyond.Data.Localization;

/// <summary>A localized tag list.</summary>
public struct LocalizedTagsDefinition
{
    /// <summary>Tag names mapped to their entries.</summary>
    public readonly Dictionary<string, List<string>> Tags;

    /// <summary>The two-letter ISO language name, or empty for invariant entries.</summary>
    [TomlProperty("Language")]
    public string TwoLetterISOLanguageName { get; private set; }

    /// <summary>Constructs an empty definition for parsing.</summary>
    public LocalizedTagsDefinition()
    {
        Tags = [];
        TwoLetterISOLanguageName = string.Empty;
    }

    /// <summary>Constructs a definition for a language and its tags.</summary>
    public LocalizedTagsDefinition(string twoLetterISOLanguageName, Dictionary<string, List<string>> tags)
    {
        TwoLetterISOLanguageName = twoLetterISOLanguageName;
        Tags = tags;
    }
}