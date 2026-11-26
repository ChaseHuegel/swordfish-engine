using System;
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
    private readonly long[] _thresholds = CumulativeThresholds(levels);
    private readonly int[] _levelNumbers = LevelNumbers(levels);

    public string ID { get; } = id;
    public string Name { get; } = name;
    public string Category { get; } = category;
    public string? Icon { get; } = icon;

    /// <summary>The level curve keyed by level, in ascending level order.</summary>
    public IReadOnlyDictionary<int, int> Levels { get; } = levels;

    /// <summary>Gets the XP this skill grants for the provided source and brick data id.</summary>
    /// <returns>The granted XP, or a failed result when the brick grants no XP for this skill.</returns>
    public Result<int> GetXP(XPSource source, ushort brickDataID)
    {
        if (sources.TryGetValue(source, out Dictionary<ushort, int>? sourceData) &&
            sourceData.TryGetValue(brickDataID, out int xp))
        {
            return Result<int>.FromSuccess(xp);
        }

        return default;
    }

    /// <summary>Resolves the level for a total XP amount, plus the XP earned into that level.</summary>
    /// <remarks>A level is reached only once the total XP exceeds the sum of every requirement up to it.</remarks>
    public LevelInfo CalculateLevel(long totalXP)
    {
        int reached = CountReachedLevels(totalXP);
        if (reached == 0)
        {
            return new LevelInfo(0, totalXP);
        }

        return new LevelInfo(_levelNumbers[reached - 1], totalXP - _thresholds[reached - 1]);
    }

    /// <summary>Resolves level, XP into level, next-level requirement, and normalized bar progress for a total XP amount.</summary>
    public SkillProgress CalculateProgress(long totalXP)
    {
        int reached = CountReachedLevels(totalXP);
        int level = reached == 0 ? 0 : _levelNumbers[reached - 1];
        long xpIntoLevel = reached == 0 ? totalXP : totalXP - _thresholds[reached - 1];

        if (reached >= _levelNumbers.Length)
        {
            return new SkillProgress(
                Level: level,
                TotalXP: totalXP,
                XPIntoLevel: xpIntoLevel,
                NextLevelXP: 0,
                Progress: 1f,
                IsMaxLevel: true
            );
        }

        int nextLevelXP = levels[_levelNumbers[reached]];
        float progress = nextLevelXP > 0 ? Math.Clamp((float)xpIntoLevel / nextLevelXP, 0f, 1f) : 1f;
        return new SkillProgress(
            Level: level,
            TotalXP: totalXP,
            XPIntoLevel: xpIntoLevel,
            NextLevelXP: nextLevelXP,
            Progress: progress,
            IsMaxLevel: false
        );
    }

    private int CountReachedLevels(long totalXP)
    {
        //  An exact threshold match reports the lower level because a level is reached only by exceeding it.
        int index = Array.BinarySearch(_thresholds, totalXP);
        return index < 0 ? ~index : index;
    }

    private static long[] CumulativeThresholds(SortedDictionary<int, int> levels)
    {
        var thresholds = new long[levels.Count];
        long total = 0;
        var i = 0;
        foreach (int requirement in levels.Values)
        {
            total += requirement;
            thresholds[i] = total;
            i++;
        }

        return thresholds;
    }

    private static int[] LevelNumbers(SortedDictionary<int, int> levels)
    {
        var levelNumbers = new int[levels.Count];
        var i = 0;
        foreach (int level in levels.Keys)
        {
            levelNumbers[i] = level;
            i++;
        }

        return levelNumbers;
    }
}
