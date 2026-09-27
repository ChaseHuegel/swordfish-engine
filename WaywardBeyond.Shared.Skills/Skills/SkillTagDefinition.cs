using System.Collections.Generic;
using Tomlet.Attributes;

namespace WaywardBeyond.Shared.Skills;

/// <summary>
/// The TOML shape of the invariant brick tag lists under <c>lang/tags/</c>. Only entries with an empty
/// <see cref="TwoLetterISOLanguageName"/> define gameplay tags; language-scoped tag files are client
/// presentation and are ignored by the shared loader.
/// </summary>
public struct SkillTagDefinition
{
    public Dictionary<string, List<string>> Tags;

    [TomlProperty("Language")]
    public string TwoLetterISOLanguageName { get; private set; }

    public SkillTagDefinition()
    {
        Tags = [];
        TwoLetterISOLanguageName = string.Empty;
    }
}