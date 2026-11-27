using System;
using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Skills;

/// <summary>An asset representing a skill.</summary>
public sealed class Skill
{
    private readonly Dictionary<XPSource, Dictionary<ushort, int>> _sources;
    private readonly Dictionary<XPSource, CraftXpSourceSet>? _craftSources;
    private readonly SortedDictionary<int, int> _levels;
    private readonly long[] _thresholds;
    private readonly int[] _levelNumbers;

    /// <summary>Constructs a skill with brick XP sources and a level progression curve.</summary>
    public Skill(
        string id,
        string name,
        string category,
        string? icon,
        Dictionary<XPSource, Dictionary<ushort, int>> sources,
        SortedDictionary<int, int> levels
    ) : this(id, name, category, icon, sources, levels, craftSources: null)
    {
    }

    internal Skill(
        string id,
        string name,
        string category,
        string? icon,
        Dictionary<XPSource, Dictionary<ushort, int>> sources,
        SortedDictionary<int, int> levels,
        Dictionary<XPSource, CraftXpSourceSet>? craftSources
    )
    {
        ID = id;
        Name = name;
        Category = category;
        Icon = icon;
        _sources = sources;
        _craftSources = craftSources;
        _levels = levels;
        _thresholds = CumulativeThresholds(levels);
        _levelNumbers = LevelNumbers(levels);
    }

    /// <summary>The unique identifier of the skill.</summary>
    public string ID { get; }

    /// <summary>The localization key of the skill display name.</summary>
    public string Name { get; }

    /// <summary>The localization key of the skill category.</summary>
    public string Category { get; }

    /// <summary>The optional skill icon texture path.</summary>
    public string? Icon { get; }

    /// <summary>The level curve keyed by level, in ascending level order.</summary>
    public IReadOnlyDictionary<int, int> Levels => _levels;

    /// <summary>Gets the XP this skill grants for the provided source and brick data id.</summary>
    /// <returns>The granted XP, or a failed result when the brick grants no XP for this skill.</returns>
    public Result<int> GetXP(XPSource source, ushort brickDataID)
    {
        if (_sources.TryGetValue(source, out Dictionary<ushort, int>? sourceData) &&
            sourceData.TryGetValue(brickDataID, out int xp))
        {
            return Result<int>.FromSuccess(xp);
        }

        return default;
    }

    /// <summary>Gets the XP this skill grants for a crafting or processing action with an optional single output item.</summary>
    /// <returns>The granted XP, or a failed result when the action grants no XP for this skill.</returns>
    public Result<int> GetXP(XPSource source, string recipeId, string stationTag, string? outputItemId = null)
    {
        if (_craftSources == null || !_craftSources.TryGetValue(source, out CraftXpSourceSet? sourceSet))
        {
            return default;
        }

        return sourceSet.Evaluate(recipeId, stationTag, outputItemId);
    }

    /// <summary>Gets the XP this skill grants for a crafting or processing action across all produced output items.</summary>
    /// <returns>The granted XP, or a failed result when the action grants no XP for this skill.</returns>
    public Result<int> GetXP(XPSource source, string recipeId, string stationTag, ReadOnlySpan<string> outputItemIds)
    {
        if (_craftSources == null || !_craftSources.TryGetValue(source, out CraftXpSourceSet? sourceSet))
        {
            return default;
        }

        return sourceSet.Evaluate(recipeId, stationTag, outputItemIds);
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

        int nextLevelXP = _levels[_levelNumbers[reached]];
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
