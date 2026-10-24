using System.Collections.Generic;

namespace WaywardBeyond.Skills;

/// <summary>Defines a skill asset.</summary>
internal struct SkillDefinition()
{
    /// <summary>The unique ID of the skill.</summary>
    public string? ID;

    /// <summary>The localization key of the skill name.</summary>
    public string? Name;

    /// <summary>The localization key of the skill category.</summary>
    public string? Category;

    /// <summary>The icon path of the skill.</summary>
    public string? Icon;

    /// <summary>XP sources keyed by source, then by brick id or <c>tag:&lt;name&gt;</c>.</summary>
    public Dictionary<XPSource, Dictionary<string, int>>? Sources;

    /// <summary>The level curve keyed by level.</summary>
    public Dictionary<int, int>? Levels;
}
