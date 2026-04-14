using System.Collections.Generic;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Skills;

internal sealed class Skill(
    string id,
    string name,
    string category,
    Material icon,
    int maxLevel,
    Dictionary<XPSource, Dictionary<string, int>> sources,
    Dictionary<int, int> levels
) {
    public string ID { get; } = id;
    public string Name { get; } = name;
    public string Category { get; } = category;
    public Material Icon { get; } = icon;
    public int MaxLevel { get; } = maxLevel;
    public Dictionary<XPSource, Dictionary<string, int>> Sources { get; } = sources;
    public Dictionary<int, int> Levels { get; } = levels;
}
