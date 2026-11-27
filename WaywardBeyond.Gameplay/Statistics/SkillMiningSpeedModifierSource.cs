using System.Collections.Generic;
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
        if (skillDatabase == null ||
            query.TargetBrickDataId == 0 ||
            query.Store == null ||
            query.Entity < 0 ||
            !query.Store.TryGet(query.Entity, out SkillStateComponent skillState))
        {
            return;
        }

        IReadOnlyList<Skill> breakSkills = skillDatabase.Get(XPSource.Break);
        var highestLevel = 0;

        for (var i = 0; i < breakSkills.Count; i++)
        {
            Skill skill = breakSkills[i];
            if (!skill.GetXP(XPSource.Break, query.TargetBrickDataId).Success)
            {
                continue;
            }

            long totalXp = skillState.GetXP(skill.ID);
            int level = skill.CalculateLevel(totalXp).Level;
            if (level > highestLevel)
            {
                highestLevel = level;
            }
        }

        if (highestLevel > 0)
        {
            accumulator.Apply(StatModifier.AddPercent(highestLevel * SKILL_SPEED_BONUS_PER_LEVEL));
        }
    }
}
