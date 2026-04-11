using System.Collections.Generic;

namespace WaywardBeyond.Client.Core.Skills;

public struct SkillDefinition()
{
    public string ID;
    public string Name;
    public string Category;
    public string? Icon;
    public int MaxLevel;
    public Dictionary<XPSource, Dictionary<string, int>> Sources;
    public Dictionary<int, int> Levels;
}