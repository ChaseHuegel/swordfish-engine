using System;
using System.Collections.Generic;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// Normal-priority modifier source for <see cref="StatisticIds.MINING_SPEED"/> that adds a percentage
/// speed bonus from the highest skill level awarding break XP for the targeted brick.
/// </summary>
public sealed class SkillMiningSpeedModifierSource(ISkillDatabase? skillDatabase = null) : IStatModifierSource
{
    /// <summary>Mining speed percentage bonus added per matching skill level (2% per level).</summary>
    public const float SKILL_SPEED_BONUS_PER_LEVEL = 0.02f;

    /// <inheritdoc/>
    public StatModifierPriority Priority => StatModifierPriority.Normal;

    /// <inheritdoc/>
    public string? StatId => StatisticIds.MINING_SPEED;

    /// <inheritdoc/>
    public void Apply(in StatQuery query, ref StatAccumulator accumulator)
    {
        if (skillDatabase == null || query.TargetBrickDataId == 0)
        {
            return;
        }

        int highestLevel = query.SkillXp != null
            ? ResolveHighestBreakLevel(skillDatabase, query.TargetBrickDataId, new DictionaryXpLookup(query.SkillXp))
            : ResolveHighestBreakLevel(skillDatabase, query.TargetBrickDataId, new StatisticsXpLookup(query.CharacterStatistics));

        if (highestLevel > 0)
        {
            accumulator.Apply(StatModifier.AddPercent(highestLevel * SKILL_SPEED_BONUS_PER_LEVEL));
        }
    }

    private static int ResolveHighestBreakLevel<TLookup>(
        ISkillDatabase skillDatabase,
        ushort brickDataId,
        TLookup lookup
    ) where TLookup : struct, IXpLookup
    {
        IReadOnlyList<Skill> breakSkills = skillDatabase.Get(XPSource.Break);
        var highestLevel = 0;

        for (var i = 0; i < breakSkills.Count; i++)
        {
            Skill skill = breakSkills[i];
            if (!skill.GetXP(XPSource.Break, brickDataId).Success)
            {
                continue;
            }

            long totalXp = lookup.GetXP(skill.ID);
            int level = skill.CalculateLevel(totalXp).Level;
            if (level > highestLevel)
            {
                highestLevel = level;
            }
        }

        return highestLevel;
    }

    private interface IXpLookup
    {
        long GetXP(string skillId);
    }

    private readonly struct DictionaryXpLookup(IReadOnlyDictionary<string, long>? map) : IXpLookup
    {
        public long GetXP(string skillId)
        {
            return map != null && map.TryGetValue(skillId, out long xp) ? xp : 0L;
        }
    }

    private readonly struct StatisticsXpLookup(Statistic[]? statistics) : IXpLookup
    {
        public long GetXP(string skillId)
        {
            if (statistics == null)
            {
                return 0L;
            }

            for (var i = 0; i < statistics.Length; i++)
            {
                if (string.Equals(statistics[i].ID, skillId, StringComparison.Ordinal))
                {
                    return statistics[i].Value;
                }
            }

            return 0L;
        }
    }
}
