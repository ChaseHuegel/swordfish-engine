using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Skills;

/// <summary>An asset representing a skill.</summary>
public sealed class Skill(
    string id,
    string name,
    string category,
    string? icon,
    Dictionary<XPSource, Dictionary<ushort, int>> sources,
    SortedDictionary<int, int> levels
) {
    public string ID { get; } = id;
    public string Name { get; } = name;
    public string Category { get; } = category;
    public string? Icon { get; } = icon;

    /// <summary>The level curve keyed by level, in ascending level order.</summary>
    public IReadOnlyDictionary<int, int> Levels { get; } = levels;

    /// <summary>Gets the XP this skill grants for the provided source and brick data id.</summary>
    public Result<int> GetXP(XPSource source, ushort brickDataID)
    {
        if (sources.TryGetValue(source, out Dictionary<ushort, int>? sourceData) &&
            sourceData.TryGetValue(brickDataID, out int xp))
        {
            return Result<int>.FromSuccess(xp);
        }

        return Result<int>.FromFailure((string?)null);
    }

    /// <summary>Resolves the level for a total XP amount, plus the XP earned into that level.</summary>
    public LevelInfo CalculateLevel(long totalXP)
    {
        var currentLevel = 0;
        var levelXP = 0;
        foreach (KeyValuePair<int, int> level in Levels)
        {
            if (levelXP + level.Value < totalXP)
            {
                levelXP += level.Value;
                currentLevel = level.Key;
            }
            else
            {
                break;
            }
        }

        return new LevelInfo(currentLevel, totalXP - levelXP);
    }
}
