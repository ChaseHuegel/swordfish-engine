using System.Collections.Generic;

namespace WaywardBeyond.Shared.Skills;

/// <summary>
/// The headless runtime representation of a skill. Carry only data the shared mechanics need: the
/// localization keys for name/category, the icon path, the level curve, and the XP sources keyed by
/// brick data id. No textures, materials, or resolved display strings.
/// </summary>
public sealed class SkillData(
    string id,
    string name,
    string category,
    string? icon,
    int maxLevel,
    Dictionary<XPSource, Dictionary<ushort, int>> sources,
    Dictionary<int, int> levels
) {
    public string ID { get; } = id;
    public string Name { get; } = name;
    public string Category { get; } = category;
    public string? Icon { get; } = icon;
    public int MaxLevel { get; } = maxLevel;
    public Dictionary<XPSource, Dictionary<ushort, int>> Sources { get; } = sources;
    public Dictionary<int, int> Levels { get; } = levels;

    /// <summary>
    /// Returns the XP granted for breaking or placing the brick with the provided data id, if this
    /// skill sources XP from that brick.
    /// </summary>
    public bool TryGetXP(XPSource source, ushort brickDataID, out int xp)
    {
        if (Sources.TryGetValue(source, out Dictionary<ushort, int>? sources) && sources.TryGetValue(brickDataID, out xp))
        {
            return true;
        }

        xp = 0;
        return false;
    }
}