using System.Collections.Generic;

namespace WaywardBeyond.Client.Core.Skills;

internal struct SkillDefinition()
{
    public string ID;
    public string Name;
    public string Category;
    public string? Icon;
    public int MaxLevel;
    public XPSources Sources;
    public Dictionary<int, int> Levels = [];
}